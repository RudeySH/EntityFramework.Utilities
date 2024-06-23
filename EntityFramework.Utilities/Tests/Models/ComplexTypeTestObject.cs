using System.ComponentModel.DataAnnotations.Schema;

namespace Tests.Models;

public class ObjectWithComplexType
{
	public int Id { get; set; }

	public ComplexTypeObject ComplexType { get; set; } = new ComplexTypeObject();
}

[ComplexType]
public class ComplexTypeObject
{
	public string? Name { get; set; }

	public AnotherComplexTypeObject Another { get; set; } = new AnotherComplexTypeObject();
}

[ComplexType]
public class AnotherComplexTypeObject
{
	public string? Name { get; set; }
}
