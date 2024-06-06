using System;
using System.Data.Common;
using System.Linq.Expressions;

namespace EntityFramework.Utilities
{
	public interface IEFBatchOperationFiltered<T>
	{
		int Delete(DbConnection connection = null);

		int DeleteTop(int numberOfRows, DbConnection connection = null);

		int DeleteTopPercent(double numberOfRows, DbConnection connection = null);

		int Update<TP>(Expression<Func<T, TP>> prop, Expression<Func<T, TP>> modifier, DbConnection connection = null);
	}
}
