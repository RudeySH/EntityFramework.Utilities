using EntityFramework.Utilities.Internal;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.Data.Entity.Core.Objects;
using System.Data.SqlClient;
using System.Text;
using System.Text.RegularExpressions;

namespace EntityFramework.Utilities;

public class SqlQueryProvider : IQueryProvider, INoOpAnalyzer
{
	public bool CanDelete => true;

	public bool CanUpdate => true;

	public bool CanInsert => true;

	public bool CanBulkUpdate => true;

	private static readonly Regex FromRegex = new(
		@"FROM \[([^\]]+)\]\.\[([^\]]+)\] AS (\[[^\]]+\])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex UpdateRegex = new(
		@"(\[[^\]]+\])[^=]+=(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public bool QueryIsNoOp(QueryInformation queryInformation)
	{
		return string.IsNullOrEmpty(queryInformation.Schema) &&
			string.IsNullOrEmpty(queryInformation.Table) &&
			(queryInformation.WhereSql?.Contains("WHERE 1 = 0") ?? false);
	}

	public string GetDeleteQuery(QueryInformation queryInfo)
	{
		var topExpression = queryInfo.TopExpression != null ? queryInfo.TopExpression + ' ' : null;

		return $"DELETE {topExpression}FROM [{queryInfo.Schema}].[{queryInfo.Table}] {queryInfo.WhereSql}";
	}

	public virtual string GetUpdateQuery(QueryInformation predicateQueryInfo, QueryInformation modificationQueryInfo)
	{
		var msql = modificationQueryInfo.WhereSql!.Replace("WHERE ", "");
		var indexOfAnd = msql.IndexOf("AND", StringComparison.Ordinal);
		var update = indexOfAnd == -1 ? msql : msql.Substring(0, indexOfAnd).Trim();

		var match = UpdateRegex.Match(update);
		string updateSql;

		if (match.Success)
		{
			var col = match.Groups[1];
			var rest = match.Groups[2].Value;

			rest = SqlStringHelper.FixParantheses(rest);

			updateSql = col.Value + " = " + rest;
		}
		else
		{
			updateSql = string.Join(" = ", update.Split([" = "], StringSplitOptions.RemoveEmptyEntries).Reverse());
		}

		return $"UPDATE [{predicateQueryInfo.Schema}].[{predicateQueryInfo.Table}] SET {updateSql} {predicateQueryInfo.WhereSql}";
	}

	public virtual int InsertItems<T>(
		DbContext dbContext, string schema, string tableName, IReadOnlyList<ColumnMapping> columns,
		IEnumerable<T> items, InsertAllOptions? options)
	{
		var sqlOptions = ((SqlInsertAllOptions?)options) ?? new SqlInsertAllOptions();

		var sqlConnection = (SqlConnection)this.GetUnderlyingConnection(dbContext.Database.Connection);
		var sqlTransaction = dbContext.Database.CurrentTransaction != null
			? (SqlTransaction)this.GetUnderlyingTransaction(dbContext.Database.CurrentTransaction)
			: null;

		var itemCollection = items as IReadOnlyCollection<T> ?? items.ToArray();

		using (var reader = new EFDataReader<T>(itemCollection, columns))
		{
			if (sqlConnection.State != ConnectionState.Open)
				sqlConnection.Open();

			using var copy = new SqlBulkCopy(sqlConnection, sqlOptions.SqlBulkCopyOptions, sqlTransaction);

			copy.BatchSize = sqlOptions?.BatchSize ?? 4000;
			copy.BulkCopyTimeout = dbContext.Database.CommandTimeout ?? 30;

			if (!string.IsNullOrWhiteSpace(schema))
				copy.DestinationTableName = $"[{schema}].[{tableName}]";
			else
				copy.DestinationTableName = $"[{tableName}]";

			copy.NotifyAfter = 0;

			for (var i = 0; i < reader.FieldCount; i++)
				copy.ColumnMappings.Add(i, columns[i].NameInDatabase);

			copy.WriteToServer(reader);

			copy.Close();
		}

		return itemCollection.Count;
	}

	public virtual async Task<int> InsertItemsAsync<T>(
		DbContext dbContext, string schema, string tableName, IReadOnlyList<ColumnMapping> columns,
		IEnumerable<T> items, InsertAllOptions? options, CancellationToken cancellationToken)
	{
		var sqlOptions = ((SqlInsertAllOptions?)options) ?? new SqlInsertAllOptions();

		var sqlConnection = (SqlConnection)this.GetUnderlyingConnection(dbContext.Database.Connection);
		var sqlTransaction = dbContext.Database.CurrentTransaction != null
			? (SqlTransaction)this.GetUnderlyingTransaction(dbContext.Database.CurrentTransaction)
			: null;

		var itemCollection = items as IReadOnlyCollection<T> ?? items.ToArray();

		var reader = new EFDataReader<T>(itemCollection, columns);

#if NETSTANDARD2_1 || NETSTANDARD2_1_OR_GREATER
		await using (reader.ConfigureAwait(false))
#else
		using (reader)
#endif
		{
			if (sqlConnection.State != ConnectionState.Open)
				await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

			var copyOptions = sqlOptions?.SqlBulkCopyOptions ?? SqlBulkCopyOptions.Default;
			using var copy = new SqlBulkCopy(sqlConnection, copyOptions, sqlTransaction);

			copy.BatchSize = sqlOptions?.BatchSize ?? 4000;
			copy.BulkCopyTimeout = dbContext.Database.CommandTimeout ?? 30;

			if (!string.IsNullOrWhiteSpace(schema))
				copy.DestinationTableName = $"[{schema}].[{tableName}]";
			else
				copy.DestinationTableName = $"[{tableName}]";

			copy.NotifyAfter = 0;

			for (var i = 0; i < reader.FieldCount; i++)
				copy.ColumnMappings.Add(i, columns[i].NameInDatabase);

			await copy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);

			copy.Close();
		}

		return itemCollection.Count;
	}

