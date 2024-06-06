using PerformanceTests.Models;
using System.Data.Entity;

namespace PerformanceTests
{
	public class Context : DbContext
	{
		public Context()
			: base("Data Source=./; Initial Catalog=EFUTest; Integrated Security=SSPI; MultipleActiveResultSets=True")
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
