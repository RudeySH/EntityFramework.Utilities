using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using EntityFramework.Utilities;
using PerformanceTests.Models;

namespace PerformanceTests
{
	internal class Program
	{
		private static void Main()
		{
			BatchIteration(25);
			BatchIteration(25);
			NormalIteration(25);
			NormalIteration(25);
			BatchIteration(2500);
			NormalIteration(2500);
			BatchIteration(25000);
			NormalIteration(25000);
			BatchIteration(50000);
			//NormalIteration(50000);
			BatchIteration(100000);
			NormalIteration(100000);
		}

		[MethodImpl(MethodImplOptions.NoOptimization)]
		private static void NormalIteration(int count)
		{
			Console.WriteLine("Standard iteration with " + count + " entities");
			CreateAndWarmUp();
			var stopwatch = new Stopwatch();

			using (var db = new Context())
			{
				db.Configuration.AutoDetectChangesEnabled = false;
				db.Configuration.ValidateOnSaveEnabled = false;
				var comments = GetEntities(count).ToList();
				stopwatch.Start();

				foreach (var comment in comments)
				{
					db.Comments.Add(comment);
				}

				db.SaveChanges();
				stopwatch.Stop();
				Console.WriteLine("Insert entities: " + stopwatch.ElapsedMilliseconds + "ms");
			}

			using (var db = new Context())
			{
				db.Configuration.AutoDetectChangesEnabled = true;
				db.Configuration.ValidateOnSaveEnabled = false;
				stopwatch.Restart();
				foreach (var item in db.Comments.Where(c => c.Text == "a").ToList())
				{
					item.Reads++;
				}
				db.SaveChanges();
				stopwatch.Stop();
				Console.WriteLine("Update all entities with a: " + stopwatch.ElapsedMilliseconds + "ms");
			}

			using (var db = new Context())
			{
				db.Configuration.AutoDetectChangesEnabled = true;
				db.Configuration.ValidateOnSaveEnabled = false;
				var toUpdate = db.Comments.ToList();
				var rand = new Random();
				foreach (var item in toUpdate)
				{
					item.Reads = rand.Next(0, 9999999);
				}
				stopwatch.Restart();
				db.SaveChanges();

				stopwatch.Stop();
				Console.WriteLine("Update all with a random read: " + stopwatch.ElapsedMilliseconds + "ms");
			}

			using (var db = new Context())
			{
				db.Configuration.AutoDetectChangesEnabled = false;
				db.Configuration.ValidateOnSaveEnabled = false;
				stopwatch.Restart();
				foreach (var item in db.Comments.Where(c => c.Text == "a").ToList())
				{
					db.Comments.Remove(item);
				}
				db.SaveChanges();
				stopwatch.Stop();
				Console.WriteLine("delete all entities with a: " + stopwatch.ElapsedMilliseconds + "ms");
			}

			using (var db = new Context())
			{
				db.Configuration.AutoDetectChangesEnabled = false;
				db.Configuration.ValidateOnSaveEnabled = false;
				stopwatch.Restart();
				foreach (var item in db.Comments.ToList())
				{
					db.Comments.Remove(item);
				}
				db.SaveChanges();
				stopwatch.Stop();
				Console.WriteLine("delete all entities: " + stopwatch.ElapsedMilliseconds + "ms");
			}
		}

		[MethodImpl(MethodImplOptions.NoOptimization)]
		private static void BatchIteration(int count)
		{
			Console.WriteLine("Batch iteration with " + count + " entities");
			CreateAndWarmUp();
			var stopwatch = new Stopwatch();
			using (var db = new Context())
			{
				var comments = GetEntities(count).ToList();
				stopwatch.Start();
				EFBatchOperation.For(db, db.Comments).InsertAll(comments);
				stopwatch.Stop();
				Console.WriteLine("Insert entities: " + stopwatch.ElapsedMilliseconds + "ms");
			}

			using (var db = new Context())
			{
				stopwatch.Restart();
				EFBatchOperation.For(db, db.Comments).Where(x => x.Text == "a").Update(x => x.Reads, x => x.Reads + 1);
				stopwatch.Stop();
				Console.WriteLine("Update all entities with a: " + stopwatch.ElapsedMilliseconds + "ms");
			}

			using (var db = new Context())
			{
				var commentsFromDb = db.Comments.AsNoTracking().ToList();
				var rand = new Random();
				foreach (var item in commentsFromDb)
				{
					item.Reads = rand.Next(0, 9999999);
				}
				stopwatch.Restart();
				EFBatchOperation.For(db, db.Comments).UpdateAll(commentsFromDb, x => x.ColumnsToUpdate(c => c.Reads));
				stopwatch.Stop();
				Console.WriteLine("Bulk update all with a random read: " + stopwatch.ElapsedMilliseconds + "ms");
			}

			using (var db = new Context())
			{
				stopwatch.Restart();
				EFBatchOperation.For(db, db.Comments).Where(x => x.Text == "a").Delete();
				stopwatch.Stop();
				Console.WriteLine("delete all entities with a: " + stopwatch.ElapsedMilliseconds + "ms");
			}

			using (var db = new Context())
			{
				stopwatch.Restart();
				EFBatchOperation.For(db, db.Comments).Where(x => true).Delete();
				stopwatch.Stop();
				Console.WriteLine("delete all entities: " + stopwatch.ElapsedMilliseconds + "ms");
			}
		}

		private static void CreateAndWarmUp()
		{
			using (var db = new Context())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}
				db.Database.Create();

				//warmup
				db.Comments.Add(new Comment { Date = DateTime.Now, Address = new Address() });
				db.SaveChanges();
				db.Comments.Remove(db.Comments.First());
				db.SaveChanges();
			}
		}

		private static IEnumerable<Comment> GetEntities(int count)
		{
			return Enumerable.Repeat('a', count).Select((c, i) => new Comment
			{
				Text = ((char)(c + i % 25)).ToString(),
				Date = DateTime.Now.AddDays(i),
				Address = new Address
				{
					Line1 = "Street",
					ZipCode = "12345",
					Town = "Town"
				}
			});
		}
	}
}
