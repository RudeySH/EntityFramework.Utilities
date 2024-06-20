using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.Data.Entity.Core.Objects;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace EntityFramework.Utilities
{
	public class SqlQueryProvider : IQueryProvider
	{
		public bool CanDelete => true;

		public bool CanUpdate => true;

		public bool CanInsert => true;

		public bool CanBulkUpdate => true;

		private static readonly Regex FromRegex = new(
			@"FROM \[([^\]]+)\]\.\[([^\]]+)\] AS (\[[^\]]+\])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static readonly Regex UpdateRegex = new(
			@"(\[[^\]]+\])[^=]+=(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		public virtual string GetDeleteQuery(QueryInformation queryInfo)
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
			DbContext dbContext, string schema, string tableName, IReadOnlyList<ColumnMapping> properties,
			IEnumerable<T> items, int? batchSize, SqlBulkCopyOptions sqlBulkCopyOptions)
		{
			var sqlConnection = (SqlConnection)this.GetUnderlyingConnection(dbContext.Database.Connection);
			var sqlTransaction = dbContext.Database.CurrentTransaction != null
				? (SqlTransaction)this.GetUnderlyingTransaction(dbContext.Database.CurrentTransaction)
				: null;

			var itemCollection = items as IReadOnlyCollection<T> ?? items.ToArray();

			using (var reader = new EFDataReader<T>(itemCollection, properties))
			{
				if (sqlConnection.State != ConnectionState.Open)
					sqlConnection.Open();

				using var copy = new SqlBulkCopy(sqlConnection, sqlBulkCopyOptions, sqlTransaction);

				copy.BatchSize = batchSize ?? 4000;
				copy.BulkCopyTimeout = dbContext.Database.CommandTimeout ?? 30;

				if (!string.IsNullOrWhiteSpace(schema))
					copy.DestinationTableName = $"[{schema}].[{tableName}]";
				else
					copy.DestinationTableName = $"[{tableName}]";

				copy.NotifyAfter = 0;

				for (var i = 0; i < reader.FieldCount; i++)
					copy.ColumnMappings.Add(i, properties[i].NameInDatabase);

				copy.WriteToServer(reader);

				copy.Close();
			}

			return itemCollection.Count;
		}

		public virtual async Task<int> InsertItemsAsync<T>(
			DbContext dbContext, string schema, string tableName, IReadOnlyList<ColumnMapping> properties,
			IEnumerable<T> items, int? batchSize, SqlBulkCopyOptions sqlBulkCopyOptions,
			CancellationToken cancellationToken)
		{
			var sqlConnection = (SqlConnection)this.GetUnderlyingConnection(dbContext.Database.Connection);
			var sqlTransaction = dbContext.Database.CurrentTransaction != null
				? (SqlTransaction)this.GetUnderlyingTransaction(dbContext.Database.CurrentTransaction)
				: null;

			var itemCollection = items as IReadOnlyCollection<T> ?? items.ToArray();

			var reader = new EFDataReader<T>(itemCollection, properties);

#if NETSTANDARD2_1_OR_GREATER
			await using (reader.ConfigureAwait(false))
#else
			using (reader)
#endif
			{
				if (sqlConnection.State != ConnectionState.Open)
					await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

				using var copy = new SqlBulkCopy(sqlConnection, sqlBulkCopyOptions, sqlTransaction);

				copy.BatchSize = batchSize ?? 4000;
				copy.BulkCopyTimeout = dbContext.Database.CommandTimeout ?? 30;

				if (!string.IsNullOrWhiteSpace(schema))
					copy.DestinationTableName = $"[{schema}].[{tableName}]";
				else
					copy.DestinationTableName = $"[{tableName}]";

				copy.NotifyAfter = 0;

				for (var i = 0; i < reader.FieldCount; i++)
					copy.ColumnMappings.Add(i, properties[i].NameInDatabase);

				await copy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);

				copy.Close();
			}

			return itemCollection.Count;
		}

		public virtual int UpdateItems<T>(
			DbContext dbContext, string schema, string tableName, IReadOnlyList<ColumnMappingToUpdate> properties,
			IEnumerable<T> items, UpdateSpecification<T> updateSpecification, int? batchSize, bool insertIfNotMatched,
			bool deleteIfNotMatched)
		{
			int rowsAffected;
			var schemaPrefix = !string.IsNullOrWhiteSpace(schema) ? $"[{schema}]." : null;
			var tempTableName = $"#{Guid.NewGuid():N}";
			var columnsToUpdate = new HashSet<string>(updateSpecification.Properties.Select(p => p.GetPropertyName()));

			if (!insertIfNotMatched)
				properties = properties.Where(p => p.IsPrimaryKey || columnsToUpdate.Contains(p.NameOnObject)).ToArray();

			using (var transaction = dbContext.Database.BeginTransaction())
			{
				// Create temporary table.
				var columns = properties.Select(c => $"[{c.NameInDatabase}] {c.DataTypeFull}{(c.DataType.EndsWith("char", StringComparison.Ordinal) ? " COLLATE DATABASE_DEFAULT" : null)}");
				var pkConstraint = string.Join(", ", properties.Where(p => p.IsPrimaryKey).Select(p => $"[{p.NameInDatabase}]"));
				var createCommandText = $"CREATE TABLE {schemaPrefix}[{tempTableName}]({string.Join(", ", columns)}, PRIMARY KEY ({pkConstraint}))";

				dbContext.Database.ExecuteSqlCommand(createCommandText);

				// Insert items into temporary table.
				this.InsertItems(
					dbContext, schema, tempTableName, properties, items, batchSize, SqlBulkCopyOptions.TableLock);

				// Merge or update items in original table.
				var joinCondition = string.Join(" AND ", properties.Where(p => p.IsPrimaryKey).Select(p => $"o.[{p.NameInDatabase}] = t.[{p.NameInDatabase}]"));
				var setters = string.Join(",", properties.Where(p => columnsToUpdate.Contains(p.NameOnObject)).Select(p => $"o.[{p.NameInDatabase}] = t.[{p.NameInDatabase}]"));
				string updateCommandText;

				if (insertIfNotMatched || deleteIfNotMatched)
				{
					updateCommandText = $"MERGE {schemaPrefix}[{tableName}] o USING {schemaPrefix}[{tempTableName}] t ON {joinCondition}"
						+ $" WHEN MATCHED THEN UPDATE SET {setters}";

					if (insertIfNotMatched)
					{
						var insertColumns = string.Join(",", properties.Select(p => $"[{p.NameInDatabase}]"));
						var insertValues = string.Join(",", properties.Select(p => $"t.[{p.NameInDatabase}]"));

						updateCommandText += $" WHEN NOT MATCHED BY o THEN INSERT ({insertColumns}) VALUES ({insertValues})";
					}

					if (deleteIfNotMatched)
						updateCommandText += $" WHEN NOT MATCHED BY t THEN DELETE";
				}
				else
				{
					updateCommandText = $"UPDATE o SET {setters} FROM {schemaPrefix}[{tableName}] o INNER JOIN {schemaPrefix}[{tempTableName}] t ON {joinCondition}";
				}

				rowsAffected = dbContext.Database.ExecuteSqlCommand(updateCommandText);

				// Delete temporary table.
				var deleteCommandText = $"DROP TABLE {schemaPrefix}[{tempTableName}]";

				dbContext.Database.ExecuteSqlCommand(deleteCommandText);

				transaction.Commit();
			}

			return rowsAffected;
		}

		public virtual async Task<int> UpdateItemsAsync<T>(
			DbContext dbContext, string schema, string tableName, IReadOnlyList<ColumnMappingToUpdate> properties,
			IEnumerable<T> items, UpdateSpecification<T> updateSpecification, int? batchSize, bool insertIfNotMatched,
			bool deleteIfNotMatched, CancellationToken cancellationToken)
		{
			int rowsAffected;
			var schemaPrefix = !string.IsNullOrWhiteSpace(schema) ? $"[{schema}]." : null;
			var tempTableName = $"#{Guid.NewGuid():N}";
			var columnsToUpdate = new HashSet<string>(updateSpecification.Properties.Select(p => p.GetPropertyName()));

			if (!insertIfNotMatched)
				properties = properties.Where(p => p.IsPrimaryKey || columnsToUpdate.Contains(p.NameOnObject)).ToArray();

#if NETSTANDARD2_1_OR_GREATER
			var transaction = await dbContext.Database.Connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

			await using (transaction.ConfigureAwait(false))
#else
			using (var transaction = dbContext.Database.Connection.BeginTransaction())
#endif
			{
				// Create temporary table.
				var columns = properties.Select(c => $"[{c.NameInDatabase}] {c.DataTypeFull}{(c.DataType.EndsWith("char") ? " COLLATE DATABASE_DEFAULT" : null)}");
				var pkConstraint = string.Join(", ", properties.Where(p => p.IsPrimaryKey).Select(p => $"[{p.NameInDatabase}]"));
				var createCommandText = $"CREATE TABLE {schemaPrefix}[{tempTableName}]({string.Join(", ", columns)}, PRIMARY KEY ({pkConstraint}))";

				await dbContext.Database.ExecuteSqlCommandAsync(createCommandText, cancellationToken).ConfigureAwait(false);

				// Insert items into temporary table.
				await this.InsertItemsAsync(
					dbContext, schema, tempTableName, properties, items, batchSize, SqlBulkCopyOptions.TableLock,
					cancellationToken)
					.ConfigureAwait(false);

				// Merge or update items in original table.
				var joinCondition = string.Join(" AND ", properties.Where(p => p.IsPrimaryKey).Select(p => $"o.[{p.NameInDatabase}] = t.[{p.NameInDatabase}]"));
				var setters = string.Join(",", properties.Where(p => columnsToUpdate.Contains(p.NameOnObject)).Select(p => $"o.[{p.NameInDatabase}] = t.[{p.NameInDatabase}]"));
				string updateCommandText;

				if (insertIfNotMatched || deleteIfNotMatched)
				{
					updateCommandText = $"MERGE {schemaPrefix}[{tableName}] o USING {schemaPrefix}[{tempTableName}] t ON {joinCondition}"
						+ $" WHEN MATCHED THEN UPDATE SET {setters}";

					if (insertIfNotMatched)
					{
						var insertColumns = string.Join(",", properties.Select(p => $"[{p.NameInDatabase}]"));
						var insertValues = string.Join(",", properties.Select(p => $"t.[{p.NameInDatabase}]"));

						updateCommandText += $" WHEN NOT MATCHED BY o THEN INSERT ({insertColumns}) VALUES ({insertValues})";
					}

					if (deleteIfNotMatched)
						updateCommandText += $" WHEN NOT MATCHED BY t THEN DELETE";
				}
				else
				{
					updateCommandText = $"UPDATE o SET {setters} FROM {schemaPrefix}[{tableName}] o INNER JOIN {schemaPrefix}[{tempTableName}] t ON {joinCondition}";
				}

				rowsAffected = await dbContext.Database.ExecuteSqlCommandAsync(updateCommandText, cancellationToken).ConfigureAwait(false);

				// Delete temporary table.
				var deleteCommandText = $"DROP TABLE {schemaPrefix}[{tempTableName}]";

				await dbContext.Database.ExecuteSqlCommandAsync(deleteCommandText, cancellationToken).ConfigureAwait(false);

#if NETSTANDARD2_1_OR_GREATER
				await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
#else
				transaction.Commit();
#endif
			}

			return rowsAffected;
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
			return transaction.UnderlyingTransaction is EntityTransaction entityTransaction
				? this.GetUnderlyingTransaction(entityTransaction.StoreTransaction)
				: transaction.UnderlyingTransaction;
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
	}
}
