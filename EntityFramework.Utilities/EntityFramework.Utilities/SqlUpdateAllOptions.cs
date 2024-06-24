using System.Data;
using System.Data.SqlClient;

namespace EntityFramework.Utilities;

/// <summary>
///     The options that can be specified for the UpdateAll operation on SQL Server databases.
/// </summary>
public class SqlUpdateAllOptions : UpdateAllOptions
{
	#region Public Properties

	/// <summary>
	///     The batch size for <see cref="SqlBulkCopy"/>.
	///     Before the target table is updated, SqlBulkCopy is used to populate a temporary table.
	/// </summary>
	/// <value>Default: 4000</value>
	public int BatchSize { get; set; } = 4000;

	/// <summary>
	///     If set to <see langword="true"/>, the UpdateAll operation deletes all records that were not updated.
	///     If either <see cref="DeleteIfNotMatched"/> or <see cref="InsertIfNotMatched"/> is set to true,
	///     the UpdateAll operation executes a MERGE statement instead of an UPDATE statement.
	/// </summary>
	/// <value>Default: <see langword="false" /></value>
	public bool DeleteIfNotMatched { get; set; }

	/// <summary>
	///     If set to <see langword="true"/>, the UpdateAll operation upserts the items; items that can not be matched
	///     with an existing record are inserted, and items that can be matched are updated.
	///     If either <see cref="DeleteIfNotMatched"/> or <see cref="InsertIfNotMatched"/> is set to true,
	///     the UpdateAll operation executes a MERGE statement instead of an UPDATE statement.
	/// </summary>
	/// <value>Default: <see langword="false" /></value>
	public bool InsertIfNotMatched { get; set; }

	/// <summary>
	///     The options for <see cref="SqlBulkCopy"/>.
	///     Before the target table is updated, SqlBulkCopy is used to populate a temporary table.
	/// </summary>
	/// <value>Default: <see cref="SqlBulkCopyOptions.TableLock" /></value>
	public SqlBulkCopyOptions SqlBulkCopyOptions { get; set; } = SqlBulkCopyOptions.TableLock;

	/// <summary>
	///     The table hints used for the UPDATE or MERGE statement.
	/// </summary>
	/// <value>Default: []</value>
	public string[] TableHints { get; set; } = [];

	/// <summary>
	///     The isolation level used for the transaction, if <see cref="WrapInTransaction"/> is set to
	///     <see langword="true"/>.
	/// </summary>
	/// <value>Default: <see cref="IsolationLevel.Unspecified"/></value>
	public IsolationLevel TransactionIsolationLevel { get; set; } = IsolationLevel.Unspecified;

	/// <summary>
	///     If set to <see langword="true"/>, the UpdateAll operation creates and commits a transaction.
	/// </summary>
	public bool WrapInTransaction { get; set; }

	#endregion Public Properties
}
