using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq.Expressions;

namespace EntityFramework.Utilities
{
	public static class EFBatchOperation
	{
		public static IEFBatchOperationBase<TBaseEntity> For<TContext, TBaseEntity>(
			TContext dbContext, IDbSet<TBaseEntity> dbSet)
			where TContext : DbContext
			where TBaseEntity : class
		{
			return new EFBatchOperation<TContext, TBaseEntity>(dbContext, dbSet);
		}
	}

	internal sealed class EFBatchOperation<TContext, TBaseEntity>
		: IEFBatchOperationBase<TBaseEntity>, IEFBatchOperationFiltered<TBaseEntity>
		where TContext : DbContext
		where TBaseEntity : class
	{
		private readonly TContext dbContext;
		private readonly IDbSet<TBaseEntity> dbSet;
		private readonly ObjectContext objectContext;
		private readonly Expression<Func<TBaseEntity, bool>>? predicate;

		public EFBatchOperation(
			TContext dbContext, IDbSet<TBaseEntity> dbSet)
		{
			this.dbContext = dbContext;
			this.dbSet = dbSet;
			this.objectContext = ((IObjectContextAdapter)dbContext).ObjectContext;
		}

		public EFBatchOperation(
			TContext dbContext, IDbSet<TBaseEntity> dbSet, Expression<Func<TBaseEntity, bool>> predicate)
		{
			this.dbContext = dbContext;
			this.dbSet = dbSet;
			this.objectContext = ((IObjectContextAdapter)dbContext).ObjectContext;
			this.predicate = predicate;
		}

		public int InsertAll<TEntity>(
			IEnumerable<TEntity> items, InsertAllOptions? options = null)
			where TEntity : class, TBaseEntity
		{
			var provider = Configuration.Providers.FirstOrDefault(p => p.CanInsert && p.CanHandle(this.dbContext));

			if (provider == null)
			{
				if (Configuration.DisableDefaultFallback)
					throw new InvalidOperationException("No provider supporting the InsertAll operation was found.");

				return Fallbacks.DefaultInsertAll(this.dbContext, this.dbSet, items);
			}

			var mapping = EfMappingFactory.GetMappingsForContext(this.objectContext);
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
				this.dbContext, tableMapping.Schema, tableMapping.TableName, properties, items, options);
		}

