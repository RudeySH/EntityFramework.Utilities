using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EntityFramework.Utilities
{
	public static class EFQueryHelpers
	{
		/// <summary>
		/// Loads a child collection in a more efficient way than the standard Include. Will run all involved queries as NoTracking
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="TChild"></typeparam>
		/// <param name="query"></param>
		/// <param name="context"></param>
		/// <param name="collectionSelector">The navigation property. It can be filtered and sorted with the methods Where,OrderBy(Descending),ThenBy(Descending) </param>
		/// <returns></returns>
		public static EFUQueryable<T> IncludeEFU<T, TChild>(this IQueryable<T> query, DbContext context, Expression<Func<T, IEnumerable<TChild>>> collectionSelector)
			where T : class
			where TChild : class
		{
			var octx = (context as IObjectContextAdapter).ObjectContext;
			var cSpaceTables = octx.MetadataWorkspace.GetItems<EntityType>(DataSpace.CSpace);
			var cSpaceType = cSpaceTables.Single(t => t.Name == typeof(T).Name); //Use single to avoid any problems with multiple tables using the same type
			var keys = cSpaceType.KeyProperties;
			if (keys.Count > 1)
			{
				throw new InvalidOperationException("The include method only works on single key entities");
			}

			var fkGetter = GetForeignKeyGetter<T, TChild>(cSpaceTables);

			var pkInfo = typeof(T).GetProperty(keys.First().Name);
			var pkGetter = MakeGetterDelegate<T>(pkInfo);

			var childCollectionModifiers = new List<MethodCallExpression>();
			var childProp = SetCollectionModifiersAndGetChildProperty(collectionSelector, childCollectionModifiers);
			var setter = MakeSetterDelegate<T>(childProp);

			var e = new IncludeExecuter
			{
				ElementType = typeof(TChild),
				SingleItemLoader = parent =>
				{
					if (parent == null)
					{
						return;
					}

					var children = octx.CreateObjectSet<TChild>();
					GetRootEntityToChildCollectionSelector<T, TChild>(cSpaceType);

					var q = ApplyChildCollectionModifiers(children, childCollectionModifiers);

					var rootPk = pkGetter((T)parent);
					var param = Expression.Parameter(typeof(TChild), "x");
					var fk = GetFkProperty<T, TChild>(cSpaceTables);
					var body = Expression.Equal(Expression.Property(param, fk), Expression.Constant(rootPk));
					var where = Expression.Lambda<Func<TChild, bool>>(body, param);

					q = q.AsNoTracking().Where(where);

					setter((T)parent, q.ToList());
				},
				Loader = (rootFilters, parents) =>
				{
					var baseType = typeof(T).BaseType != typeof(object) ? typeof(T).BaseType : typeof(T);

					dynamic dynamicSet = octx.GetType()
									.GetMethod("CreateObjectSet", new Type[] { })
									?.MakeGenericMethod(baseType)
									.Invoke(octx, new Object[] { });

					if (!(dynamicSet?.OfType<T>() is ObjectQuery<T> set)) return;

					IQueryable<T> q = set;
					foreach (var item in rootFilters)
					{
						var newSource = Expression.Constant(q);
						var arguments = Enumerable.Repeat(newSource, 1).Concat(item.Arguments.Skip(1)).ToArray();
						var newMethods = Expression.Call(item.Method, arguments);
						q = q.Provider.CreateQuery<T>(newMethods);
					}

					var lambdaExpression = GetRootEntityToChildCollectionSelector<T, TChild>(cSpaceType);

					var childQ = q.SelectMany(lambdaExpression);
					childQ = ApplyChildCollectionModifiers(childQ, childCollectionModifiers);

					var dict = childQ.AsNoTracking().ToLookup(fkGetter);
					var list = parents.Cast<T>().ToList();

					foreach (var parent in list)
					{
						var prop = pkGetter(parent);
						var childs = dict.Contains(prop) ? dict[prop].ToList() : new List<TChild>();
						setter(parent, childs);
					}
				}
			};

			return new EFUQueryable<T>(query.AsNoTracking()).Include(e);
		}

		private static IQueryable<TChild> ApplyChildCollectionModifiers<TChild>(IQueryable<TChild> childQ, List<MethodCallExpression> childCollectionModifiers) where TChild : class
		{
			foreach (var item in childCollectionModifiers)
			{
				switch (item.Method.Name)
				{
					case "Where":
						childQ = childQ.Where((Expression<Func<TChild, bool>>)item.Arguments[1]);
						break;
					case "OrderBy":
					case "ThenBy":
					case "OrderByDescending":
					case "ThenByDescending":
						childQ = SortQuery(childQ, item, item.Method.Name);
						break;
					default:
						throw new NotSupportedException("The method " + item.Method.Name + " is not supported in the child query");
				}
			}

			return childQ;
		}

		private static PropertyInfo SetCollectionModifiersAndGetChildProperty<T, TChild>(Expression<Func<T, IEnumerable<TChild>>> collectionSelector, List<MethodCallExpression> childCollectionModifiers)
			where T : class
			where TChild : class
		{
			var temp = collectionSelector.Body;

			while (temp is MethodCallExpression)
			{
				var mce = temp as MethodCallExpression;
				childCollectionModifiers.Add(mce);
				temp = mce.Arguments[0];
			}

			//This loop is for VB, See: https://github.com/MikaelEliasson/EntityFramework.Utilities/issues/29
			while (temp is UnaryExpression)
			{
				var ue = temp as UnaryExpression;
				temp = ue.Operand;
			}

			childCollectionModifiers.Reverse(); //We parse from right to left so reverse it

			if (!(temp is MemberExpression))
			{
				throw new ArgumentException("Could not find a MemberExpression", nameof(collectionSelector));
			}

			var childProp = (temp as MemberExpression).Member as PropertyInfo;
			return childProp;
		}

		private static Func<TChild, object> GetForeignKeyGetter<T, TChild>(System.Collections.ObjectModel.ReadOnlyCollection<EntityType> cSpaceTables)
			where T : class
			where TChild : class
		{
			var fkInfo = GetFkProperty<T, TChild>(cSpaceTables);
			var fkGetter = MakeGetterDelegate<TChild>(fkInfo);
			return fkGetter;
		}

		private static PropertyInfo GetFkProperty<T, TChild>(System.Collections.ObjectModel.ReadOnlyCollection<EntityType> cSpaceTables)
			where T : class
			where TChild : class
		{
			var cSpaceChildType = cSpaceTables.Single(t => t.Name == typeof(TChild).Name); //Use single to avoid any problems with multiple tables using the same type
			var fk = cSpaceChildType.NavigationProperties.First(n => n.ToEndMember.GetEntityType().Name == typeof(T).Name).GetDependentProperties().First();
			var fkInfo = typeof(TChild).GetProperty(fk.Name);
			return fkInfo;
		}

		private static IQueryable<TChild> SortQuery<TChild>(IQueryable<TChild> query, MethodCallExpression item, string method)
		{
			var body = (LambdaExpression)item.Arguments[1];

			if (body != null)
			{
				var call = Expression.Call(
					typeof(Queryable),
					method,
					new[] { typeof(TChild), body.Body.Type },
					query.Expression,
					Expression.Quote(body));

				return (IOrderedQueryable<TChild>)query.Provider.CreateQuery<TChild>(call);
			}

			return null;
		}

		private static Expression<Func<T, IEnumerable<TChild>>> GetRootEntityToChildCollectionSelector<T, TChild>(EntityType cSpaceType)
			where T : class
			where TChild : class
		{
			var parameter = Expression.Parameter(typeof(T), "t");
			var memberExpression = Expression.Property(parameter, cSpaceType.NavigationProperties.First(p => p.ToEndMember.GetEntityType().Name == typeof(TChild).Name).Name);
			var lambdaExpression = Expression.Lambda<Func<T, IEnumerable<TChild>>>(memberExpression, parameter);
			return lambdaExpression;
		}

		private static Action<T, object> MakeSetterDelegate<T>(PropertyInfo property)
		{
			var setMethod = property.GetSetMethod();

			if (setMethod != null && setMethod.GetParameters().Length == 1)
			{
				var target = Expression.Parameter(typeof(T));
				var value = Expression.Parameter(typeof(object));
				var body = Expression.Call(target, setMethod,
					Expression.Convert(value, property.PropertyType));
				return Expression.Lambda<Action<T, object>>(body, target, value)
					.Compile();
			}

			return null;
		}

		private static Func<TX, object> MakeGetterDelegate<TX>(PropertyInfo property)
		{
			var getMethod = property.GetGetMethod();

			if (getMethod != null)
			{
				var target = Expression.Parameter(typeof(TX));
				var body = Expression.Call(target, getMethod);
				Expression conversion = Expression.Convert(body, typeof(object));
				return Expression.Lambda<Func<TX, object>>(conversion, target)
					.Compile();
			}

			return null;
		}
	}
}
