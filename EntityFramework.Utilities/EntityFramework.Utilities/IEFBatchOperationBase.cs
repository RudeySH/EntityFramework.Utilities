using System.Linq.Expressions;

namespace EntityFramework.Utilities;

/// <summary>
///     The base interface for all batch operations that EFUtilities provides for Entity Framework.
/// </summary>
public interface IEFBatchOperationBase<TBaseEntity>
	where TBaseEntity : class
{
	/// <summary>
	///     Bulk insert all items if the provider supports it. Otherwise it will use the default insert
	///     unless Configuration.DisableDefaultFallback is set to true in which case it would throw an exception.
	/// </summary>
	/// <param name="items">The items to insert.</param>
	/// <param name="options">The options. For SQL Server databases, use <see cref="SqlInsertAllOptions"/>.</param>
	int InsertAll<TEntity>(
		IEnumerable<TEntity> items, InsertAllOptions? options = null)
		where TEntity : class, TBaseEntity;

	/// <summary>
	///     Bulk insert all items asynchronously if the provider supports it. Otherwise it will use the default insert
	///     unless Configuration.DisableDefaultFallback is set to true in which case it would throw an exception.
	/// </summary>
	/// <param name="items">The items to insert.</param>
	/// <param name="options">The options. For SQL Server databases, use <see cref="SqlInsertAllOptions"/>.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	Task<int> InsertAllAsync<TEntity>(
		IEnumerable<TEntity> items, InsertAllOptions? options = null, CancellationToken cancellationToken = default)
		where TEntity : class, TBaseEntity;

	/// <summary>
	///     Bulk update all items if the provider supports it. Otherwise it will throw an exception.
	/// </summary>
	/// <param name="items">The items to update.</param>
	/// <param name="updateSpecification">Define which columns to update.</param>
	/// <param name="options">The options. For SQL Server databases, use <see cref="SqlUpdateAllOptions"/>.</param>
	int UpdateAll<TEntity>(
		IEnumerable<TEntity> items, Action<UpdateSpecification<TEntity>> updateSpecification,
		UpdateAllOptions? options = null)
		where TEntity : class, TBaseEntity;

	/// <summary>
	///     Bulk update all items asynchronously if the provider supports it. Otherwise it will throw an exception.
	/// </summary>
	/// <param name="items">The items to update.</param>
	/// <param name="updateSpecification">Define which columns to update.</param>
	/// <param name="options">The options. For SQL Server databases, use <see cref="SqlUpdateAllOptions"/>.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	Task<int> UpdateAllAsync<TEntity>(
		IEnumerable<TEntity> items, Action<UpdateSpecification<TEntity>> updateSpecification,
		UpdateAllOptions? options = null, CancellationToken cancellationToken = default)
		where TEntity : class, TBaseEntity;

	/// <summary>
	///    Provides operations that work based on a predicate, such as Update and Delete.
	/// </summary>
	/// <param name="predicate">The predicate.</param>
	/// <returns>An implementation of <see cref="IEFBatchOperationFiltered{TBaseEntity}"/>.</returns>
	IEFBatchOperationFiltered<TBaseEntity> Where(
		Expression<Func<TBaseEntity, bool>> predicate);
}
