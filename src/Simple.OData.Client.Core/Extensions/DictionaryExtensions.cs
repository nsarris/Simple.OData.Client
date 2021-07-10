using System;
using System.Collections.Generic;

namespace Simple.OData.Client.Extensions
{
    internal static class DictionaryExtensions
    {
        public static object ToObject(this IDictionary<string, object> source, Type type, ISession session, bool dynamicObject = false)
        {
            return new ObjectDictionaryConverter(session).ToObject(source, type, dynamicObject);
        }

        public static T ToObject<T>(this IDictionary<string, object> source, ISession session, bool dynamicObject = false)
            where T : class
        {
            return new ObjectDictionaryConverter(session).ToObject<T>(source, dynamicObject);
        }
    }
}