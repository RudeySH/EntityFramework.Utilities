using System.Linq.Expressions;

namespace EntityFramework.Utilities;

public class EFUQueryable<T> : IOrderedQueryable<T>, IIncludeContainer<T>
{
	private readonly EFUQueryProvider<T> _provider;
	private readonly List<IncludeExecuter> _includes = [];

	public IEnumerable<IncludeExecuter> Includes => _includes;

	public EFUQueryable(IQueryable source)
	{
		Expression = Expression.Constant(this);
		_provider = new EFUQueryProvider<T>(source);
	}

	public EFUQueryable(IQueryable source, Expression e)
	{
		Expression = e ?? throw new ArgumentNullException(nameof(e));
		_provider = new EFUQueryProvider<T>(source);
	}

	public IEnumerator<T> GetEnumerator()
	{
		return _provider.ExecuteEnumerable(Expression).Cast<T>().GetEnumerator();
	}

	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
	{
		return _provider.ExecuteEnumerable(Expression).GetEnumerator();
	}

	public EFUQueryable<T> Include(IncludeExecuter include)
	{
		_includes.Add(include);
		return this;
	}

	public Type ElementType => typeof(T);

	public Expression Expression { get; }

	public System.Linq.IQueryProvider Provider => _provider;
}
