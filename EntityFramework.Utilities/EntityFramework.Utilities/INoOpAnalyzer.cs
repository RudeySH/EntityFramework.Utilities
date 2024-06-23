namespace EntityFramework.Utilities
{
	/// <summary>
	///     Provides analysis related to whether queries would perform no operation.
	/// </summary>
	public interface INoOpAnalyzer
	{
		/// <summary>
		///     Determines whether the given query would perform no operation. Used to guard against queries Entity
		///     Framework has optimized down to a dummy query against nothing that returns nothing.
		/// </summary>
		/// <param name="queryInformation">Information about the query.</param>
		/// <returns>Whether the query would perform no operation.</returns>
		bool QueryIsNoOp(QueryInformation queryInformation);
	}
}
