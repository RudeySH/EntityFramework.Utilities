using System.Data.Entity.Core.Objects;

namespace EntityFramework.Utilities.Internal;

internal static class EFMappingFactory
{
	private static readonly Dictionary<Type, EFMapping> Cache = [];

	public static EFMapping GetMappingsForContext(ObjectContext context)
	{
		var type = context.GetType();

		if (!Cache.TryGetValue(type, out var mapping))
		{
			// Lock only if we don't have the item in the cache.
			lock (Cache)
			{
				if (!Cache.TryGetValue(type, out mapping))
				{
					mapping = new EFMapping(context);
					Cache.Add(type, mapping);
				}
			}
		}

		return mapping;
	}
}
