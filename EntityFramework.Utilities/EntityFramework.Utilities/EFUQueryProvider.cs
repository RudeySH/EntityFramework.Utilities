using System.Collections;
using System.Linq.Expressions;

namespace EntityFramework.Utilities;

public class EFUQueryProvider<T>(IQueryable source) : ExpressionVisitor, System.Linq.IQueryProvider
{
	private readonly IQueryable Source = source ?? throw new ArgumentNullException(nameof(source));

	public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
	{
		if (expression == null) throw new ArgumentNullException(nameof(expression));

		return new EFUQueryable<TElement>(Source, expression);
	}

	public IQueryable CreateQuery(Expression expression)
	{
		if (expression == null) throw new ArgumentNullException(nameof(expression));

		var elementType = expression.Type.GetGenericArguments().First();
		var result = (IQueryable)Activator.CreateInstance(typeof(EFUQueryable<>)
			.MakeGenericType(elementType),[Source, expression]);

		return result;
	}

	public TResult Execute<TResult>(Expression expression)
	{
		if (expression == null) throw new ArgumentNullException(nameof(expression));

		var result = Execute(expression);

		return (TResult)result;
	}

	public object Execute(Expression expression)
	{
		if (expression == null) throw new ArgumentNullException(nameof(expression));

		var efuQuery = GetIncludeContainer(expression);
		var translated = Visit(expression);
		var result = Source.Provider.Execute(translated);

		var first = efuQuery.Includes.First();
		first.SingleItemLoader(result);

		return result;
	}

	internal IEnumerable ExecuteEnumerable(Expression expression)
	{
		if (expression == null) throw new ArgumentNullException(nameof(expression));

		var modifiers = GetModifiersForQuery(expression);

		var efuQuery = GetIncludeContainer(expression);
		var translated = Visit(expression);
		var translatedQuery = Source.Provider.CreateQuery(translated);
		var list = new List<object>();
		foreach (var item in translatedQuery)
		{
			list.Add(item);
		}

		var first = efuQuery.Includes.First();
		first.Loader(modifiers, list);

		return list;
	}

	private static List<MethodCallExpression> GetModifiersForQuery(Expression expression)
	{
		var modifiers = new List<MethodCallExpression>();

		while (expression is MethodCallExpression methodCallExpression)
		{
			if (methodCallExpression.Method.Name != "IncludeEFU" && methodCallExpression.Method.Name != "Include")
			{
				modifiers.Add(methodCallExpression);
			}

			expression = methodCallExpression.Arguments[0];
		}

		modifiers.Reverse(); // We parse in reverse order so undo that

		return modifiers;
	}

	private static IIncludeContainer<T> GetIncludeContainer(Expression expression)
	{
		while (expression is MethodCallExpression methodCallExpression)
		{
			expression = methodCallExpression.Arguments[0];
		}

		return (IIncludeContainer<T>)((ConstantExpression)expression).Value;
	}

	#region Visitors
	protected override Expression VisitConstant(ConstantExpression node)
	{
		// fix up the Expression tree to work with EF again
		if (node.Type == typeof(EFUQueryable<T>))
		{
			return Source.Expression;
		}

		return base.VisitConstant(node);
	}
	#endregion
}
