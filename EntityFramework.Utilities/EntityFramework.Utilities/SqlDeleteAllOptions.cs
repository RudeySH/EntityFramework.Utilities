using System.Data.SqlClient;

namespace EntityFramework.Utilities;

/// <summary>
///     The options that can be specified for the DeleteAll operation on SQL Server databases.
/// </summary>
public class SqlDeleteAllOptions : DeleteAllOptions
{
	/// <summary>
	///     The batch size for <see cref="SqlBulkCopy"/>.
	///     Before the target rows are deleted, SqlBulkCopy is used to populate a temporary table with the keys of
	///     the rows to delete.
	/// </summary>
	/// <value>Default: 4000</value>
	public int BatchSize { get; set; } = 4000;

	/// <summary>
	///     The options for <see cref="SqlBulkCopy"/>.
	/// </summary>
	/// <value>Default: <see cref="SqlBulkCopyOptions.Default" /></value>
	public SqlBulkCopyOptions SqlBulkCopyOptions { get; set; } = SqlBulkCopyOptions.Default;
}
