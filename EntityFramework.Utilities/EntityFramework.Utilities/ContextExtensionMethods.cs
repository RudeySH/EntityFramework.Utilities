using EntityFramework.Utilities.Internal;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq.Expressions;

namespace EntityFramework.Utilities;

public static class ContextExtensionMethods
{
	public class AttachAndModifyContext<T>(DbEntityEntry<T> entry)
		where T : class
	{
		public AttachAndModifyContext<T> Set<TProp>(Expression<Func<T, TProp>> property, TProp value)
		{
			var setter = ExpressionHelper.PropertyExpressionToSetter(property);
			setter(entry.Entity, value);
			entry.Property(property).IsModified = true;

			return this;
		}
	}

	public static AttachAndModifyContext<T> AttachAndModify<T>(this DbContext source, T item)
		where T : class
	{
		var set = source.Set<T>();
		set.Attach(item);
		var entry = source.Entry(item);

		return new AttachAndModifyContext<T>(entry);
	}
}
