using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;

namespace EntityFramework.Utilities;

// Adapted from http://romiller.com/2013/09/24/ef-code-first-mapping-between-types-tables/
// This whole file contains a hack needed because the mapping API is internal pre 6.1 at least.

/// <summary>
/// Represents the mapping of an entity type to one or mode tables in the database.
///
/// A single entity can be mapped to more than one table when 'Entity Splitting' is used.
/// Entity Splitting involves mapping different properties from the same type to different tables.
/// See <see href="http://msdn.com/data/jj591617#2.7" /> for more details.
/// </summary>
public class TypeMapping
{
	/// <summary>
	/// The type of the entity from the model.
	/// </summary>
	public Type EntityType { get; set; } = null!;

	/// <summary>
	/// The table(s) that the entity is mapped to.
	/// </summary>
	public List<TableMapping> TableMappings { get; set; } = null!;
}

/// <summary>
/// Represents the mapping of an entity to a table in the database.
/// </summary>
public class TableMapping
{
	/// <summary>
	/// The name of the table the entity is mapped to.
	/// </summary>
	public string TableName { get; set; } = null!;

	/// <summary>
	/// The schema of the table the entity is mapped to.
	/// </summary>
	public string Schema { get; set; } = null!;

	/// <summary>
	/// Details of the property-to-column mapping.
	/// </summary>
	public List<PropertyMapping> PropertyMappings { get; set; } = null!;

	/// <summary>
	/// Null if not TPH.
	/// </summary>
	public TphConfiguration? TphConfiguration { get; set; }
}

public class TphConfiguration
{
	public Dictionary<Type, string> Mappings { get; set; } = null!;

	public string ColumnName { get; set; } = null!;
}

/// <summary>
/// Represents the mapping of a property to a column in the database.
/// </summary>
public class PropertyMapping
{
	/// <summary>
	/// The property chain leading to this property.
	/// For scalar properties this is a single value but for Complex properties this is a dot (.) separated list.
	/// </summary>
	public string PropertyName { get; set; } = null!;

	/// <summary>
	/// The column that property is mapped to.
	/// </summary>
	public string ColumnName { get; set; } = null!;

	/// <summary>
	/// Used when we have TPH to exclude entities.
	/// </summary>
	public Type ForEntityType { get; set; } = null!;

	public string DataType { get; set; } = null!;

	public bool IsPrimaryKey { get; set; }

	public string DataTypeFull { get; set; } = null!;

	public bool IsComputed { get; set; }
}

/// <summary>
/// Represents that mapping between entity types and tables in an EF model.
/// </summary>
public class EfMapping
{
	/// <summary>
	/// Mapping information for each entity type in the model.
	/// </summary>
	public Dictionary<Type, TypeMapping> TypeMappings { get; set; }

