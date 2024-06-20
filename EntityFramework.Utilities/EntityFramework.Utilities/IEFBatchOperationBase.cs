using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace EntityFramework.Utilities
{
	public interface IEFBatchOperationBase<TBaseEntity>
		where TBaseEntity : class
	{
		/// <summary>
		/// Bulk insert all items if the Provider supports it. Otherwise it will use the default insert
		/// unless Configuration.DisableDefaultFallback is set to true in which case it would throw an exception.
		/// </summary>
		/// <param name="items">The items to insert.</param>
		/// <param name="batchSize">
		/// The size of each batch. Default depends on the provider. The SqlQueryProvider uses 4000 as default.
		/// </param>
		/// <param name="sqlBulkCopyOptions">The options for SqlBulkCopy which is used by the SqlQueryProvider.</param>
		int InsertAll<TEntity>(
			IEnumerable<TEntity> items, int? batchSize = null, SqlBulkCopyOptions sqlBulkCopyOptions = default)
			where TEntity : class, TBaseEntity;

		/// <summary>
		/// Bulk insert all items asynchronously if the Provider supports it. Otherwise it will use the default insert
		/// unless Configuration.DisableDefaultFallback is set to true in which case it would throw an exception.
		/// </summary>
		/// <param name="items">The items to insert.</param>
		/// <param name="batchSize">
		/// The size of each batch. Default depends on the provider. The SqlQueryProvider uses 4000 as default.
		/// </param>
		/// <param name="sqlBulkCopyOptions">The options for SqlBulkCopy which is used by the SqlQueryProvider.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<int> InsertAllAsync<TEntity>(
			IEnumerable<TEntity> items, int? batchSize = null, SqlBulkCopyOptions sqlBulkCopyOptions = default,
			CancellationToken cancellationToken = default)
			where TEntity : class, TBaseEntity;

		/// <summary>
		/// Bulk update all items if the Provider supports it. Otherwise it will throw an exception.
		/// </summary>
		/// <param name="items">The items to update.</param>
		/// <param name="updateSpecification">Define which columns to update.</param>
		/// <param name="insertIfNotMatched">
		/// A boolean indicating whether or not to insert records that are in <paramref name="items"/> but not yet in
		/// the database.
		/// </param>
		/// <param name="deleteIfNotMatched">
		/// A boolean indicating whether or not to delete database records that are not in <paramref name="items"/>.
		/// </param>
		/// <param name="batchSize">
		/// The size of each batch. Default depends on the provider. The SqlQueryProvider uses 4000 as default.
		/// </param>
		int UpdateAll<TEntity>(
			IEnumerable<TEntity> items, Action<UpdateSpecification<TEntity>> updateSpecification, int? batchSize = null,
			bool insertIfNotMatched = false, bool deleteIfNotMatched = false)
			where TEntity : class, TBaseEntity;

		/// <summary>
		/// Bulk update all items asynchronously if the Provider supports it. Otherwise it will throw an exception.
		/// </summary>
		/// <param name="items">The items to update.</param>
		/// <param name="updateSpecification">Define which columns to update.</param>
		/// <param name="insertIfNotMatched">
		/// A boolean indicating whether or not to insert records that are in <paramref name="items"/> but not yet in
		/// the database.
		/// </param>
		/// <param name="deleteIfNotMatched">
		/// A boolean indicating whether or not to delete database records that are not in <paramref name="items"/>.
		/// </param>
		/// <param name="batchSize">
		/// The size of each batch. Default depends on the provider. The SqlQueryProvider uses 4000 as default.
		/// </param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<int> UpdateAllAsync<TEntity>(
			IEnumerable<TEntity> items, Action<UpdateSpecification<TEntity>> updateSpecification, int? batchSize = null,
			bool insertIfNotMatched = false, bool deleteIfNotMatched = false,
			CancellationToken cancellationToken = default)
			where TEntity : class, TBaseEntity;

		IEFBatchOperationFiltered<TBaseEntity> Where(
			Expression<Func<TBaseEntity, bool>> predicate);
	}
}
