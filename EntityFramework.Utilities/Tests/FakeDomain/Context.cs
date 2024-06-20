using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using Tests.Models;

namespace Tests.FakeDomain
{
	public class Context : DbContext
	{
		private Context(string connectionString)
			: base(connectionString) { }

		public DbSet<BlogPost> BlogPosts { get; set; } = null!;

		public DbSet<Person> People { get; set; } = null!;

		public DbSet<Contact> Contacts { get; set; } = null!;

		public DbSet<PhoneNumber> PhoneNumbers { get; set; } = null!;

		public DbSet<Email> Emails { get; set; } = null!;

		public DbSet<Comment> Comments { get; set; } = null!;

		public DbSet<NumericTestObject> NumericTestsObjects { get; set; } = null!;

		public DbSet<MultiPkObject> MultiPkObjects { get; set; } = null!;

		public DbSet<ObjectWithComplexType> ObjectsWithComplexType { get; set; } = null!;

		protected override void OnModelCreating(DbModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.ComplexType<AuthorInfo>();
			modelBuilder.ComplexType<Address>();

			// Table per Type Hierarchy setup.
			modelBuilder.Entity<Person>()
				.Map<Person>(m => m.Requires("Type").HasValue("Person"))
				.Map<Contact>(m => m.Requires("Type").HasValue("Contact"));

			modelBuilder.Entity<MultiPkObject>().HasKey(x => new { x.Pk1, x.Pk2 });

			modelBuilder.Entity<BlogPost>().Property(x => x.ShortTitle).HasMaxLength(100);

			var n = modelBuilder.Entity<NumericTestObject>();
			n.Property(x => x.NumericType).HasColumnType("numeric");
		}

		public static Context Sql()
		{
			Database.SetInitializer<Context>(null);
			Database.DefaultConnectionFactory = new SqlConnectionFactory("System.Data.SqlServer");

			var ctx = new Context(ConnectionStringsFixture.ConnectionStrings.SqlServer);
			ctx.Configuration.ValidateOnSaveEnabled = false;
			ctx.Configuration.LazyLoadingEnabled = false;
			ctx.Configuration.ProxyCreationEnabled = false;
			ctx.Configuration.AutoDetectChangesEnabled = false;

			return ctx;
		}

		public static Context SqlCe()
		{
			Database.SetInitializer<Context>(null);
			Database.DefaultConnectionFactory = new SqlCeConnectionFactory("System.Data.SqlServerCe.4.0");

			var ctx = new Context(ConnectionStringsFixture.ConnectionStrings.SqlCe);
			ctx.Configuration.ValidateOnSaveEnabled = false;
			ctx.Configuration.LazyLoadingEnabled = false;
			ctx.Configuration.ProxyCreationEnabled = false;
			ctx.Configuration.AutoDetectChangesEnabled = false;

			return ctx;
		}
	}
}
