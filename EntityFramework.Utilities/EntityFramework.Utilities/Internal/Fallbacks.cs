using System.Data.Entity;
using System.Linq.Expressions;

namespace EntityFramework.Utilities.Internal;

internal sealed class Fallbacks
{
	internal static int DefaultInsertAll<TEntity>(
		DbContext dbContext, IDbSet<TEntity> dbSet, IEnumerable<TEntity> items)
		where TEntity : class
	{
		foreach (var item in items)
			dbSet.Add(item);

		return dbContext.SaveChanges();
	}

	internal static Task<int> DefaultInsertAllAsync<TEntity>(
		DbContext dbContext, IDbSet<TEntity> dbSet, IEnumerable<TEntity> items, CancellationToken cancellationToken)
		where TEntity : class
	{
		foreach (var item in items)
			dbSet.Add(item);

		return dbContext.SaveChangesAsync(cancellationToken);
	}

	internal static int DefaultDelete<TEntity>(
		DbContext dbContext, IDbSet<TEntity> dbSet, Expression<Func<TEntity, bool>> predicate)
		where TEntity : class
	{
		foreach (var item in dbSet.Where(predicate))
			dbSet.Add(item);

		return dbContext.SaveChanges();
	}

	internal static Task<int> DefaultDeleteAsync<TEntity>(
		DbContext dbContext, IDbSet<TEntity> dbSet, Expression<Func<TEntity, bool>> predicate,
		CancellationToken cancellationToken)
		where TEntity : class
	{
		foreach (var item in dbSet.Where(predicate))
			dbSet.Remove(item);

		return dbContext.SaveChangesAsync(cancellationToken);
	}

	internal static int DefaultUpdate<TEntity, TP>(
		DbContext dbContext, IDbSet<TEntity> dbSet, Expression<Func<TEntity, bool>> predicate,
		Expression<Func<TEntity, TP>> prop, Expression<Func<TEntity, TP>> modifier)
		where TEntity : class
	{
		var setter = ExpressionHelper.PropertyExpressionToSetter(prop);
		var compiledModifer = modifier.Compile();

		foreach (var item in dbSet.Where(predicate))
			setter(item, compiledModifer(item));

		return dbContext.SaveChanges();
	}

	internal static Task<int> DefaultUpdateAsync<TEntity, TP>(
		DbContext dbContext, IDbSet<TEntity> dbSet, Expression<Func<TEntity, bool>> predicate,
		Expression<Func<TEntity, TP>> prop, Expression<Func<TEntity, TP>> modifier,
		CancellationToken cancellationToken)
		where TEntity : class
	{
		var setter = ExpressionHelper.PropertyExpressionToSetter(prop);
		var compiledModifer = modifier.Compile();

		foreach (var item in dbSet.Where(predicate))
			setter(item, compiledModifer(item));

		return dbContext.SaveChangesAsync(cancellationToken);
	}
}
