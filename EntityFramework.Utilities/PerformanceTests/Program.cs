namespace PerformanceTests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using EntityFramework.Utilities;
	using PerformanceTests.Models;

	/// <summary>
	/// Run-able class used to test the performance of the EntityFramework.Utilities package
	/// </summary>
	internal static class Program
	{
		private static void Main()
		{
			foreach (var test in new[] { 25, 100, 250, 500, 1000, 2500, 5000, 25000, 50000, 10000, 100000 })
			{
				ExecuteTest(test);
			}
			Console.WriteLine("Completed benchmarks");
			Console.ReadLine();
		}

		/// <summary>
		/// Executes the test based on the given count.
		/// </summary>
		/// <param name="count">The amount of entities the test should use</param>
		private static void ExecuteTest(int count)
		{
			Console.WriteLine($"Standard iteration with " + count + " entities");
			CreateDatabase();
			WarmUpConntection();
			BatchIteration(count);
			Console.WriteLine();

			CreateDatabase();
			WarmUpConntection();
			NormalIteration(count);
			Console.WriteLine();
		}

		/// <summary>
		/// Does a testing iteration using default Entity Framework implementations.
		/// This methods has optimizations disabled to prevent the JIT from in-lining or removing results.
		/// The contexts within the test have been optimized to not track changes where possible.
		/// </summary>
		/// <param name="count">The amount of objects that will be edited.</param>
		[MethodImpl(MethodImplOptions.NoOptimization)]
		private static void NormalIteration(int count)
		{
			var stopwatch = new Stopwatch();

			using (var db = new Context())
			{
				db.Configuration.AutoDetectChangesEnabled = false;
				db.Configuration.ValidateOnSaveEnabled = false;

				foreach (var comment in GetEntities(count))
				{
					db.Comments.Add(comment);
				}

				stopwatch.Start();
				db.SaveChanges();
				stopwatch.Stop();
				LogMessage("EF6", "Insert entities", count, stopwatch.ElapsedMilliseconds);
			}

			using (var db = new Context())
			{
				db.Configuration.AutoDetectChangesEnabled = true;
				db.Configuration.ValidateOnSaveEnabled = false;
				foreach (var item in db.Comments.Where(c => c.Text == "a").ToList())
				{
					item.Reads++;
				}

				stopwatch.Restart();
				db.SaveChanges();
				stopwatch.Stop();
				LogMessage("EF6", "Update all entities with 'a'", count, stopwatch.ElapsedMilliseconds);
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
				LogMessage("EF6", "Update all with a random read", count, stopwatch.ElapsedMilliseconds);
			}

			using (var db = new Context())
			{
				db.Configuration.AutoDetectChangesEnabled = false;
				db.Configuration.ValidateOnSaveEnabled = false;
				foreach (var item in db.Comments.Where(c => c.Text == "a").ToList())
				{
					db.Comments.Remove(item);
				}

				stopwatch.Restart();
				db.SaveChanges();
				stopwatch.Stop();
				LogMessage("EF6", "Delete all entities with 'a'", count, stopwatch.ElapsedMilliseconds);
			}

			using (var db = new Context())
			{
				db.Configuration.AutoDetectChangesEnabled = false;
				db.Configuration.ValidateOnSaveEnabled = false;
				foreach (var item in db.Comments.ToList())
				{
					db.Comments.Remove(item);
				}

				stopwatch.Restart();
				db.SaveChanges();
				stopwatch.Stop();
				LogMessage("EF6", "Delete all entities", count, stopwatch.ElapsedMilliseconds);
			}
		}

		/// <summary>
		/// Does a testing iteration using Entity Framework Utilities.
		/// This methods has optimizations disabled to prevent the JIT from in-lining or removing results.
		/// </summary>
		/// <param name="count">The amount of objects that will be edited.</param>
		[MethodImpl(MethodImplOptions.NoOptimization)]
		private static void BatchIteration(int count)
		{
			var stopwatch = new Stopwatch();
			using (var db = new Context())
			{
				var comments = GetEntities(count);
				stopwatch.Start();
				EFBatchOperation.For(db, db.Comments).InsertAll(comments);
				stopwatch.Stop();
				LogMessage("EFU", "Insert entities", count, stopwatch.ElapsedMilliseconds);
			}

			using (var db = new Context())
			{
				stopwatch.Restart();
				EFBatchOperation.For(db, db.Comments).Where(x => x.Text == "a").Update(x => x.Reads, x => x.Reads + 1);
				stopwatch.Stop();
				LogMessage("EFU", "Update all entities with a", count, stopwatch.ElapsedMilliseconds);
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
				LogMessage("EFU", "Bulk update all with a random read", count, stopwatch.ElapsedMilliseconds);
			}

			using (var db = new Context())
			{
				stopwatch.Restart();
				EFBatchOperation.For(db, db.Comments).Where(x => x.Text == "a").Delete();
				stopwatch.Stop();
				LogMessage("EFU", "Delete all entities with 'a'", count, stopwatch.ElapsedMilliseconds);
			}

			using (var db = new Context())
			{
				stopwatch.Restart();
				EFBatchOperation.For(db, db.Comments).Where(_ => true).Delete();
				stopwatch.Stop();
				LogMessage("EFU", "Delete all entities", count, stopwatch.ElapsedMilliseconds);
			}
		}

		/// <summary>
		/// Creates the databases based off the <see cref="Context"/> class.
		/// When the database already exists it will be deleted.
		/// </summary>
		private static void CreateDatabase()
		{
			using (var db = new Context())
			{
				if (db.Database.Exists())
				{
					db.Database.Delete();
				}
				db.Database.Create();
			}
		}

		/// <summary>
		///	Creates an <see cref="IEnumerable{Comment}"/> an fills it with test data.
		/// </summary>
		/// <param name="count">The amount of items wanted in the IEnumerable</param>
		/// <returns>An IEnumerable of the <see cref="Comment"/> class.</returns>
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

		/// <summary>
		/// Prepares the database connection by requesting random data.
		/// </summary>
		private static void WarmUpConntection()
		{
			using (var db = new Context())
			{
				var comment = db.Comments.Add(new Comment { Text = "Test", Date = DateTime.Now, Address = new Address() });
				db.SaveChanges();
				db.Comments.Remove(comment);
				db.SaveChanges();
			}
		}

		private static void LogMessage(string testMethod, string action, int iterations, long time)
		{
			Console.WriteLine(string.Format("[{0}] {1} {2} iterations took {3} ms", testMethod, action, iterations, time));
		}
	}
}
