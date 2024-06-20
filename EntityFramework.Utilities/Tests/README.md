# Running Tests

## Database Connections

Configure how databases are connected to in `testSettings.json`'s `databaseMode` setting. Valid options are:

- `connectionStringConfig` (default) &ndash; Use the connection strings declared in `connectionStrings.json`.
- `generated` &ndash; Spin up database instances automatically. Generate local
[Testcontainers](https://dotnet.testcontainers.org/) (Docker Desktop installation required for spinning up database
instances) for the SQL Server database and generate local [`.sdf` files](https://stackoverflow.com/a/1487865/8076767)
(SQL Server Compact 4.0 installation required for generating the database file) for the SQL Server Compact Edition
database.