	public virtual int UpdateItems<T>(
		DbContext dbContext, string schema, string tableName, IReadOnlyList<ColumnMappingToUpdate> columns,
		IEnumerable<T> items, UpdateSpecification<T> updateSpecification, UpdateAllOptions? options)
	{
		var sqlOptions = ((SqlUpdateAllOptions?)options) ?? new SqlUpdateAllOptions();

		var tempTableName = $"#{Guid.NewGuid():N}";
		var propertiesToUpdate = new HashSet<string>(updateSpecification.Properties.Select(p => p.GetPropertyName()));

		if (!sqlOptions.InsertIfNotMatched)
			columns = columns.Where(p => p.IsPrimaryKey || propertiesToUpdate.Contains(p.NameOnObject)).ToArray();

		var commands = PrepareUpdateAllCommands(
			schema, tableName, tempTableName, columns, propertiesToUpdate, sqlOptions);

		if (dbContext.Database.Connection.State != ConnectionState.Open)
			dbContext.Database.Connection.Open();

		var transaction = sqlOptions.WrapInTransaction
			? dbContext.Database.Connection.BeginTransaction(sqlOptions.TransactionIsolationLevel)
			: null;

		try
		{
			// Create the temporary table.
			dbContext.Database.ExecuteSqlCommand(commands.CreateTempTable);

			// Insert items into the temporary table with SqlBulkCopy.
			var sqlInsertAllOptions = new SqlInsertAllOptions
			{
				BatchSize = sqlOptions.BatchSize,
				SqlBulkCopyOptions = sqlOptions.SqlBulkCopyOptions,
			};

			this.InsertItems(dbContext, schema, tempTableName, columns, items, sqlInsertAllOptions);

			// Update (or merge) rows in the original table.
			var rowsAffected = dbContext.Database.ExecuteSqlCommand(commands.UpdateOrMerge);

			// Delete the temporary table.
			dbContext.Database.ExecuteSqlCommand(commands.DeleteTempTable);

			transaction?.Commit();

			return rowsAffected;
		}
		finally
		{
			if (transaction != null)
			{
				transaction.Dispose();
				transaction = null;
			}
		}
	}

