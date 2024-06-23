using System.Collections;
using System.Linq.Expressions;

namespace EntityFramework.Utilities;

public interface IIncludeContainer<T>
{
	IEnumerable<IncludeExecuter> Includes { get; }
}

public class IncludeExecuter
{
	internal Type ElementType { get; set; } = null!;

	internal Action<IEnumerable<MethodCallExpression>, IEnumerable> Loader { get; set; } = null!;

	internal Action<object> SingleItemLoader { get; set; } = null!;
}
