using System.Collections.Generic;

namespace PerformanceTests.Models
{
	public class Publication
	{
		public int Id { get; set; }

		public string Title { get; set; }

		public ICollection<Comment> Comments { get; set; }
	}
}
