namespace PerformanceTests.Models
{
	public class Comment
	{
		public int Id { get; set; }

		public string Text { get; set; } = null!;

		public DateTime Date { get; set; }

		public int Reads { get; set; }

		public Address Address { get; set; } = null!;
	}
}
