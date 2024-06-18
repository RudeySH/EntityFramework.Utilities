using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using EntityFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests.FakeDomain;
using Tests.Models;

namespace Tests
{
	[TestClass]
	public class InsertTests
	{
		[TestMethod]
		public void InsertAll_InsertItems_WithTypeHierarchy()
		{
			using (var db = Context.Sql())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();

				var people = new List<Contact>
				{
					Contact.Build("FN1", "LN1", "Director"),
					Contact.Build("FN2", "LN2", "Associate"),
					Contact.Build("FN3", "LN3", "Vice President")
				};

				EFBatchOperation.For(db, db.People).InsertAll(people);
			}

			using (var db = Context.Sql())
			{
				var contacts = db.People.OfType<Contact>().OrderBy(c => c.FirstName).ToList();
				Assert.AreEqual(3, contacts.Count);
				Assert.AreEqual("FN1", contacts.First().FirstName);
				Assert.AreEqual("Director", contacts.First().Title);
			}
		}

		[TestMethod]
		public async Task InsertAllAsync_InsertItems_WithTypeHierarchy()
		{
			using (var db = Context.Sql())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();

				var people = new List<Contact>
				{
					Contact.Build("FN1", "LN1", "Director"),
					Contact.Build("FN2", "LN2", "Associate"),
					Contact.Build("FN3", "LN3", "Vice President")
				};

				await EFBatchOperation.For(db, db.People).InsertAllAsync(people);
			}

