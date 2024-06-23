using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using Tests.Models;

namespace Tests.FakeDomain;

public class ReorderedContext : DbContext
{
	public ReorderedContext()
		: base(ConnectionStringsFixture.ConnectionStrings.SqlServer)
	{
		Database.DefaultConnectionFactory = new SqlConnectionFactory("System.Data.SqlServer");
		Database.SetInitializer(new CreateDatabaseIfNotExists<ReorderedContext>());

		Configuration.ValidateOnSaveEnabled = false;
		Configuration.LazyLoadingEnabled = false;
		Configuration.ProxyCreationEnabled = false;
		Configuration.AutoDetectChangesEnabled = false;
	}

	public IDbSet<ReorderedBlogPost> BlogPosts { get; set; } = null!;

	protected override void OnModelCreating(DbModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<ReorderedBlogPost>().ToTable("BlogPosts");
		modelBuilder.ComplexType<AuthorInfo>();
		modelBuilder.ComplexType<Address>();
	}
}
