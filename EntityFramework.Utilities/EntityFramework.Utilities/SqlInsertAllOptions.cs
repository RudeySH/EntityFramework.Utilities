﻿using System.Data.SqlClient;

namespace EntityFramework.Utilities;

/// <summary>
///     The options that can be specified for the InsertAll operation on SQL Server databases.
/// </summary>
public class SqlInsertAllOptions : InsertAllOptions
{
	/// <summary>
	///     The batch size for <see cref="SqlBulkCopy"/>.
	/// </summary>
	/// <value>Default: 4000</value>
	public int BatchSize { get; set; } = 4000;

	/// <summary>
	///     The options for <see cref="SqlBulkCopy"/>.
	/// </summary>
	/// <value>Default: <see cref="SqlBulkCopyOptions.Default" /></value>
	public SqlBulkCopyOptions SqlBulkCopyOptions { get; set; } = SqlBulkCopyOptions.Default;
}