	public virtual async Task<int> UpdateItemsAsync<T>(
		DbContext dbContext, string schema, string tableName, IReadOnlyList<ColumnMappingToUpdate> columns,
		IEnumerable<T> items, UpdateSpecification<T> updateSpecification, UpdateAllOptions? options,
		CancellationToken cancellationToken)
	{
		var sqlOptions = ((SqlUpdateAllOptions?)options) ?? new SqlUpdateAllOptions();

		var tempTableName = $"#{Guid.NewGuid():N}";
		var propertiesToUpdate = new HashSet<string>(updateSpecification.Properties.Select(p => p.GetPropertyName()));

		if (!sqlOptions.InsertIfNotMatched)
			columns = columns.Where(p => p.IsPrimaryKey || propertiesToUpdate.Contains(p.NameOnObject)).ToArray();

		var commands = PrepareUpdateAllCommands(
			schema, tableName, tempTableName, columns, propertiesToUpdate, sqlOptions);

		if (dbContext.Database.Connection.State != ConnectionState.Open)
			await dbContext.Database.Connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var transaction = sqlOptions.WrapInTransaction
#if NETSTANDARD2_1 || NETSTANDARD2_1_OR_GREATER
			? await dbContext.Database.Connection.BeginTransactionAsync(sqlOptions.TransactionIsolationLevel, cancellationToken).ConfigureAwait(false)
#else
			? dbContext.Database.Connection.BeginTransaction(sqlOptions.TransactionIsolationLevel)
#endif
			: null;

		try
		{
			// Create the temporary table.
			await dbContext.Database.ExecuteSqlCommandAsync(commands.CreateTempTable, cancellationToken)
				.ConfigureAwait(false);

			// Insert items into the temporary table with SqlBulkCopy.
			var sqlInsertAllOptions = new SqlInsertAllOptions
			{
				BatchSize = sqlOptions.BatchSize,
				SqlBulkCopyOptions = sqlOptions.SqlBulkCopyOptions,
			};

			await this.InsertItemsAsync(
				dbContext, schema, tempTableName, columns, items, sqlInsertAllOptions, cancellationToken)
				.ConfigureAwait(false);

			// Update (or merge) rows in the original table.
			var rowsAffected = await dbContext.Database.ExecuteSqlCommandAsync(commands.UpdateOrMerge, cancellationToken)
				.ConfigureAwait(false);

			// Delete the temporary table.
			await dbContext.Database.ExecuteSqlCommandAsync(commands.DeleteTempTable, cancellationToken)
				.ConfigureAwait(false);

#if NETSTANDARD2_1 || NETSTANDARD2_1_OR_GREATER
			if (transaction != null)
				await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
#else
			transaction?.Commit();
#endif

			return rowsAffected;
		}
		finally
		{
			if (transaction != null)
			{
#if NETSTANDARD2_1 || NETSTANDARD2_1_OR_GREATER
				await transaction.DisposeAsync().ConfigureAwait(false);
#else
				transaction.Dispose();
#endif
				transaction = null;
			}
		}
	}

