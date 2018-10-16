namespace PerformanceTests.Models
{
	using System.Collections.Generic;

	public class Publication
	{
		public int Id { get; set; }

		public string Title { get; set; }

		public ICollection<Comment> Comments { get; set; }
	}
}
