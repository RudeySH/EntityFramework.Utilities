namespace Tests.Models;

public class ReorderedBlogPost
{
	public int Id { get; set; }

	public string ShortTitle { get; set; } = null!;

	public DateTime Created { get; set; }

	public string Title { get; set; } = null!; // Reversed order of this and created for Batch Insert testing.

	public int Reads { get; set; }

	public AuthorInfo Author { get; set; } = null!;
}
