using EntityFramework.Utilities.Internal;
using System.Data.Entity;

namespace EntityFramework.Utilities;

public static class EFBatchOperation
{
	public static IEFBatchOperationBase<TBaseEntity> For<TContext, TBaseEntity>(
		TContext dbContext, IDbSet<TBaseEntity> dbSet)
		where TContext : DbContext
		where TBaseEntity : class
	{
		return new EFBatchOperationImplementation<TContext, TBaseEntity>(dbContext, dbSet);
	}
}
