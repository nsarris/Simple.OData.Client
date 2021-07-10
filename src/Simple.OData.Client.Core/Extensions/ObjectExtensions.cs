using System.Collections.Generic;

namespace Simple.OData.Client.Extensions
{
    internal static class ObjectExtensions
    {
        public static IDictionary<string, object> ToDictionary(this object source, ISession session)
        {
            return new ObjectDictionaryConverter(session).ToDictionary(source);
        }
    }
}