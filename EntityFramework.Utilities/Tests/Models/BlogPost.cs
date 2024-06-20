namespace Tests.Models
{
	public class BlogPost
	{
		public int Id { get; set; }

		public string Title { get; set; } = null!;

		public string? ShortTitle { get; set; }

		public DateTime Created { get; set; }

		public int Reads { get; set; }

		public AuthorInfo Author { get; set; } = null!;

		public ICollection<Comment> Comments { get; set; } = null!;

		public static BlogPost Create(string title, DateTime created)
		{
			return new BlogPost
			{
				Title = title,
				Created = created,
				Author = new AuthorInfo
				{
					Email = "m@m.com",
					Name = "name",
					Address = new Address
					{
						Line1 = "Street",
						Town = "Gothenburg",
						ZipCode = "41654",
					},
				},
			};
		}

		public static BlogPost Create(string title)
		{
			return new BlogPost
			{
				Title = title,
				Created = DateTime.Now,
				Author = new AuthorInfo
				{
					Email = "m@m.com",
					Name = "name",
					Address = new Address
					{
						Line1 = "Street",
						Town = "Gothenburg",
						ZipCode = "41654",
					},
				},
			};
		}
	}

	public class AuthorInfo
	{
		public string Name { get; set; } = null!;

		public string Email { get; set; } = null!;

		public Address Address { get; set; } = null!;
	}

	public class Address
	{
		public string Line1 { get; set; } = null!;

		public string ZipCode { get; set; } = null!;

		public string Town { get; set; } = null!;
	}
}
