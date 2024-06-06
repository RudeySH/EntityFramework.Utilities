using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq.Expressions;

namespace EntityFramework.Utilities
{
	public interface IEFBatchOperationBase<T> where T : class
	{
		/// <summary>
		/// Bulk insert all items if the Provider supports it. Otherwise it will use the default insert unless
		/// Configuration.DisableDefaultFallback is set to true in which case it would throw an exception.
		/// </summary>
		/// <param name="items">The items to insert</param>
		/// <param name="connection">
		/// The DbConnection to use for the insert. Only needed when for example a profiler wraps the connection.
		/// Then you need to provide a connection of the type the provider use.
		/// </param>
		/// <param name="batchSize">
		/// The size of each batch. Default depends on the provider. SqlProvider uses 15000 as default.
		/// </param>
		void InsertAll<TEntity>(
			IEnumerable<TEntity> items, DbConnection connection = null, int? batchSize = null,
			int? executeTimeout = null, SqlBulkCopyOptions copyOptions = SqlBulkCopyOptions.Default,
			DbTransaction transaction = null)
			where TEntity : class, T;

		/// <summary>
		/// Bulk update all items if the Provider supports it. Otherwise it will throw an exception.
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="items">The items to update</param>
		/// <param name="updateSpecification">Define which columns to update</param>
		/// <param name="connection">
		/// The DbConnection to use for the insert. Only needed when for example a profiler wraps the connection.
		/// Then you need to provide a connection of the type the provider use.
		/// </param>
		/// <param name="batchSize">
		/// The size of each batch. Default depends on the provider. SqlProvider uses 15000 as default.
		/// </param>
		void UpdateAll<TEntity>(
			IEnumerable<TEntity> items, Action<UpdateSpecification<TEntity>> updateSpecification,
			DbConnection connection = null, int? batchSize = null, int? executeTimeout = null,
			SqlBulkCopyOptions copyOptions = SqlBulkCopyOptions.Default, DbTransaction transaction = null)
			where TEntity : class, T;

		IEFBatchOperationFiltered<T> Where(Expression<Func<T, bool>> predicate);
	}
}