	private static UpdateAllCommands PrepareUpdateAllCommands(
		string schema, string tableName, string tempTableName, IReadOnlyList<ColumnMappingToUpdate> columns,
		HashSet<string> propertiesToUpdate, SqlUpdateAllOptions sqlOptions)
	{
		var schemaPrefix = !string.IsNullOrWhiteSpace(schema) ? $"[{schema}]." : null;

		// Prepare command for creating the temporary table.
		var columnDefinitions = columns.Select(c => $"[{c.NameInDatabase}] {c.DataTypeFull}{(c.DataType.EndsWith("char", StringComparison.Ordinal) ? " COLLATE DATABASE_DEFAULT" : null)}");
		var pkConstraint = string.Join(", ", columns.Where(c => c.IsPrimaryKey).Select(c => $"[{c.NameInDatabase}]"));
		var createTempTableSql = $"CREATE TABLE {schemaPrefix}[{tempTableName}] ({string.Join(", ", columnDefinitions)}, PRIMARY KEY ({pkConstraint}))";

		// Prepare command for updating (or merging) rows in the original table.
		var joinCondition = string.Join(" AND ", columns.Where(c => c.IsPrimaryKey).Select(c => $"t.[{c.NameInDatabase}] = s.[{c.NameInDatabase}]"));
		var columnsToSet = columns.Where(c => propertiesToUpdate.Contains(c.NameOnObject)).ToArray();
		var setters = string.Join(", ", columnsToSet.Select(c => $"t.[{c.NameInDatabase}] = s.[{c.NameInDatabase}]"));
		var updateOrMergeSql = new StringBuilder();

		if (sqlOptions.InsertIfNotMatched || sqlOptions.DeleteIfNotMatched)
		{
			updateOrMergeSql.Append($"MERGE {schemaPrefix}[{tableName}]");

			if (sqlOptions.TableHints?.Length > 0)
				updateOrMergeSql.Append($" WITH ({string.Join(", ", sqlOptions.TableHints)})");

			updateOrMergeSql.Append($" AS t USING {schemaPrefix}[{tempTableName}] AS s ON {joinCondition}");

			if (sqlOptions.InsertIfNotMatched)
			{
				var insertColumns = string.Join(", ", columns.Select(p => $"[{p.NameInDatabase}]"));
				var insertValues = string.Join(", ", columns.Select(p => $"s.[{p.NameInDatabase}]"));

				updateOrMergeSql.Append($" WHEN NOT MATCHED BY t THEN INSERT ({insertColumns}) VALUES ({insertValues})");
			}

			if (sqlOptions.DeleteIfNotMatched)
				updateOrMergeSql.Append(" WHEN NOT MATCHED BY s THEN DELETE");

			updateOrMergeSql.Append($" WHEN MATCHED THEN UPDATE SET {setters}");
		}
		else
		{
			updateOrMergeSql.Append($"UPDATE t SET {setters} FROM {schemaPrefix}[{tableName}] AS t INNER JOIN {schemaPrefix}[{tempTableName}] AS s ON {joinCondition}");
		}

		if (sqlOptions.SkipUnchangedRows)
		{
			var targetColumns = string.Join(", ", columnsToSet.Select(c => c.DataType == "float" ? $"ROUND(t.[{c.NameInDatabase}], {sqlOptions.FloatDecimals}, 1)" : $"t.[{c.NameInDatabase}]"));
			var sourceColumns = string.Join(", ", columnsToSet.Select(c => c.DataType == "float" ? $"ROUND(s.[{c.NameInDatabase}], {sqlOptions.FloatDecimals}, 1)" : $"s.[{c.NameInDatabase}]"));

			updateOrMergeSql.Append($" WHERE NOT EXISTS (SELECT {targetColumns} INTERSECT SELECT {sourceColumns})");
		}

		// Prepare command for deleting the temporary table.
		var deleteTempTableSql = $"DROP TABLE {schemaPrefix}[{tempTableName}]";

		return new UpdateAllCommands
		{
			CreateTempTable = createTempTableSql,
			UpdateOrMerge = updateOrMergeSql.ToString(),
			DeleteTempTable = deleteTempTableSql,
		};
	}

	public virtual bool CanHandle(DbContext dbContext)
	{
		return this.GetUnderlyingConnection(dbContext.Database.Connection) is SqlConnection;
	}

	public virtual DbConnection GetUnderlyingConnection(DbConnection connection)
	{
		return connection is EntityConnection entityConnection
			? this.GetUnderlyingConnection(entityConnection.StoreConnection)
			: connection;
	}

	public virtual DbTransaction GetUnderlyingTransaction(DbContextTransaction transaction)
	{
		return this.GetUnderlyingTransaction(transaction.UnderlyingTransaction);
	}

	public virtual DbTransaction GetUnderlyingTransaction(DbTransaction transaction)
	{
		return transaction is EntityTransaction entityTransaction
			? this.GetUnderlyingTransaction(entityTransaction.StoreTransaction)
			: transaction;
	}

	public virtual QueryInformation GetQueryInformation<T>(ObjectQuery<T> query)
	{
		var queryInfo = new QueryInformation();

		var str = query.ToTraceString();
		var match = FromRegex.Match(str);
		queryInfo.Schema = match.Groups[1].Value;
		queryInfo.Table = match.Groups[2].Value;
		queryInfo.Alias = match.Groups[3].Value;

		var i = str.IndexOf("WHERE", StringComparison.Ordinal);

		if (i > 0)
		{
			var whereClause = str.Substring(i);
			queryInfo.WhereSql = whereClause.Replace(queryInfo.Alias + '.', "");
		}

		return queryInfo;
	}

	private sealed class UpdateAllCommands
	{
		public string CreateTempTable { get; set; } = null!;

		public string UpdateOrMerge { get; set; } = null!;

		public string DeleteTempTable { get; set; } = null!;
	}
}
