using System.Data.SqlClient;

namespace EntityFramework.Utilities;

public class SqlUpdateAllOptions : UpdateAllOptions
{
	#region Public Properties

	/// <summary>
	/// The batch size for <see cref="SqlBulkCopy"/>.
	/// Before the target table is updated, SqlBulkCopy is used to populate a temporary table.
	/// </summary>
	/// <value>Default: 4000</value>
	public int BatchSize { get; set; } = 4000;

	/// <summary>
	/// If set to <see langword="true"/>, the UpdateAll operation deletes all records that were not updated.
	/// If either <see cref="DeleteIfNotMatched"/> or <see cref="InsertIfNotMatched"/> is set to true, the UpdateAll
	/// operation executes a MERGE statement instead of an UPDATE statement.
	/// </summary>
	/// <value>Default: <see langword="false" /></value>
	public bool DeleteIfNotMatched { get; set; }

	/// <summary>
	/// If set to <see langword="true"/>, the UpdateAll operation upserts the items; items that can not be matched
	/// with an existing record are inserted, and items that can be matched are updated.
	/// If either <see cref="DeleteIfNotMatched"/> or <see cref="InsertIfNotMatched"/> is set to true, the UpdateAll
	/// operation executes a MERGE statement instead of an UPDATE statement.
	/// </summary>
	/// <value>Default: <see langword="false" /></value>
	public bool InsertIfNotMatched { get; set; }

	/// <summary>
	/// The options for <see cref="SqlBulkCopy"/>.
	/// Before the target table is updated, SqlBulkCopy is used to populate a temporary table.
	/// </summary>
	/// <value>Default: <see cref="SqlBulkCopyOptions.TableLock" /></value>
	public SqlBulkCopyOptions SqlBulkCopyOptions { get; set; } = SqlBulkCopyOptions.TableLock;

	/// <summary>
	/// The table hints used for the UPDATE or MERGE statement.
	/// </summary>
	/// <value>Default: []</value>
	public string[] TableHints { get; set; } = [];

	#endregion Public Properties
}
