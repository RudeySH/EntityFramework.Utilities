namespace EntityFramework.Utilities
{
	internal static class SqlStringHelper
	{
		internal static string FixParantheses(string str)
		{
			var chars = str.ToCharArray();
			var stack = new Stack<Tuple<int, int>>();
			var toRemove = new HashSet<int>();

			foreach (var c in chars.Select((c, i) => new { Character = c, Index = i }))
			{
				switch (c.Character)
				{
					case '(':
						stack.Push(new Tuple<int, int>(c.Index, -1));
						break;

					case ')':
						if (stack.Count != 0)
						{
							stack.Pop();
						}
						else
						{
							toRemove.Add(c.Index);
						}
						break;
				}
			}

			foreach (var item in stack)
			{
				toRemove.Add(item.Item1);
			}

			return string.Join("", chars.Where((c, i) => !toRemove.Contains(i)).Select(c => c));
		}
	}
}
