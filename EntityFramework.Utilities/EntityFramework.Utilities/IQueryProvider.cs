using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;

namespace EntityFramework.Utilities
{
	public interface IQueryProvider
	{
		bool CanDelete { get; }
		bool CanUpdate { get; }
		bool CanInsert { get; }
		bool CanBulkUpdate { get; }

		string GetDeleteQuery(QueryInformation queryInformation);

		string GetUpdateQuery(QueryInformation predicateQueryInfo, QueryInformation modificationQueryInfo);

		void InsertItems<T>(
			IEnumerable<T> items, string schema, string tableName, IReadOnlyList<ColumnMapping> properties,
			DbConnection storeConnection, int? batchSize, int? executeTimeout, SqlBulkCopyOptions copyOptions,
			DbTransaction transaction);

		void UpdateItems<T>(
			IEnumerable<T> items, string schema, string tableName, IReadOnlyList<ColumnMapping> properties,
			DbConnection storeConnection, int? batchSize, UpdateSpecification<T> updateSpecification,
			int? executeTimeout, SqlBulkCopyOptions copyOptions, DbTransaction transaction,
			DbConnection insertConnection, bool insertIfNotMatched, bool deleteIfNotMatched);

		bool CanHandle(DbConnection storeConnection);

		QueryInformation GetQueryInformation<T>(System.Data.Entity.Core.Objects.ObjectQuery<T> query);
	}
}
