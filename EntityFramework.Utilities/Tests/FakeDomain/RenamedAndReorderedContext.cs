using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using Tests.Models;

namespace Tests.FakeDomain
{
	public class RenamedAndReorderedContext : DbContext
	{
		public RenamedAndReorderedContext()
			: base(ConnectionStringsFixture.ConnectionStrings.SqlServer)
		{
			Database.DefaultConnectionFactory = new SqlConnectionFactory("System.Data.SqlServer");
			Database.SetInitializer(new CreateDatabaseIfNotExists<RenamedAndReorderedContext>());

			Configuration.ValidateOnSaveEnabled = false;
			Configuration.LazyLoadingEnabled = false;
			Configuration.ProxyCreationEnabled = false;
			Configuration.AutoDetectChangesEnabled = false;
		}

		public DbSet<RenamedAndReorderedBlogPost> BlogPosts { get; set; } = null!;

		protected override void OnModelCreating(DbModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<RenamedAndReorderedBlogPost>().ToTable("RenamedAndReorderedBlogPosts");
			modelBuilder.Entity<RenamedAndReorderedBlogPost>().Property(x => x.Created).HasColumnName("Created2");
			modelBuilder.Entity<RenamedAndReorderedBlogPost>().Property(x => x.Reads).HasColumnName("Reads2");
		}

		public static void SetupTestDb()
		{
			using var db = new RenamedAndReorderedContext();

			if (db.Database.Exists())
			{
				db.Database.Delete();
			}

			db.Database.Create();
			db.Database.ExecuteSqlCommand("drop table dbo.RenamedAndReorderedBlogPosts;");
			db.Database.ExecuteSqlCommand(RenamedAndReorderedBlogPost.CreateTableSql());
		}
	}
}
