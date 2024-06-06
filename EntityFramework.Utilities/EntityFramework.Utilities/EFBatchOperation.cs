using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

namespace EntityFramework.Utilities
{
	public static class EFBatchOperation
	{
		public static IEFBatchOperationBase<T> For<TContext, T>(TContext context, IDbSet<T> _set)
			where TContext : DbContext
			where T : class
		{
			return EFBatchOperation<TContext, T>.For(context);
		}
	}

	public class EFBatchOperation<TContext, T> : IEFBatchOperationBase<T>, IEFBatchOperationFiltered<T>
		where T : class
		where TContext : DbContext
	{
		private readonly ObjectContext _context;
		private readonly DbContext _dbContext;
		private Expression<Func<T, bool>> _predicate;
		private string _deleteTopExpression;

		private EFBatchOperation(TContext context)
		{
			_dbContext = context;
			_context = (context as IObjectContextAdapter).ObjectContext;
		}

		public static IEFBatchOperationBase<T> For(TContext context)
		{
			return new EFBatchOperation<TContext, T>(context);
		}

		public void InsertAll<TEntity>(
			IEnumerable<TEntity> items, DbConnection connection = null, int? batchSize = null,
			int? executeTimeout = null, SqlBulkCopyOptions copyOptions = SqlBulkCopyOptions.Default,
			DbTransaction transaction = null)
			where TEntity : class, T
		{
			var con = _context.Connection as EntityConnection;
			if (con == null && connection == null)
			{
				Configuration.Log("No provider could be found because the Connection didn't implement System.Data.EntityClient.EntityConnection");
				Fallbacks.DefaultInsertAll(_context, items);
				return;
			}

			var connectionToUse = connection ?? con.StoreConnection;
			var currentType = typeof(TEntity);
			var provider = Configuration.Providers.FirstOrDefault(p => p.CanHandle(connectionToUse));

			if (provider == null || !provider.CanInsert)
			{
				Configuration.Log("Found provider: " + (provider?.GetType().Name ?? "[]") + " for " + connectionToUse.GetType().Name);
				Fallbacks.DefaultInsertAll(_context, items);

				return;
			}

			var mapping = EfMappingFactory.GetMappingsForContext(_dbContext);
			var typeMapping = mapping.TypeMappings[typeof(T)];
			var tableMapping = typeMapping.TableMappings.First();

			var properties = tableMapping.PropertyMappings
				.Where(p => currentType.IsSubclassOf(p.ForEntityType) || p.ForEntityType == currentType)
				.Where(p => p.IsComputed == false)
				.Select(p => new ColumnMapping { NameInDatabase = p.ColumnName, NameOnObject = p.PropertyName })
				.ToList();

			if (tableMapping.TphConfiguration != null)
			{
				properties.Add(new ColumnMapping
				{
					NameInDatabase = tableMapping.TphConfiguration.ColumnName,
					StaticValue = tableMapping.TphConfiguration.Mappings[typeof(TEntity)]
				});
			}

			provider.InsertItems(
				items, tableMapping.Schema, tableMapping.TableName, properties, connectionToUse, batchSize,
				executeTimeout, copyOptions, transaction);
		}

		public void UpdateAll<TEntity>(
			IEnumerable<TEntity> items, Action<UpdateSpecification<TEntity>> updateSpecification,
			DbConnection connection = null, int? batchSize = null, int? executeTimeout = null,
			SqlBulkCopyOptions copyOptions = SqlBulkCopyOptions.Default, DbTransaction transaction = null,
			bool insertIfNotMatched = false, bool deleteIfNotMatched = false)
			where TEntity : class, T
		{
			var con = _context.Connection as EntityConnection;

			if (con == null && connection == null)
				throw new InvalidOperationException("No provider supporting the Update operation for this data source was found");

			var connectionToUse = connection ?? con.StoreConnection;
			var currentType = typeof(TEntity);
			var provider = Configuration.Providers.FirstOrDefault(p => p.CanHandle(connectionToUse));

			if (provider == null && con != null)
				provider = Configuration.Providers.FirstOrDefault(p => p.CanHandle(con.StoreConnection));

			if (provider == null || !provider.CanBulkUpdate)
			{
				Configuration.Log("Found provider: " + (provider?.GetType().Name ?? "[]") + " for " + connectionToUse.GetType().Name);

				throw new InvalidOperationException("No provider supporting the Update operation for this data source was found");
			}

			var mapping = EfMappingFactory.GetMappingsForContext(_dbContext);
			var typeMapping = mapping.TypeMappings[typeof(T)];
			var tableMapping = typeMapping.TableMappings.First();

			var properties = tableMapping.PropertyMappings
				.Where(p => currentType.IsSubclassOf(p.ForEntityType) || p.ForEntityType == currentType)
				.Where(p => p.IsComputed == false)
				.Select(p => new ColumnMapping
				{
					NameInDatabase = p.ColumnName,
					NameOnObject = p.PropertyName,
					DataType = p.DataType,
					DataTypeFull = p.DataTypeFull,
					IsPrimaryKey = p.IsPrimaryKey,
				})
				.ToList();

			var spec = new UpdateSpecification<TEntity>();
			updateSpecification(spec);

			var insertConnection = !(connectionToUse is SqlConnection) && con?.StoreConnection is SqlConnection
				? con.StoreConnection
				: connectionToUse;

			provider.UpdateItems(
				items, tableMapping.Schema, tableMapping.TableName, properties, connectionToUse, batchSize, spec,
				executeTimeout, copyOptions, transaction, insertConnection, insertIfNotMatched, deleteIfNotMatched);
		}

		public IEFBatchOperationFiltered<T> Where(Expression<Func<T, bool>> predicate)
		{
			_predicate = predicate;

			return this;
		}

		public int DeleteTop(int numberOfRows, DbConnection connection = null)
		{
			_deleteTopExpression = $"TOP ({numberOfRows})";

			return this.Delete(connection);
		}

		public int DeleteTopPercent(double percentOfRows, DbConnection connection = null)
		{
			_deleteTopExpression = $"TOP ({percentOfRows.ToString(CultureInfo.InvariantCulture)}) PERCENT";

			return this.Delete(connection);
		}

		public int Delete(DbConnection connection = null)
		{
			var con = _context.Connection as EntityConnection;

			if (con == null && connection == null)
			{
				Configuration.Log("No provider could be found because the Connection didn't implement System.Data.EntityClient.EntityConnection");

				return Fallbacks.DefaultDelete(_context, _predicate);
			}

			var connectionToUse = connection ?? con.StoreConnection;

			var provider = Configuration.Providers.FirstOrDefault(p => p.CanHandle(connectionToUse));

			if (provider == null || !provider.CanDelete)
			{
				Configuration.Log("Found provider: " + (provider?.GetType().Name ?? "[]") + " for " + connectionToUse.GetType().Name);

				return Fallbacks.DefaultDelete(_context, _predicate);
			}

			var set = _context.CreateObjectSet<T>();
			var query = (ObjectQuery<T>)set.Where(_predicate);
			var queryInformation = provider.GetQueryInformation(query);
			queryInformation.TopExpression = _deleteTopExpression;

			var delete = provider.GetDeleteQuery(queryInformation);
			var parameters = query.Parameters.Select(p => new SqlParameter { Value = p.Value, ParameterName = p.Name }).ToArray<object>();

			return _context.ExecuteStoreCommand(delete, parameters);
		}

		public int Update<TP>(Expression<Func<T, TP>> prop, Expression<Func<T, TP>> modifier, DbConnection connection = null)
		{
			var con = _context.Connection as EntityConnection;

			if (con == null && connection == null)
			{
				Configuration.Log("No provider could be found because the Connection didn't implement System.Data.EntityClient.EntityConnection");

				return Fallbacks.DefaultUpdate(_context, _predicate, prop, modifier);
			}

			var connectionToUse = connection ?? con.StoreConnection;
			var provider = Configuration.Providers.FirstOrDefault(p => p.CanHandle(connectionToUse));

			if (provider == null || !provider.CanUpdate)
			{
				Configuration.Log("Found provider: " + (provider?.GetType().Name ?? "[]") + " for " + connectionToUse.GetType().Name);

				return Fallbacks.DefaultUpdate(_context, _predicate, prop, modifier);
			}

			var set = _context.CreateObjectSet<T>();

			var query = (ObjectQuery<T>)set.Where(_predicate);
			var queryInformation = provider.GetQueryInformation(query);

			var updateExpression = ExpressionHelper.CombineExpressions(prop, modifier);

			var mquery = (ObjectQuery<T>)_context.CreateObjectSet<T>().Where(updateExpression);
			var mqueryInfo = provider.GetQueryInformation(mquery);

			var update = provider.GetUpdateQuery(queryInformation, mqueryInfo);

			var parameters = query.Parameters
				.Concat(mquery.Parameters)
				.Select(p => new SqlParameter { Value = p.Value, ParameterName = p.Name })
				.ToArray<object>();

			return _context.ExecuteStoreCommand(update, parameters);
		}
	}
}
