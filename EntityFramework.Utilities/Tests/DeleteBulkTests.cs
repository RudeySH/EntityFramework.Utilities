using EntityFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests.FakeDomain;
using Tests.Models;

namespace Tests;

[TestClass]
public class DeleteBulkTests
{
	[TestMethod]
	public void DeleteAll_DeletesGivenItems()
	{
		Setup();

		using (var db = Context.Sql())
		{
			var toDelete = db.BlogPosts.Where(p => p.Title != "T2").ToList();

			var result = EFBatchOperation.For(db, db.BlogPosts).DeleteAll(toDelete);

			Assert.AreEqual(2, result);
		}

		using (var db = Context.Sql())
		{
			var remaining = db.BlogPosts.ToList();
			Assert.AreEqual(1, remaining.Count);
			Assert.AreEqual("T2", remaining[0].Title);
		}
	}

	[TestMethod]
	public async Task DeleteAllAsync_DeletesGivenItems()
	{
		Setup();

		using (var db = Context.Sql())
		{
			var toDelete = db.BlogPosts.Where(p => p.Title != "T2").ToList();

			var result = await EFBatchOperation.For(db, db.BlogPosts).DeleteAllAsync(toDelete);

			Assert.AreEqual(2, result);
		}

		using (var db = Context.Sql())
		{
			var remaining = db.BlogPosts.ToList();
			Assert.AreEqual(1, remaining.Count);
			Assert.AreEqual("T2", remaining[0].Title);
		}
	}

	[TestMethod]
	public void DeleteAll_CanDeleteMultiPk()
	{
		using (var db = Context.Sql())
		{
			if (db.Database.Exists())
			{
				db.Database.ForceDelete();
			}
			db.Database.Create();

			var guid = Guid.NewGuid();
			var list = new List<MultiPkObject>
			{
				new MultiPkObject { Pk1 = guid, Pk2 = 0 },
				new MultiPkObject { Pk1 = guid, Pk2 = 1 },
			};

			EFBatchOperation.For(db, db.MultiPkObjects).InsertAll(list);
		}

		using (var db = Context.Sql())
		{
			var toDelete = db.MultiPkObjects.Where(x => x.Pk2 == 0).ToList();

			EFBatchOperation.For(db, db.MultiPkObjects).DeleteAll(toDelete);
		}

		using (var db = Context.Sql())
		{
			var items = db.MultiPkObjects.ToList();
			Assert.AreEqual(1, items.Count);
			Assert.AreEqual(1, items[0].Pk2);
		}
	}

	private static void Setup()
	{
		using var db = Context.Sql();
		if (db.Database.Exists())
		{
			db.Database.ForceDelete();
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
}
