using System.Diagnostics;

namespace EntityFramework.Utilities
{
	[DebuggerDisplay("NameOnObject = {NameOnObject} NameInDatabase = {NameInDatabase}")]
	public class ColumnMappingToUpdate : ColumnMapping
	{
		public string DataType { get; set; } = null!;

		public string DataTypeFull { get; set; } = null!;

		public bool IsPrimaryKey { get; set; }
	}
}
