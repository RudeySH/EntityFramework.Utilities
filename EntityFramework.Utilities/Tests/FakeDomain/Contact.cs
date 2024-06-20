namespace Tests.FakeDomain
{
	public class Contact : Person
	{
		public string Title { get; set; } = null!;

		public ICollection<PhoneNumber> PhoneNumbers { get; set; } = null!;

		public ICollection<Email> Emails { get; set; } = null!;

		public static Contact Build(string firstname, string lastname, string title, DateTime? birthdate = null)
		{
			return new Contact
			{
				Id = Guid.NewGuid(),
				FirstName = firstname,
				LastName = lastname,
				Title = title,
				BirthDate = birthdate ?? DateTime.Today
			};
		}
	}
}
