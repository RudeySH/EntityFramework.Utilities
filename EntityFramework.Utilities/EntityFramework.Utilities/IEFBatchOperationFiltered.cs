using System.Data.Entity;
using System.Linq.Expressions;

namespace EntityFramework.Utilities;

public interface IEFBatchOperationFiltered<T>
{
	int Delete(
		TransactionalBehavior? transactionalBehavior = null);

	Task<int> DeleteAsync(
		TransactionalBehavior? transactionalBehavior = null, CancellationToken cancellationToken = default);

	int DeleteTop(
		int numberOfRows, TransactionalBehavior? transactionalBehavior = null);

	Task<int> DeleteTopAsync(
		int numberOfRows, TransactionalBehavior? transactionalBehavior = null,
		CancellationToken cancellationToken = default);

	int DeleteTopPercent(
		double numberOfRows, TransactionalBehavior? transactionalBehavior = null);

	Task<int> DeleteTopPercentAsync(
		double numberOfRows, TransactionalBehavior? transactionalBehavior = null,
		CancellationToken cancellationToken = default);

	int Update<TP>(
		Expression<Func<T, TP>> prop, Expression<Func<T, TP>> modifier,
		TransactionalBehavior? transactionalBehavior = null);

	Task<int> UpdateAsync<TP>(
		Expression<Func<T, TP>> prop, Expression<Func<T, TP>> modifier,
		TransactionalBehavior? transactionalBehavior = null, CancellationToken cancellationToken = default);
}
