﻿using EntityFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Tests.FakeDomain;
using Tests.Models;

namespace Tests
{
	[TestClass]
	public class AttachAndModifyTest
	{
		[TestMethod]
		public void AttachAndModify_UpdateSingleItem_UpdatesDatabase()
		{
			SetupBasePosts();

			int postId;
			using (var db = Context.Sql())
			{
				postId = db.BlogPosts.First(b => b.Title == "T1").Id;
			}

			using (var db = Context.Sql())
			{
				db.AttachAndModify(new BlogPost { Id = postId, Author = new AuthorInfo { Address = new Address() } })
					.Set(x => x.Reads, 10);
				db.SaveChanges();
			}

			using (var db = Context.Sql())
			{
				var p2 = db.BlogPosts.First(b => b.Title == "T1");
				Assert.AreEqual(10, p2.Reads);
			}
		}

		[TestMethod]
		public void AttachAndModify_UpdateTwoItem_UpdatesDatabase()
		{
			SetupBasePosts();

			int postId;
			using (var db = Context.Sql())
			{
				postId = db.BlogPosts.First(b => b.Title == "T1").Id;
			}

			using (var db = Context.Sql())
			{
				db.AttachAndModify(new BlogPost { Id = postId, Author = new AuthorInfo { Address = new Address() } })
					.Set(x => x.Reads, 10)
					.Set(x => x.Title, "NewTitle");
				db.SaveChanges();
			}

			using (var db = Context.Sql())
			{
				var p2 = db.BlogPosts.First(b => b.Id == postId);
				Assert.AreEqual(10, p2.Reads);
				Assert.AreEqual("NewTitle", p2.Title);
			}
		}

		private static void SetupBasePosts()
		{
			using (var db = Context.Sql())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}

				db.Database.Create();

				var p = BlogPost.Create("T1");
				p.Reads = 2;
				db.BlogPosts.Add(p);
				db.BlogPosts.Add(BlogPost.Create("T2"));

				db.SaveChanges();
			}
		}
	}
}
