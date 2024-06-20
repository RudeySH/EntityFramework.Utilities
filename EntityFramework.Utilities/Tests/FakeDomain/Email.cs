namespace Tests.FakeDomain
{
	public class Email
	{
		public Guid Id { get; set; }

		public string Address { get; set; } = null!;

		public Guid ContactId { get; set; }

		public Contact Contact { get; set; } = null!;
	}
}
