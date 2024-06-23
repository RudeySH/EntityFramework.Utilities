using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq.Expressions;

namespace EntityFramework.Utilities.Internal;

internal sealed class EFBatchOperationImplementation<TContext, TBaseEntity>
	: IEFBatchOperationBase<TBaseEntity>, IEFBatchOperationFiltered<TBaseEntity>
	where TContext : DbContext
	where TBaseEntity : class
{
	private readonly TContext dbContext;
	private readonly IDbSet<TBaseEntity> dbSet;
	private readonly ObjectContext objectContext;
	private readonly Expression<Func<TBaseEntity, bool>>? predicate;

	public EFBatchOperationImplementation(
		TContext dbContext, IDbSet<TBaseEntity> dbSet)
	{
		this.dbContext = dbContext;
		this.dbSet = dbSet;
		objectContext = ((IObjectContextAdapter)dbContext).ObjectContext;
	}

	public EFBatchOperationImplementation(
		TContext dbContext, IDbSet<TBaseEntity> dbSet, Expression<Func<TBaseEntity, bool>> predicate)
	{
		this.dbContext = dbContext;
		this.dbSet = dbSet;
		objectContext = ((IObjectContextAdapter)dbContext).ObjectContext;
		this.predicate = predicate;
	}

	public int InsertAll<TEntity>(
		IEnumerable<TEntity> items, InsertAllOptions? options = null)
		where TEntity : class, TBaseEntity
	{
		var provider = Configuration.Providers.FirstOrDefault(p => p.CanInsert && p.CanHandle(dbContext));

		if (provider == null)
		{
			if (Configuration.DisableDefaultFallback)
				throw new InvalidOperationException("No provider supporting the InsertAll operation was found.");

			return Fallbacks.DefaultInsertAll(dbContext, dbSet, items);
		}

		var mapping = EFMappingFactory.GetMappingsForContext(objectContext);
		var typeMapping = mapping.TypeMappings[typeof(TBaseEntity)];
		var tableMapping = typeMapping.TableMappings.First();
		var currentType = typeof(TEntity);

		var properties = tableMapping.PropertyMappings
			.Where(p => currentType.IsSubclassOf(p.ForEntityType) || p.ForEntityType == currentType)
			.Where(p => p.IsComputed == false)
			.Select(p => new ColumnMapping { NameInDatabase = p.ColumnName, NameOnObject = p.PropertyName })
			.ToList();

		if (tableMapping.TphConfiguration != null)
		{
			properties.Add(new ColumnMapping
			{
				NameOnObject = "",
				NameInDatabase = tableMapping.TphConfiguration.ColumnName,
				StaticValue = tableMapping.TphConfiguration.Mappings[typeof(TEntity)],
			});
		}

		return provider.InsertItems(
			dbContext, tableMapping.Schema, tableMapping.TableName, properties, items, options);
	}

	public Task<int> InsertAllAsync<TEntity>(
		IEnumerable<TEntity> items, InsertAllOptions? options = null, CancellationToken cancellationToken = default)
		where TEntity : class, TBaseEntity
	{
		var provider = Configuration.Providers.FirstOrDefault(p => p.CanInsert && p.CanHandle(dbContext));

		if (provider == null)
		{
			if (Configuration.DisableDefaultFallback)
				throw new InvalidOperationException("No provider supporting the InsertAll operation was found.");

			return Fallbacks.DefaultInsertAllAsync(dbContext, dbSet, items, cancellationToken);
		}

		var mapping = EFMappingFactory.GetMappingsForContext(objectContext);
		var typeMapping = mapping.TypeMappings[typeof(TBaseEntity)];
		var tableMapping = typeMapping.TableMappings.First();
		var currentType = typeof(TEntity);

		var properties = tableMapping.PropertyMappings
			.Where(p => currentType.IsSubclassOf(p.ForEntityType) || p.ForEntityType == currentType)
			.Where(p => p.IsComputed == false)
			.Select(p => new ColumnMapping { NameInDatabase = p.ColumnName, NameOnObject = p.PropertyName })
			.ToList();

		if (tableMapping.TphConfiguration != null)
		{
			properties.Add(new ColumnMapping
			{
				NameOnObject = "",
				NameInDatabase = tableMapping.TphConfiguration.ColumnName,
				StaticValue = tableMapping.TphConfiguration.Mappings[typeof(TEntity)],
			});
		}

		return provider.InsertItemsAsync(
			dbContext, tableMapping.Schema, tableMapping.TableName, properties, items, options,
			cancellationToken);
	}

	public int UpdateAll<TEntity>(
		IEnumerable<TEntity> items, Action<UpdateSpecification<TEntity>> updateSpecification,
		UpdateAllOptions? options = null)
		where TEntity : class, TBaseEntity
	{
		var provider = Configuration.Providers.FirstOrDefault(p => p.CanBulkUpdate && p.CanHandle(dbContext))
			?? throw new InvalidOperationException("No provider supporting the UpdateAll operation was found.");

		var mapping = EFMappingFactory.GetMappingsForContext(objectContext);
		var typeMapping = mapping.TypeMappings[typeof(TBaseEntity)];
		var tableMapping = typeMapping.TableMappings.First();
		var currentType = typeof(TEntity);

		var properties = tableMapping.PropertyMappings
			.Where(p => currentType.IsSubclassOf(p.ForEntityType) || p.ForEntityType == currentType)
			.Where(p => p.IsComputed == false)
			.Select(p => new ColumnMappingToUpdate
			{
				NameOnObject = p.PropertyName,
				NameInDatabase = p.ColumnName,
				DataType = p.DataType,
				DataTypeFull = p.DataTypeFull,
				IsPrimaryKey = p.IsPrimaryKey,
			})
			.ToList();

		var spec = new UpdateSpecification<TEntity>();
		updateSpecification(spec);

		return provider.UpdateItems(
			dbContext, tableMapping.Schema, tableMapping.TableName, properties, items, spec, options);
	}

	public Task<int> UpdateAllAsync<TEntity>(
		IEnumerable<TEntity> items, Action<UpdateSpecification<TEntity>> updateSpecification,
		UpdateAllOptions? options = null, CancellationToken cancellationToken = default)
		where TEntity : class, TBaseEntity
	{
		var provider = Configuration.Providers.FirstOrDefault(p => p.CanBulkUpdate && p.CanHandle(dbContext))
			?? throw new InvalidOperationException("No provider supporting the UpdateAll operation was found");

		var mapping = EFMappingFactory.GetMappingsForContext(objectContext);
		var typeMapping = mapping.TypeMappings[typeof(TBaseEntity)];
		var tableMapping = typeMapping.TableMappings.First();
		var currentType = typeof(TEntity);

		var properties = tableMapping.PropertyMappings
			.Where(p => p.ForEntityType == currentType || currentType.IsSubclassOf(p.ForEntityType))
			.Where(p => p.IsComputed == false)
			.Select(p => new ColumnMappingToUpdate
			{
				NameOnObject = p.PropertyName,
				NameInDatabase = p.ColumnName,
				DataType = p.DataType,
				DataTypeFull = p.DataTypeFull,
				IsPrimaryKey = p.IsPrimaryKey,
			})
			.ToList();

		var spec = new UpdateSpecification<TEntity>();
		updateSpecification(spec);

		return provider.UpdateItemsAsync(
			dbContext, tableMapping.Schema, tableMapping.TableName, properties, items, spec, options,
			cancellationToken);
	}

	public IEFBatchOperationFiltered<TBaseEntity> Where(
		Expression<Func<TBaseEntity, bool>> predicate)
	{
		return new EFBatchOperationImplementation<TContext, TBaseEntity>(dbContext, dbSet, predicate);
	}

	public int DeleteTop(
		int numberOfRows, TransactionalBehavior? transactionalBehavior = null)
	{
		var topExpression = $"TOP ({numberOfRows})";

		return Delete(transactionalBehavior, topExpression);
	}

	public Task<int> DeleteTopAsync(
		int numberOfRows, TransactionalBehavior? transactionalBehavior = null,
		CancellationToken cancellationToken = default)
	{
		var topExpression = $"TOP ({numberOfRows})";

		return DeleteAsync(transactionalBehavior, topExpression, cancellationToken);
	}

	public int DeleteTopPercent(
		double percentOfRows, TransactionalBehavior? transactionalBehavior = null)
	{
		var topExpression = $"TOP ({percentOfRows.ToString(CultureInfo.InvariantCulture)}) PERCENT";

		return Delete(transactionalBehavior, topExpression);
	}

	public Task<int> DeleteTopPercentAsync(
		double percentOfRows, TransactionalBehavior? transactionalBehavior = null,
		CancellationToken cancellationToken = default)
	{
		var topExpression = $"TOP ({percentOfRows.ToString(CultureInfo.InvariantCulture)}) PERCENT";

		return DeleteAsync(transactionalBehavior, topExpression, cancellationToken);
	}

	public int Delete(
		TransactionalBehavior? transactionalBehavior = null)
	{
		return Delete(transactionalBehavior, topExpression: null);
	}

	public Task<int> DeleteAsync(
		TransactionalBehavior? transactionalBehavior = null, CancellationToken cancellationToken = default)
	{
		return DeleteAsync(transactionalBehavior, topExpression: null, cancellationToken);
	}

	private int Delete(
		TransactionalBehavior? transactionalBehavior, string? topExpression)
	{
		var provider = Configuration.Providers.FirstOrDefault(p => p.CanDelete && p.CanHandle(dbContext));

		if (provider == null)
		{
			if (Configuration.DisableDefaultFallback)
				throw new InvalidOperationException("No provider supporting the Delete operation was found.");

			return Fallbacks.DefaultDelete(dbContext, dbSet, predicate!);
		}

		var set = objectContext.CreateObjectSet<TBaseEntity>();
		var query = (ObjectQuery<TBaseEntity>)set.Where(predicate);
		var queryInformation = provider.GetQueryInformation(query);
		queryInformation.TopExpression = topExpression;

		// If Entity Framework has optimized the query down to a query that queries against nothing and returns
		// nothing, then abort as there is no way to somehow translate it to a delete query.
		if (provider is INoOpAnalyzer noOpAnalyzer && noOpAnalyzer.QueryIsNoOp(queryInformation))
			return 0;

		var delete = provider.GetDeleteQuery(queryInformation);
		var parameters = query.Parameters.Select(p => new SqlParameter { Value = p.Value, ParameterName = p.Name }).ToArray<object>();

		if (transactionalBehavior != null)
			return objectContext.ExecuteStoreCommand(transactionalBehavior.Value, delete, parameters);
		else
			return objectContext.ExecuteStoreCommand(delete, parameters);
	}

	private Task<int> DeleteAsync(
		TransactionalBehavior? transactionalBehavior, string? topExpression, CancellationToken cancellationToken)
	{
		var provider = Configuration.Providers.FirstOrDefault(p => p.CanDelete && p.CanHandle(dbContext));

		if (provider == null)
		{
			if (Configuration.DisableDefaultFallback)
				throw new InvalidOperationException("No provider supporting the Delete operation was found.");

			return Fallbacks.DefaultDeleteAsync(dbContext, dbSet, predicate!, cancellationToken);
		}

		var set = objectContext.CreateObjectSet<TBaseEntity>();
		var query = (ObjectQuery<TBaseEntity>)set.Where(predicate);
		var queryInformation = provider.GetQueryInformation(query);
		queryInformation.TopExpression = topExpression;

		var delete = provider.GetDeleteQuery(queryInformation);
		var parameters = query.Parameters.Select(p => new SqlParameter { Value = p.Value, ParameterName = p.Name }).ToArray<object>();

		if (transactionalBehavior != null)
			return objectContext.ExecuteStoreCommandAsync(transactionalBehavior.Value, delete, cancellationToken, parameters);
		else
			return objectContext.ExecuteStoreCommandAsync(delete, cancellationToken, parameters);
	}

	public int Update<TP>(
		Expression<Func<TBaseEntity, TP>> prop, Expression<Func<TBaseEntity, TP>> modifier,
		TransactionalBehavior? transactionalBehavior = null)
	{
		var provider = Configuration.Providers.FirstOrDefault(p => p.CanUpdate && p.CanHandle(dbContext));

		if (provider == null)
		{
			if (Configuration.DisableDefaultFallback)
				throw new InvalidOperationException("No provider supporting the Update operation was found.");

			return Fallbacks.DefaultUpdate(dbContext, dbSet, predicate!, prop, modifier);
		}

		var set = objectContext.CreateObjectSet<TBaseEntity>();

		var query = (ObjectQuery<TBaseEntity>)set.Where(predicate);
		var queryInformation = provider.GetQueryInformation(query);

		var updateExpression = ExpressionHelper.CombineExpressions(prop, modifier);

		var mquery = (ObjectQuery<TBaseEntity>)objectContext.CreateObjectSet<TBaseEntity>().Where(updateExpression);
		var mqueryInfo = provider.GetQueryInformation(mquery);

		// If Entity Framework has optimized either query down to a query that queries against nothing and returns
		// nothing, then abort as there is no way to somehow translate it to an update query.
		if (provider is INoOpAnalyzer noOpAnalyzer && (
				noOpAnalyzer.QueryIsNoOp(queryInformation) ||
				noOpAnalyzer.QueryIsNoOp(mqueryInfo)))
		{
			return 0;
		}

		var update = provider.GetUpdateQuery(queryInformation, mqueryInfo);

		var parameters = query.Parameters
			.Concat(mquery.Parameters)
			.Select(p => new SqlParameter { Value = p.Value, ParameterName = p.Name })
			.ToArray<object>();

		if (transactionalBehavior != null)
			return objectContext.ExecuteStoreCommand(transactionalBehavior.Value, update, parameters);
		else
			return objectContext.ExecuteStoreCommand(update, parameters);
	}

	public Task<int> UpdateAsync<TP>(
		Expression<Func<TBaseEntity, TP>> prop, Expression<Func<TBaseEntity, TP>> modifier,
		TransactionalBehavior? transactionalBehavior = null, CancellationToken cancellationToken = default)
	{
		var provider = Configuration.Providers.FirstOrDefault(p => p.CanUpdate && p.CanHandle(dbContext));

		if (provider == null)
		{
			if (Configuration.DisableDefaultFallback)
				throw new InvalidOperationException("No provider supporting the Update operation was found.");

			return Fallbacks.DefaultUpdateAsync(
				dbContext, dbSet, predicate!, prop, modifier, cancellationToken);
		}

		var set = objectContext.CreateObjectSet<TBaseEntity>();

		var query = (ObjectQuery<TBaseEntity>)set.Where(predicate);
		var queryInformation = provider.GetQueryInformation(query);

		var updateExpression = ExpressionHelper.CombineExpressions(prop, modifier);

		var mquery = (ObjectQuery<TBaseEntity>)objectContext.CreateObjectSet<TBaseEntity>().Where(updateExpression);
		var mqueryInfo = provider.GetQueryInformation(mquery);

		var update = provider.GetUpdateQuery(queryInformation, mqueryInfo);

		var parameters = query.Parameters
			.Concat(mquery.Parameters)
			.Select(p => new SqlParameter { Value = p.Value, ParameterName = p.Name })
			.ToArray<object>();

		if (transactionalBehavior != null)
			return objectContext.ExecuteStoreCommandAsync(transactionalBehavior.Value, update, cancellationToken, parameters);
		else
			return objectContext.ExecuteStoreCommandAsync(update, cancellationToken, parameters);
	}
}
