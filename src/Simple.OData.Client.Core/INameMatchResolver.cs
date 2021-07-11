namespace Simple.OData.Client
{
    public interface INameMatchResolver
    {
        /// <summary>
        /// Specifies if resolver is strict to optimize performance.
        /// </summary>
        bool IsStrict { get; }

        /// <summary>
        /// Returns true if names match.
        /// </summary>
        /// <param name="actualName"></param>
        /// <param name="requestedName"></param>
        /// <returns></returns>
        bool IsMatch(string actualName, string requestedName);

        /// <summary>
        /// Returns true is entity names match.
        /// </summary>
        /// <param name="actualName"></param>
        /// <param name="requestedName"></param>
        /// <returns></returns>
        bool IsEntityTypeMatch(string actualName, string requestedName);
    }
}