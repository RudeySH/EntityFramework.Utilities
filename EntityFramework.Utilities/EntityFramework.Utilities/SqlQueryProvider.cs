using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;

namespace EntityFramework.Utilities
{
	public class SqlQueryProvider : IQueryProvider, INoOpAnalyzer
	{
		public bool CanDelete => true;
		public bool CanUpdate => true;
		public bool CanInsert => true;
		public bool CanBulkUpdate => true;

		private static readonly Regex FromRegex = new Regex(@"FROM \[([^\]]+)\]\.\[([^\]]+)\] AS (\[[^\]]+\])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex UpdateRegex = new Regex(@"(\[[^\]]+\])[^=]+=(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <inheritdoc/>
		public bool QueryIsNoOp(QueryInformation queryInformation) =>
			string.IsNullOrEmpty(queryInformation.Schema) &&
			string.IsNullOrEmpty(queryInformation.Table) &&
			queryInformation.WhereSql.Contains("WHERE 1 = 0");

		public string GetDeleteQuery(QueryInformation queryInfo)
		{
			return $"DELETE {queryInfo.TopExpression} FROM [{queryInfo.Schema}].[{queryInfo.Table}] {queryInfo.WhereSql}";
		}

		public string GetUpdateQuery(QueryInformation predicateQueryInfo, QueryInformation modificationQueryInfo)
		{
			var msql = modificationQueryInfo.WhereSql.Replace("WHERE ", "");
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
				updateSql = string.Join(" = ", update.Split(new[] { " = " }, StringSplitOptions.RemoveEmptyEntries).Reverse());
			}

			return
				$"UPDATE [{predicateQueryInfo.Schema}].[{predicateQueryInfo.Table}] SET {updateSql} {predicateQueryInfo.WhereSql}";
		}

		public void InsertItems<T>(
			IEnumerable<T> items, string schema, string tableName, IReadOnlyList<ColumnMapping> properties,
			DbConnection storeConnection, int? batchSize, int? executeTimeout, SqlBulkCopyOptions copyOptions,
			DbTransaction transaction)
		{
			using (var reader = new EFDataReader<T>(items, properties))
			{
				if (storeConnection.State != System.Data.ConnectionState.Open)
					storeConnection.Open();

				using (var copy = new SqlBulkCopy((SqlConnection)storeConnection, copyOptions, transaction as SqlTransaction))
				{
					copy.BulkCopyTimeout = executeTimeout ?? 600;
					copy.BatchSize = batchSize ?? 15000; //default batch size

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
			}
		}

		public void UpdateItems<T>(
			IEnumerable<T> items, string schema, string tableName, IReadOnlyList<ColumnMapping> properties,
			DbConnection storeConnection, int? batchSize, UpdateSpecification<T> updateSpecification,
			int? executeTimeout, SqlBulkCopyOptions copyOptions, DbTransaction transaction,
			DbConnection insertConnection, bool insertIfNotMatched, bool deleteIfNotMatched)
		{
			var tempTableName = $"#{Guid.NewGuid():N}";
			var columnsToUpdate = new HashSet<string>(updateSpecification.Properties.Select(p => p.GetPropertyName()));

			if (!insertIfNotMatched)
				properties = properties.Where(p => p.IsPrimaryKey || columnsToUpdate.Contains(p.NameOnObject)).ToArray();

			if (storeConnection.State != System.Data.ConnectionState.Open)
				storeConnection.Open();

			using (var createCommand = storeConnection.CreateCommand())
			{
				var columns = properties.Select(c => $"[{c.NameInDatabase}] {c.DataTypeFull}{(c.DataType.EndsWith("char") ? " COLLATE DATABASE_DEFAULT" : null)}");
				var pkConstraint = string.Join(", ", properties.Where(p => p.IsPrimaryKey).Select(p => $"[{p.NameInDatabase}]"));

				createCommand.CommandText = $"CREATE TABLE [{schema}].[{tempTableName}]({string.Join(", ", columns)}, PRIMARY KEY ({pkConstraint}))";
				createCommand.CommandTimeout = executeTimeout ?? 600;
				createCommand.Transaction = transaction;

				createCommand.ExecuteNonQuery();
			}

			this.InsertItems(items, schema, tempTableName, properties, insertConnection, batchSize, executeTimeout, copyOptions, transaction);

			using (var updateCommand = storeConnection.CreateCommand())
			{
				var joinCondition = string.Join(" AND ", properties.Where(p => p.IsPrimaryKey).Select(p => $"o.[{p.NameInDatabase}] = t.[{p.NameInDatabase}]"));
				var setters = string.Join(",", properties.Where(p => columnsToUpdate.Contains(p.NameOnObject)).Select(p => $"o.[{p.NameInDatabase}] = t.[{p.NameInDatabase}]"));

				if (insertIfNotMatched || deleteIfNotMatched)
				{
					updateCommand.CommandText = $"MERGE [{schema}].[{tableName}] o USING [{schema}].[{tempTableName}] t ON {joinCondition}"
						+ $" WHEN MATCHED THEN UPDATE SET {setters}";

					if (insertIfNotMatched)
					{
						var columns = string.Join(",", properties.Select(p => $"[{p.NameInDatabase}]"));
						var values = string.Join(",", properties.Select(p => $"t.[{p.NameInDatabase}]"));

						updateCommand.CommandText += $" WHEN NOT MATCHED BY o THEN INSERT ({columns}) VALUES ({values})";
					}

					if (deleteIfNotMatched)
						updateCommand.CommandText += $" WHEN NOT MATCHED BY t THEN DELETE";
				}
				else
				{
					updateCommand.CommandText = $"UPDATE o SET {setters} FROM [{schema}].[{tableName}] o INNER JOIN [{schema}].[{tempTableName}] t ON {joinCondition}";
				}

				updateCommand.CommandTimeout = executeTimeout ?? 600;
				updateCommand.Transaction = transaction;

				updateCommand.ExecuteNonQuery();
			}

			using (var deleteCommand = storeConnection.CreateCommand())
			{
				deleteCommand.CommandText = $"DROP TABLE [{schema}].[{tempTableName}]";
				deleteCommand.CommandTimeout = executeTimeout ?? 600;
				deleteCommand.Transaction = transaction;

				deleteCommand.ExecuteNonQuery();
			}
		}

		public bool CanHandle(DbConnection storeConnection)
		{
			return storeConnection is SqlConnection;
		}

		public QueryInformation GetQueryInformation<T>(System.Data.Entity.Core.Objects.ObjectQuery<T> query)
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
