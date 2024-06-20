using System.Diagnostics;

namespace EntityFramework.Utilities
{
	[DebuggerDisplay("NameOnObject = {NameOnObject} NameInDatabase = {NameInDatabase}")]
	public class ColumnMapping
	{
		public string NameOnObject { get; set; } = null!;

		public string NameInDatabase { get; set; } = null!;

		public string? StaticValue { get; set; }
	}
}
