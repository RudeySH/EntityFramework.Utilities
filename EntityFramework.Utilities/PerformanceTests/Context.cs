namespace PerformanceTests
{
	using System.Data.Entity;
	using PerformanceTests.Models;

	public class Context : DbContext
	{
		public Context()
			: base("Server=.\\SQLExpress;Database=EFUPerformance;Trusted_Connection=True;MultipleActiveResultSets=true;")
		{
		}

		public DbSet<Comment> Comments { get; set; }
		public DbSet<Publication> Publications { get; set; }

		protected override void OnModelCreating(DbModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.ComplexType<Address>();
		}
	}
}
