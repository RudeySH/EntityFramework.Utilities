namespace Tests.Models;

public class Comment
{
	public int Id { get; set; }

	public string Text { get; set; } = null!;

	public int PostId { get; set; }

	public BlogPost Post { get; set; } = null!;
}