		public Task<int> InsertAllAsync<TEntity>(
			IEnumerable<TEntity> items, InsertAllOptions? options = null, CancellationToken cancellationToken = default)
			where TEntity : class, TBaseEntity
		{
			var provider = Configuration.Providers.FirstOrDefault(p => p.CanInsert && p.CanHandle(this.dbContext));

			if (provider == null)
			{
				if (Configuration.DisableDefaultFallback)
					throw new InvalidOperationException("No provider supporting the InsertAll operation was found.");

				return Fallbacks.DefaultInsertAllAsync(this.dbContext, this.dbSet, items, cancellationToken);
			}

			var mapping = EfMappingFactory.GetMappingsForContext(this.objectContext);
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
				this.dbContext, tableMapping.Schema, tableMapping.TableName, properties, items, options,
				cancellationToken);
		}

		public int UpdateAll<TEntity>(
			IEnumerable<TEntity> items, Action<UpdateSpecification<TEntity>> updateSpecification,
			UpdateAllOptions? options = null)
			where TEntity : class, TBaseEntity
		{
			var provider = Configuration.Providers.FirstOrDefault(p => p.CanBulkUpdate && p.CanHandle(this.dbContext))
				?? throw new InvalidOperationException("No provider supporting the UpdateAll operation was found.");

			var mapping = EfMappingFactory.GetMappingsForContext(this.objectContext);
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
				this.dbContext, tableMapping.Schema, tableMapping.TableName, properties, items, spec, options);
		}

		public Task<int> UpdateAllAsync<TEntity>(
			IEnumerable<TEntity> items, Action<UpdateSpecification<TEntity>> updateSpecification,
			UpdateAllOptions? options = null, CancellationToken cancellationToken = default)
			where TEntity : class, TBaseEntity
		{
			var provider = Configuration.Providers.FirstOrDefault(p => p.CanBulkUpdate && p.CanHandle(this.dbContext))
				?? throw new InvalidOperationException("No provider supporting the UpdateAll operation was found");

			var mapping = EfMappingFactory.GetMappingsForContext(this.objectContext);
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
				this.dbContext, tableMapping.Schema, tableMapping.TableName, properties, items, spec, options,
				cancellationToken);
		}

		public IEFBatchOperationFiltered<TBaseEntity> Where(
			Expression<Func<TBaseEntity, bool>> predicate)
		{
			return new EFBatchOperation<TContext, TBaseEntity>(this.dbContext, this.dbSet, predicate);
		}

		public int DeleteTop(
			int numberOfRows, TransactionalBehavior? transactionalBehavior = null)
		{
			var topExpression = $"TOP ({numberOfRows})";

			return this.Delete(transactionalBehavior, topExpression);
		}

		public Task<int> DeleteTopAsync(
			int numberOfRows, TransactionalBehavior? transactionalBehavior = null,
			CancellationToken cancellationToken = default)
		{
			var topExpression = $"TOP ({numberOfRows})";

			return this.DeleteAsync(transactionalBehavior, topExpression, cancellationToken);
		}

		public int DeleteTopPercent(
			double percentOfRows, TransactionalBehavior? transactionalBehavior = null)
		{
			var topExpression = $"TOP ({percentOfRows.ToString(CultureInfo.InvariantCulture)}) PERCENT";

			return this.Delete(transactionalBehavior, topExpression);
		}

		public Task<int> DeleteTopPercentAsync(
			double percentOfRows, TransactionalBehavior? transactionalBehavior = null,
			CancellationToken cancellationToken = default)
		{
			var topExpression = $"TOP ({percentOfRows.ToString(CultureInfo.InvariantCulture)}) PERCENT";

			return this.DeleteAsync(transactionalBehavior, topExpression, cancellationToken);
		}

		public int Delete(
			TransactionalBehavior? transactionalBehavior = null)
		{
			return this.Delete(transactionalBehavior, topExpression: null);
		}

		public Task<int> DeleteAsync(
			TransactionalBehavior? transactionalBehavior = null, CancellationToken cancellationToken = default)
		{
			return this.DeleteAsync(transactionalBehavior, topExpression: null, cancellationToken);
		}

		private int Delete(
			TransactionalBehavior? transactionalBehavior, string? topExpression)
		{
			var provider = Configuration.Providers.FirstOrDefault(p => p.CanDelete && p.CanHandle(this.dbContext));

			if (provider == null)
			{
				if (Configuration.DisableDefaultFallback)
					throw new InvalidOperationException("No provider supporting the Delete operation was found.");

				return Fallbacks.DefaultDelete(this.dbContext, this.dbSet, this.predicate!);
			}

			var set = this.objectContext.CreateObjectSet<TBaseEntity>();
			var query = (ObjectQuery<TBaseEntity>)set.Where(this.predicate);
			var queryInformation = provider.GetQueryInformation(query);
			queryInformation.TopExpression = topExpression;

			var delete = provider.GetDeleteQuery(queryInformation);
			var parameters = query.Parameters.Select(p => new SqlParameter { Value = p.Value, ParameterName = p.Name }).ToArray<object>();

			if (transactionalBehavior != null)
				return this.objectContext.ExecuteStoreCommand(transactionalBehavior.Value, delete, parameters);
			else
				return this.objectContext.ExecuteStoreCommand(delete, parameters);
		}

		private Task<int> DeleteAsync(
			TransactionalBehavior? transactionalBehavior, string? topExpression, CancellationToken cancellationToken)
		{
			var provider = Configuration.Providers.FirstOrDefault(p => p.CanDelete && p.CanHandle(this.dbContext));

			if (provider == null)
			{
				if (Configuration.DisableDefaultFallback)
					throw new InvalidOperationException("No provider supporting the Delete operation was found.");

				return Fallbacks.DefaultDeleteAsync(this.dbContext, this.dbSet, this.predicate!, cancellationToken);
			}

			var set = this.objectContext.CreateObjectSet<TBaseEntity>();
			var query = (ObjectQuery<TBaseEntity>)set.Where(this.predicate);
			var queryInformation = provider.GetQueryInformation(query);
			queryInformation.TopExpression = topExpression;

			var delete = provider.GetDeleteQuery(queryInformation);
			var parameters = query.Parameters.Select(p => new SqlParameter { Value = p.Value, ParameterName = p.Name }).ToArray<object>();

			if (transactionalBehavior != null)
				return this.objectContext.ExecuteStoreCommandAsync(transactionalBehavior.Value, delete, cancellationToken, parameters);
			else
				return this.objectContext.ExecuteStoreCommandAsync(delete, cancellationToken, parameters);
		}

		public int Update<TP>(
			Expression<Func<TBaseEntity, TP>> prop, Expression<Func<TBaseEntity, TP>> modifier,
			TransactionalBehavior? transactionalBehavior = null)
		{
			var provider = Configuration.Providers.FirstOrDefault(p => p.CanUpdate && p.CanHandle(this.dbContext));

			if (provider == null)
			{
				if (Configuration.DisableDefaultFallback)
					throw new InvalidOperationException("No provider supporting the Update operation was found.");

				return Fallbacks.DefaultUpdate(this.dbContext, this.dbSet, this.predicate!, prop, modifier);
			}

			var set = this.objectContext.CreateObjectSet<TBaseEntity>();

			var query = (ObjectQuery<TBaseEntity>)set.Where(this.predicate);
			var queryInformation = provider.GetQueryInformation(query);

			var updateExpression = ExpressionHelper.CombineExpressions(prop, modifier);

			var mquery = (ObjectQuery<TBaseEntity>)this.objectContext.CreateObjectSet<TBaseEntity>().Where(updateExpression);
			var mqueryInfo = provider.GetQueryInformation(mquery);

			var update = provider.GetUpdateQuery(queryInformation, mqueryInfo);

			var parameters = query.Parameters
				.Concat(mquery.Parameters)
				.Select(p => new SqlParameter { Value = p.Value, ParameterName = p.Name })
				.ToArray<object>();

			if (transactionalBehavior != null)
				return this.objectContext.ExecuteStoreCommand(transactionalBehavior.Value, update, parameters);
			else
				return this.objectContext.ExecuteStoreCommand(update, parameters);
		}

		public Task<int> UpdateAsync<TP>(
			Expression<Func<TBaseEntity, TP>> prop, Expression<Func<TBaseEntity, TP>> modifier,
			TransactionalBehavior? transactionalBehavior = null, CancellationToken cancellationToken = default)
		{
			var provider = Configuration.Providers.FirstOrDefault(p => p.CanUpdate && p.CanHandle(this.dbContext));

			if (provider == null)
			{
				if (Configuration.DisableDefaultFallback)
					throw new InvalidOperationException("No provider supporting the Update operation was found.");

				return Fallbacks.DefaultUpdateAsync(
					this.dbContext, this.dbSet, this.predicate!, prop, modifier, cancellationToken);
			}

			var set = this.objectContext.CreateObjectSet<TBaseEntity>();

			var query = (ObjectQuery<TBaseEntity>)set.Where(this.predicate);
			var queryInformation = provider.GetQueryInformation(query);

			var updateExpression = ExpressionHelper.CombineExpressions(prop, modifier);

			var mquery = (ObjectQuery<TBaseEntity>)this.objectContext.CreateObjectSet<TBaseEntity>().Where(updateExpression);
			var mqueryInfo = provider.GetQueryInformation(mquery);

			var update = provider.GetUpdateQuery(queryInformation, mqueryInfo);

			var parameters = query.Parameters
				.Concat(mquery.Parameters)
				.Select(p => new SqlParameter { Value = p.Value, ParameterName = p.Name })
				.ToArray<object>();

			if (transactionalBehavior != null)
				return this.objectContext.ExecuteStoreCommandAsync(transactionalBehavior.Value, update, cancellationToken, parameters);
			else
				return this.objectContext.ExecuteStoreCommandAsync(update, cancellationToken, parameters);
		}
	}
}
