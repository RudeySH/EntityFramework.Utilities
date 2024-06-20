using ServiceStack.Text;

namespace Tests
{
	public static class ConnectionStringReader
	{
		private static ConnectionStrings? ConnectionStringsField;

		public static ConnectionStrings ConnectionStrings
		{
			get
			{
				if (ConnectionStringsField == null)
				{
					using var stream = File.OpenRead("connectionStrings.json");
					ConnectionStringsField = JsonSerializer.DeserializeFromStream<ConnectionStrings>(stream);
				}

				return ConnectionStringsField;
			}
		}
	}
}
