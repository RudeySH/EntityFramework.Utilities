namespace EntityFramework.Utilities
{
	public class QueryInformation
	{
		public string Schema { get; set; } = null!;

		public string Table { get; set; } = null!;

		public string Alias { get; set; } = null!;

		public string? WhereSql { get; set; }

		public string? TopExpression { get; set; }
	}
}
