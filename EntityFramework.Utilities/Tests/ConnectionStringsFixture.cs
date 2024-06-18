using System;
using System.Data.SqlServerCe;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServiceStack.Text;
using Testcontainers.MsSql;

namespace Tests
{
	/// <summary>
	///     Encapsulates access to the connection strings to use for all tests.
	/// </summary>
	/// <remarks>
	///     See <a href="https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2008/ms245278(v=vs.90)">the docs</a>
	///     for an explanation of the MSTest lifecycle hooks used.
	/// </remarks>
	[TestClass]
	public static class ConnectionStringsFixture
	{
		/// <summary>
		///     The connection strings to use for tests.
		/// </summary>
		public static ConnectionStrings ConnectionStrings { get; private set; }

		/// <summary>
		///     The database name to use for connection strings to generated SQL Server databases.
		/// </summary>
		private const string TestDatabaseName = "BatchTests";

		/// <summary>
		///     The test settings as declared by the user.
		/// </summary>
		private static TestSettings _testSettings;

		/// <summary>
		///     If requested, the Docker container for the local SQL Server database to use for testing.
		/// </summary>
		private static MsSqlContainer _msSqlContainer;

		/// <summary>
		///     If requested, the encapsulation of the local SQL Server Compact Edition database to use for testing.
		/// </summary>
		private static SqlCeEngine _sqlCeEngine;

		/// <summary>
		///     The path to the file that <see cref="_sqlCeEngine"/> stores its database in.
		/// </summary>
		private static string _sqlCeFilePath;

		/// <summary>
		///     Initializes this fixture for use across all tests. Called by MSTest before any tests run.
		/// </summary>
		/// <param name="context">Contains information provided to unit tests by the unit testing framework.</param>
		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext context)
		{
			// Get the user-declared test settings.
			using (var testSettingsFileStream = File.OpenRead("testSettings.json"))
			{
				_testSettings = JsonSerializer.DeserializeFromStream<TestSettings>(testSettingsFileStream);
			}

			// If the user chose the connection string database mode, then simply fetch the user-declared connection
			// strings.
			if (_testSettings.DatabaseMode.Equals("connectionStringConfig", StringComparison.OrdinalIgnoreCase))
			{
				using (var connectionStringsFileStream = File.OpenRead("connectionStrings.json"))
				{
					ConnectionStrings = JsonSerializer.DeserializeFromStream<ConnectionStrings>(connectionStringsFileStream);
				}
			}
			// If the user chose the generated database mode, then generate databases and get store their connection
			// strings.
			else if (_testSettings.DatabaseMode.Equals("generated", StringComparison.OrdinalIgnoreCase))
			{
				// A GUID for the generated databases.
				string guid = Guid.NewGuid().ToString("N");

				// Build a Docker container for a local SQL Server database.
				_msSqlContainer = new MsSqlBuilder()
					.WithName($"efutilities-testsqldb_{guid}")
					.Build();

				// Start the Docker container for the SQL Server database.
				_msSqlContainer.StartAsync().GetAwaiter().GetResult();

				// Determine where the SQL Server CE database file is going to go.
				_sqlCeFilePath = $"efutilities-testsqlcedb_{guid}.sdf";

				// Create a SQL Server CE engine given the path to the database file.
				_sqlCeEngine = new SqlCeEngine($"DataSource=\"{_sqlCeFilePath}\"");

				// Initialize the SQL Server CE database.
				_sqlCeEngine.CreateDatabase();

				// Get and store the local databases' connection strings.
				ConnectionStrings = new ConnectionStrings
				{
					SqlServer = ReplaceDatabaseInConnectionString(_msSqlContainer.GetConnectionString(), MsSqlBuilder.DefaultDatabase, TestDatabaseName),
					SqlCe = _sqlCeEngine.LocalConnectionString
				};
			}
			// If the user chose no database mode, then throw.
			else if (_testSettings.DatabaseMode == null)
			{
				throw new InvalidOperationException("testSettings.json's databaseMode setting is not set.");
			}
			// If the user chose an invalid database mode, then throw
			else
			{
				throw new InvalidOperationException(
					$"testSettings.json's databaseMode setting is set to an invalid value: '{_testSettings.DatabaseMode}'.");
			}
		}

		/// <summary>
		///     Cleans up this fixture once all tests are done. Called by MSTest after all tests run.
		/// </summary>
		[AssemblyCleanup]
		public static void AssemblyCleanup()
		{
			// Erase the connection strings.
			ConnectionStrings = null;

			// Stop the Docker container for the SQL Server database if it exists.
			_msSqlContainer?.StopAsync().GetAwaiter().GetResult();

			// Dispose the engine for the SQL Server CE database if it exists.
			_sqlCeEngine?.Dispose();

			// Delete the file where the SQL Server CE database is stored if it exists.
			if (!(_sqlCeFilePath is null) && File.Exists(_sqlCeFilePath))
			{
				File.Delete(_sqlCeFilePath);
			}
		}

		/// <summary>
		///     Given a SQL Server database connection string, replaces a database setting with another if it exists.
		/// </summary>
		/// <param name="connectionString">The connection string whose database setting to replace.</param>
		/// <param name="currentDatabase">The database setting to replace if it exists.</param>
		/// <param name="newDatabase">The database setting to replace <paramref name="currentDatabase"/> with.</param>
		/// <returns>The connection string with the database setting replaced (if at all).</returns>
		private static string ReplaceDatabaseInConnectionString(string connectionString, string currentDatabase, string newDatabase)
		{
			string currentDatabaseSetting = $"Database={currentDatabase}";
			string newDatabaseSetting = $"Database={newDatabase}";

			return connectionString.Replace(currentDatabaseSetting, newDatabaseSetting);
		}
	}
}