			using (var db = Context.Sql())
			{
				var contacts = db.People.OfType<Contact>().OrderBy(c => c.FirstName).ToList();
				Assert.AreEqual(3, contacts.Count);
				Assert.AreEqual("FN1", contacts.First().FirstName);
				Assert.AreEqual("Director", contacts.First().Title);
			}
		}

		[TestMethod]
		public void InsertAll_InsertItems_WithTypeHierarchyBase()
		{
			using (var db = Context.Sql())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();

				var people = new List<Person>
				{
					Person.Build("FN1", "LN1"),
					Person.Build("FN2", "LN2"),
					Person.Build("FN3", "LN3")
				};

				EFBatchOperation.For(db, db.People).InsertAll(people);
			}

			using (var db = Context.Sql())
			{
				var contacts = db.People.OrderBy(c => c.FirstName).ToList();
				Assert.AreEqual(3, contacts.Count);
				Assert.AreEqual("FN1", contacts.First().FirstName);
			}
		}

		[TestMethod]
		public async Task InsertAllAsync_InsertItems_WithTypeHierarchyBase()
		{
			using (var db = Context.Sql())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();

				var people = new List<Person>
				{
					Person.Build("FN1", "LN1"),
					Person.Build("FN2", "LN2"),
					Person.Build("FN3", "LN3")
				};

				await EFBatchOperation.For(db, db.People).InsertAllAsync(people);
			}

			using (var db = Context.Sql())
			{
				var contacts = await db.People.OrderBy(c => c.FirstName).ToListAsync();
				Assert.AreEqual(3, contacts.Count);
				Assert.AreEqual("FN1", contacts.First().FirstName);
			}
		}

		[TestMethod]
		public void InsertAll_InsertsItems()
		{
			using (var db = Context.Sql())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();

				var list = new List<BlogPost>
				{
					BlogPost.Create("T1"),
					BlogPost.Create("T2"),
					BlogPost.Create("T3")
				};

				EFBatchOperation.For(db, db.BlogPosts).InsertAll(list);
			}

			using (var db = Context.Sql())
			{
				Assert.AreEqual(3, db.BlogPosts.Count());
				Assert.AreEqual("m@m.com", db.BlogPosts.First().Author.Email);
			}
		}

		[TestMethod]
		public async Task InsertAllAsync_InsertsItems()
		{
			using (var db = Context.Sql())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();

				var list = new List<BlogPost>
				{
					BlogPost.Create("T1"),
					BlogPost.Create("T2"),
					BlogPost.Create("T3")
				};

				await EFBatchOperation.For(db, db.BlogPosts).InsertAllAsync(list);
			}

			using (var db = Context.Sql())
			{
				Assert.AreEqual(3, await db.BlogPosts.CountAsync());
				Assert.AreEqual("m@m.com", (await db.BlogPosts.FirstAsync()).Author.Email);
			}
		}

		[TestMethod]
		public void InsertAll_WithExplicitConnection_InsertsItems()
		{
			using (var db = Context.Sql())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();

				var list = new List<BlogPost>
				{
					BlogPost.Create("T1"),
					BlogPost.Create("T2"),
					BlogPost.Create("T3")
				};
				EFBatchOperation.For(db, db.BlogPosts).InsertAll(list, db.Database.Connection);
			}

			using (var db = Context.Sql())
			{
				Assert.AreEqual(3, db.BlogPosts.Count());
			}
		}

		[TestMethod]
		public async Task InsertAllAsync_WithExplicitConnection_InsertsItems()
		{
			using (var db = Context.Sql())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();

				var list = new List<BlogPost>
				{
					BlogPost.Create("T1"),
					BlogPost.Create("T2"),
					BlogPost.Create("T3")
				};
				await EFBatchOperation.For(db, db.BlogPosts).InsertAllAsync(list, db.Database.Connection);
			}

			using (var db = Context.Sql())
			{
				Assert.AreEqual(3, await db.BlogPosts.CountAsync());
			}
		}

		[TestMethod]
		public void InsertAll_WrongColumnOrder_InsertsItems()
		{
			using (var db = new ReorderedContext())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();
			}

			using (var db = Context.Sql())
			{
				var list = new List<BlogPost>
				{
					BlogPost.Create("T1"),
					BlogPost.Create("T2"),
					BlogPost.Create("T3")
				};

				EFBatchOperation.For(db, db.BlogPosts).InsertAll(list);
			}

			using (var db = Context.Sql())
			{
				Assert.AreEqual(3, db.BlogPosts.Count());
			}
		}

		[TestMethod]
		public async Task InsertAllAsync_WrongColumnOrder_InsertsItems()
		{
			using (var db = new ReorderedContext())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();
			}

			using (var db = Context.Sql())
			{
				var list = new List<BlogPost>
				{
					BlogPost.Create("T1"),
					BlogPost.Create("T2"),
					BlogPost.Create("T3")
				};

				await EFBatchOperation.For(db, db.BlogPosts).InsertAllAsync(list);
			}

			using (var db = Context.Sql())
			{
				Assert.AreEqual(3, await db.BlogPosts.CountAsync());
			}
		}

		[TestMethod]
		public void InsertAll_WrongColumnOrderAndRenamedColumn_InsertsItems()
		{
			using (var db = new RenamedAndReorderedContext())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();
				db.Database.ExecuteSqlCommand("drop table dbo.RenamedAndReorderedBlogPosts;");
				db.Database.ExecuteSqlCommand(RenamedAndReorderedBlogPost.CreateTableSql());
			}

			using (var db = new RenamedAndReorderedContext())
			{
				var list = new List<RenamedAndReorderedBlogPost>
				{
					RenamedAndReorderedBlogPost.Create("T1"),
					RenamedAndReorderedBlogPost.Create("T2"),
					RenamedAndReorderedBlogPost.Create("T3")
				};

				EFBatchOperation.For(db, db.BlogPosts).InsertAll(list);
			}

			using (var db = new RenamedAndReorderedContext())
			{
				Assert.AreEqual(3, db.BlogPosts.Count());
			}
		}

		[TestMethod]
		public async Task InsertAllAsync_WrongColumnOrderAndRenamedColumn_InsertsItems()
		{
			using (var db = new RenamedAndReorderedContext())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();
				await db.Database.ExecuteSqlCommandAsync("drop table dbo.RenamedAndReorderedBlogPosts;");
				await db.Database.ExecuteSqlCommandAsync(RenamedAndReorderedBlogPost.CreateTableSql());
			}

			using (var db = new RenamedAndReorderedContext())
			{
				var list = new List<RenamedAndReorderedBlogPost>
				{
					RenamedAndReorderedBlogPost.Create("T1"),
					RenamedAndReorderedBlogPost.Create("T2"),
					RenamedAndReorderedBlogPost.Create("T3")
				};

				await EFBatchOperation.For(db, db.BlogPosts).InsertAllAsync(list);
			}

			using (var db = new RenamedAndReorderedContext())
			{
				Assert.AreEqual(3, await db.BlogPosts.CountAsync());
			}
		}

		[TestMethod]
		public void InsertAll_NoProvider_UsesDefaultInsert()
		{
			string fallbackText = null;
			Configuration.DisableDefaultFallback = false;
			Configuration.Log = str => fallbackText = str;

			using (var db = Context.SqlCe())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();
			}

			var list = new List<BlogPost>
			{
					BlogPost.Create("T1"),
					BlogPost.Create("T2"),
					BlogPost.Create("T3")
				};

			using (var db = Context.SqlCe())
			{
				EFBatchOperation.For(db, db.BlogPosts).InsertAll(list);
			}

			using (var db = Context.SqlCe())
			{
				Assert.AreEqual(3, db.BlogPosts.Count());
			}

			Assert.IsNotNull(fallbackText);
		}

		[TestMethod]
		public async Task InsertAllAsync_NoProvider_UsesDefaultInsert()
		{
			string fallbackText = null;
			Configuration.DisableDefaultFallback = false;
			Configuration.Log = str => fallbackText = str;

			using (var db = Context.SqlCe())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();
			}

			var list = new List<BlogPost>
			{
				BlogPost.Create("T1"),
				BlogPost.Create("T2"),
				BlogPost.Create("T3")
			};

			using (var db = Context.SqlCe())
			{
				await EFBatchOperation.For(db, db.BlogPosts).InsertAllAsync(list);
			}

			using (var db = Context.SqlCe())
			{
				Assert.AreEqual(3, await db.BlogPosts.CountAsync());
			}

			Assert.IsNotNull(fallbackText);
		}

		[TestMethod]
		public void InsertAll_WithForeignKey()
		{
			int postId;
			using (var db = Context.Sql())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();

				var bp = BlogPost.Create("B1");
				db.BlogPosts.Add(bp);
				db.SaveChanges();
				postId = bp.Id;

				var comments = new List<Comment>
				{
					new Comment{Text = "C1", PostId = bp.Id },
					new Comment{Text = "C2", PostId = bp.Id }
				};

				EFBatchOperation.For(db, db.Comments).InsertAll(comments);
			}

			using (var db = Context.Sql())
			{
				Assert.AreEqual(2, db.Comments.Count());
				Assert.AreEqual(2, db.Comments.Count(c => c.PostId == postId));
			}
		}

		[TestMethod]
		public async Task InsertAllAsync_WithForeignKey()
		{
			int postId;
			using (var db = Context.Sql())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();

				var bp = BlogPost.Create("B1");
				db.BlogPosts.Add(bp);
				await db.SaveChangesAsync();
				postId = bp.Id;

				var comments = new List<Comment>
				{
					new Comment{Text = "C1", PostId = bp.Id },
					new Comment{Text = "C2", PostId = bp.Id }
				};

				await EFBatchOperation.For(db, db.Comments).InsertAllAsync(comments);
			}

			using (var db = Context.Sql())
			{
				Assert.AreEqual(2, await db.Comments.CountAsync());
				Assert.AreEqual(2, await db.Comments.CountAsync(c => c.PostId == postId));
			}
		}
	}
}
