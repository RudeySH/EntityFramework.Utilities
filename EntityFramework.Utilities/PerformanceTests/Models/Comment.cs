namespace PerformanceTests.Models
{
	using System;

	public class Comment
	{
		public int Id { get; set; }

		public string Text { get; set; }

		public DateTime Date { get; set; }

		public int Reads { get; set; }

		public Address Address { get; set; }

		public int? PublicationId { get; set; }

		public Publication Publication { get; set; }
	}
}
