using System.Data.Entity;
using System.Data.Entity.Core.Objects;

namespace EntityFramework.Utilities;

public interface IQueryProvider
{
	bool CanDelete { get; }

	bool CanUpdate { get; }

	bool CanInsert { get; }

	bool CanBulkUpdate { get; }

	string GetDeleteQuery(QueryInformation queryInfo);

	string GetUpdateQuery(QueryInformation predicateQueryInfo, QueryInformation modificationQueryInfo);

	int InsertItems<T>(
		DbContext dbContext, string schema, string tableName, IReadOnlyList<ColumnMapping> properties,
		IEnumerable<T> items, InsertAllOptions? options);

	Task<int> InsertItemsAsync<T>(
		DbContext dbContext, string schema, string tableName, IReadOnlyList<ColumnMapping> properties,
		IEnumerable<T> items, InsertAllOptions? options, CancellationToken cancellationToken);

	int UpdateItems<T>(
		DbContext dbContext, string schema, string tableName, IReadOnlyList<ColumnMappingToUpdate> properties,
		IEnumerable<T> items, UpdateSpecification<T> updateSpecification, UpdateAllOptions? options);

	Task<int> UpdateItemsAsync<T>(
		DbContext dbContext, string schema, string tableName, IReadOnlyList<ColumnMappingToUpdate> properties,
		IEnumerable<T> items, UpdateSpecification<T> updateSpecification, UpdateAllOptions? options,
		CancellationToken cancellationToken);

	bool CanHandle(DbContext dbContext);

	QueryInformation GetQueryInformation<T>(ObjectQuery<T> query);
}
