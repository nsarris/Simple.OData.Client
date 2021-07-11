namespace Simple.OData.Client
{
    public interface INameMatchResolver
    {
        bool IsMatch(string actualName, string requestedName);
        bool IsEntityTypeMatch(string actualName, string requestedName);
    }
}