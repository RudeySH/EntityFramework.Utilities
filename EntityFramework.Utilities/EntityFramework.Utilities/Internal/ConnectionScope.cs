using System.Data;
using System.Data.Common;

namespace EntityFramework.Utilities.Internal;

internal readonly struct ConnectionScope : IDisposable
{
	private readonly DbConnection? connection;

	private ConnectionScope(DbConnection? connection)
	{
		this.connection = connection;
	}

	public static ConnectionScope EnsureOpen(DbConnection connection)
	{
		if (connection.State.HasFlag(ConnectionState.Open))
			return default;

		connection.Open();

		return new ConnectionScope(connection);
	}

	public static async Task<ConnectionScope> EnsureOpenAsync(DbConnection connection, CancellationToken cancellationToken)
	{
		if (connection.State.HasFlag(ConnectionState.Open))
			return default;

		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return new ConnectionScope(connection);
	}

	public void Dispose()
	{
		this.connection?.Close();
	}
}