	/// <summary>
	/// Initializes an instance of the EfMapping class.
	/// </summary>
	/// <param name="context">The context to get the mapping from.</param>
	public EfMapping(ObjectContext context)
	{
		TypeMappings = [];

		var metadata = context.MetadataWorkspace;

		//EF61Test(metadata);

		// Conceptual part of the model has info about the shape of our entity classes.
		var conceptualContainer = metadata.GetItems<EntityContainer>(DataSpace.CSpace).Single();

		// Storage part of the model has info about the shape of our tables.
		_ = metadata.GetItems<EntityContainer>(DataSpace.SSpace).Single();

		// Object part of the model that contains info about the actual CLR types.
		var objectItemCollection = (ObjectItemCollection)metadata.GetItemCollection(DataSpace.OSpace);

		// Loop through each entity type in the model.
		foreach (var set in conceptualContainer.BaseEntitySets.OfType<EntitySet>())
		{
			// Find the mapping between conceptual and storage model for this entity set.
			var mapping = metadata.GetItems<EntityContainerMapping>(DataSpace.CSSpace)
				.Single()
				.EntitySetMappings
				.Single(s => s.EntitySet == set);

			var typeMapping = new TypeMapping
			{
				TableMappings = [],
				EntityType = GetClrType(metadata, objectItemCollection, set),
			};

			TypeMappings.Add(typeMapping.EntityType, typeMapping);

			var tableMapping = new TableMapping
			{
				PropertyMappings = [],
			};
			var mappingToLookAt = mapping.EntityTypeMappings.FirstOrDefault(m => m.IsHierarchyMapping) ?? mapping.EntityTypeMappings.First();
			tableMapping.Schema = mappingToLookAt.Fragments[0].StoreEntitySet.Schema;
			tableMapping.TableName = mappingToLookAt.Fragments[0].StoreEntitySet.Table ?? mappingToLookAt.Fragments[0].StoreEntitySet.Name;
			typeMapping.TableMappings.Add(tableMapping);

			void Recurse(Type t, System.Data.Entity.Core.Mapping.PropertyMapping item, string path)
			{
				if (item is ComplexPropertyMapping complex)
				{
					foreach (var child in complex.TypeMappings[0].PropertyMappings)
					{
						Recurse(t, child, path + complex.Property.Name + ".");
					}
				}
				else if (item is ScalarPropertyMapping scalar)
				{
					tableMapping.PropertyMappings.Add(new PropertyMapping
					{
						ColumnName = scalar.Column.Name,
						DataType = scalar.Column.TypeName,
						DataTypeFull = GetFullTypeName(scalar),
						PropertyName = path + scalar.Property.Name,
						ForEntityType = t,
						IsComputed = scalar.Column.IsStoreGeneratedComputed,
					});
				}
			};

			Type GetClr(MappingFragment m)
			{
				return GetClrTypeFromTypeMapping(metadata, objectItemCollection, (EntityTypeMapping)m.TypeMapping);
			}

			if (mapping.EntityTypeMappings.Any(m => m.IsHierarchyMapping))
			{
				var withConditions = mapping.EntityTypeMappings.Where(m => m.Fragments[0].Conditions.Count != 0).ToArray();

				if (withConditions.Length != 0)
				{
					tableMapping.TphConfiguration = new TphConfiguration
					{
						ColumnName = withConditions.First().Fragments[0].Conditions[0].Column.Name,
						Mappings = [],
					};

					foreach (var item in withConditions)
					{
						tableMapping.TphConfiguration.Mappings.Add(
							GetClr(item.Fragments[0]),
							((ValueConditionMapping)item.Fragments[0].Conditions[0]).Value.ToString()
						);
					}
				}
			}

			foreach (var entityType in mapping.EntityTypeMappings)
			{
				foreach (var item in entityType.Fragments[0].PropertyMappings)
				{
					Recurse(GetClr(entityType.Fragments[0]), item, "");
				}
			}

			// Inheriting propertymappings contains duplicates for id's.
			tableMapping.PropertyMappings = tableMapping.PropertyMappings.GroupBy(p => p.ColumnName)
				.Select(g => g.OrderByDescending(outer => g.Count(inner => inner.ForEntityType.IsSubclassOf(outer.ForEntityType))).First())
				.ToList();

			foreach (var item in tableMapping.PropertyMappings)
			{
				if ((mappingToLookAt.EntityType ?? mappingToLookAt.IsOfEntityTypes[0]).KeyProperties.Any(p => p.Name == item.PropertyName))
				{
					item.IsPrimaryKey = true;
				}
			}
		}
	}

	private static string GetFullTypeName(ScalarPropertyMapping scalar)
	{
		if (scalar.Column.TypeName == "nvarchar" ||
			scalar.Column.TypeName == "varchar" ||
			scalar.Column.TypeName == "nchar" ||
			scalar.Column.TypeName == "char")
		{
			return $"{scalar.Column.TypeName}({scalar.Column.MaxLength})";
		}

		if (scalar.Column.TypeName == "decimal" || scalar.Column.TypeName == "numeric")
		{
			return $"{scalar.Column.TypeName}({scalar.Column.Precision},{scalar.Column.Scale})";
		}

		return scalar.Column.TypeName;
	}

	private static Type GetClrTypeFromTypeMapping(MetadataWorkspace metadata, ObjectItemCollection objectItemCollection, EntityTypeMapping mapping)
	{
		return GetClrType(metadata, objectItemCollection, mapping.EntityType ?? mapping.IsOfEntityTypes.First());
	}

	private static Type GetClrType(MetadataWorkspace metadata, ObjectItemCollection objectItemCollection, EntitySet set)
	{
		return GetClrType(metadata, objectItemCollection, set.ElementType);
	}

	private static Type GetClrType(MetadataWorkspace metadata, ObjectItemCollection objectItemCollection, EntityTypeBase type)
	{
		return metadata
			.GetItems<EntityType>(DataSpace.OSpace)
			.Select(objectItemCollection.GetClrType)
			.Single(e => e.Name == type.Name);
	}
}

public static class EfMappingFactory
{
	private static readonly Dictionary<Type, EfMapping> Cache = [];

	public static EfMapping GetMappingsForContext(ObjectContext context)
	{
		var type = context.GetType();

		if (!Cache.TryGetValue(type, out var mapping))
		{
			// Lock only if we don't have the item in the cache.
			lock (Cache)
			{
				if (!Cache.TryGetValue(type, out mapping))
				{
					mapping = new EfMapping(context);
					Cache.Add(type, mapping);
				}
			}
		}

		return mapping;
	}
}
