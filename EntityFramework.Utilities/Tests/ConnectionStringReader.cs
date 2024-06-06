using ServiceStack.Text;
using System.IO;

namespace Tests
{
	public static class ConnectionStringReader
	{
		private static ConnectionStrings _connectionsStrings;

		public static ConnectionStrings ConnectionStrings
		{
			get
			{
				if (_connectionsStrings == null)
				{
					using (var stream = File.OpenRead("connectionStrings.json"))
					{
						_connectionsStrings = JsonSerializer.DeserializeFromStream<ConnectionStrings>(stream);
					}
				}

				return _connectionsStrings;
			}
		}
	}
}
