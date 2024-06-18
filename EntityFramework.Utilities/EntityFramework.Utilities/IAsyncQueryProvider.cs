using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace EntityFramework.Utilities
{
	/// <summary>
	///     An extension of <see cref="IQueryProvider"/> that offers asynchronous versions of database interactions.
	/// </summary>
	public interface IAsyncQueryProvider : IQueryProvider
	{
		Task InsertItemsAsync<T>(IEnumerable<T> items, string schema, string tableName, IList<ColumnMapping> properties, DbConnection storeConnection, int? batchSize, int? executeTimeout, SqlBulkCopyOptions copyOptions, DbTransaction transaction);
		Task UpdateItemsAsync<T>(IEnumerable<T> items, string schema, string tableName, IList<ColumnMapping> properties, DbConnection storeConnection, int? batchSize, UpdateSpecification<T> updateSpecification, int? executeTimeout, SqlBulkCopyOptions copyOptions, DbTransaction transaction, DbConnection insertConnection);
	}
}
