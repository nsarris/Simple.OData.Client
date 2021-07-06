using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simple.OData.Client.Extensions
{
    internal static class SessionExtensions
    {
        public static bool NamedKeyValuesMatchAnyKey(this ISession session, string entityCollectionName, IDictionary<string, object> namedKeyValues, out IEnumerable<KeyValuePair<string, object>> matchingNamedKeyValues, out bool isAlternateKey)
        {
            isAlternateKey = false;

            if (NamedKeyValuesMatchPrimaryKey(session, entityCollectionName, namedKeyValues, out matchingNamedKeyValues))
                return true;

            if (NamedKeyValuesMatchAlternateKey(session, entityCollectionName, namedKeyValues, out matchingNamedKeyValues))
            {
                isAlternateKey = true;
                return true;
            }

            return false;
        }

        public static bool NamedKeyValuesMatchPrimaryKey(this ISession session, string entityCollectionName, IDictionary<string, object> namedKeyValues, out IEnumerable<KeyValuePair<string, object>> matchingNamedKeyValues)
        {
            var keyNames = session.Metadata.GetDeclaredKeyPropertyNames(entityCollectionName).ToList();

            return NamedKeyValuesMatchKeyNames(namedKeyValues, session.Settings.NameMatchResolver, keyNames, out matchingNamedKeyValues);
        }

        public static bool NamedKeyValuesMatchAlternateKey(this ISession session, string entityCollectionName, IDictionary<string, object> namedKeyValues, out IEnumerable<KeyValuePair<string, object>> alternateKeyNamedValues)
        {
            alternateKeyNamedValues = null;

            var alternateKeys = session.Metadata.GetAlternateKeyPropertyNames(entityCollectionName);

            foreach (var alternateKey in alternateKeys)
            {
                if (NamedKeyValuesMatchKeyNames(namedKeyValues, session.Settings.NameMatchResolver, alternateKey, out alternateKeyNamedValues))
                    return true;
            }

            return false;
        }

        public static bool TryExtractAnyKeyFromNamedValues(this ISession session, string entityCollectionName, IDictionary<string, object> namedValues, out IEnumerable<KeyValuePair<string, object>> matchingNamedKeyValues, out bool isAlternateKey)
        {
            isAlternateKey = false;

            if (TryExtractPrimaryKeyFromNamedValues(session, entityCollectionName, namedValues, out matchingNamedKeyValues))
                return true;

            if (TryExtractAlternateKeyFromNamedValues(session, entityCollectionName, namedValues, out matchingNamedKeyValues))
            {
                isAlternateKey = true;
                return true;
            }

            return false;
        }

        public static bool TryExtractPrimaryKeyFromNamedValues(this ISession session, string entityCollectionName, IDictionary<string, object> namedValues, out IEnumerable<KeyValuePair<string, object>> matchingNamedKeyValues)
        {
            return NamedKeyValuesContainKeyNames(namedValues,
                session.Settings.NameMatchResolver,
                session.Metadata.GetDeclaredKeyPropertyNames(entityCollectionName),
                !session.Settings.SupportNullsAsKeyPropertyValues,
                out matchingNamedKeyValues);
        }

        public static bool TryExtractAlternateKeyFromNamedValues(this ISession session, string entityCollectionName, IDictionary<string, object> namedValues, out IEnumerable<KeyValuePair<string, object>> matchingNamedKeyValues)
        {
            matchingNamedKeyValues = null;

            var alternateKeys = session.Metadata.GetAlternateKeyPropertyNames(entityCollectionName);

            foreach (var alternateKey in alternateKeys)
            {
                if (NamedKeyValuesContainKeyNames(namedValues,
                    session.Settings.NameMatchResolver,
                    alternateKey,
                    !session.Settings.SupportNullsAsKeyPropertyValues,
                    out matchingNamedKeyValues))
                    return true;
            }

            return false;
        }

        private static bool NamedKeyValuesMatchKeyNames(IDictionary<string, object> namedKeyValues, INameMatchResolver resolver, IEnumerable<string> keyNames, out IEnumerable<KeyValuePair<string, object>> matchingNamedKeyValues)
        {
            matchingNamedKeyValues = null;
            if (namedKeyValues is null || keyNames is null)
                return false;

            if (keyNames.Count() == namedKeyValues.Count)
            {
                var tmpMatchingNamedKeyValues = new List<KeyValuePair<string, object>>();
                foreach (var keyProperty in keyNames)
                {
                    var namedKeyValue = namedKeyValues.FirstOrDefault(x => resolver.IsMatch(x.Key, keyProperty));
                    if (namedKeyValue.Key != null)
                    {
                        tmpMatchingNamedKeyValues.Add(new KeyValuePair<string, object>(keyProperty, namedKeyValue.Value));
                    }
                    else
                    {
                        break;
                    }
                }
                if (tmpMatchingNamedKeyValues.Count == keyNames.Count())
                {
                    matchingNamedKeyValues = tmpMatchingNamedKeyValues;
                    return true;
                }
            }

            return false;
        }

        //private static bool NamedKeyValuesContainKeyNames(IDictionary<string, object> namedKeyValues, INameMatchResolver resolver, IEnumerable<string> keyNames, bool ignoreNullValues, out IEnumerable<KeyValuePair<string, object>> matchingNamedKeyValues)
        //{
        //    matchingNamedKeyValues = null;
        //    if (namedKeyValues is null || keyNames is null)
        //        return false;

        //    var tmpMatchingNamedKeyValues = new List<KeyValuePair<string, object>>();
        //    foreach (var namedKeyValue in namedKeyValues)
        //    {
        //        var keyProperty = keyNames.FirstOrDefault(x => resolver.IsMatch(x, namedKeyValue.Key));
        //        if (keyProperty != null
        //            && (ignoreNullValues || namedKeyValue.Value != null))
        //        {
        //            tmpMatchingNamedKeyValues.Add(new KeyValuePair<string, object>(keyProperty, namedKeyValue.Value));
        //        }
        //    }
        //    if (tmpMatchingNamedKeyValues.Any())
        //    {
        //        matchingNamedKeyValues = tmpMatchingNamedKeyValues;
        //        return true;
        //    }

        //    return false;
        //}

        private static bool NamedKeyValuesContainKeyNames(IDictionary<string, object> namedKeyValues, INameMatchResolver resolver, IEnumerable<string> keyNames, bool ignoreNullValues, out IEnumerable<KeyValuePair<string, object>> matchingNamedKeyValues)
        {
            matchingNamedKeyValues = null;
            if (namedKeyValues is null || keyNames is null)
                return false;

            var tmpMatchingNamedKeyValues = new List<KeyValuePair<string, object>>();

            foreach (var prop in keyNames)
            {
                var foundValue = false;
                var valueIsNull = false;

                foreach (var keyValue in namedKeyValues)
                {
                    if (resolver.IsMatch(keyValue.Key, prop))
                    {
                        tmpMatchingNamedKeyValues.Add(new KeyValuePair<string, object>(prop, keyValue.Value));
                        foundValue = true;
                        valueIsNull = keyValue.Value == null;
                        break;
                    }
                }

                if (!foundValue
                    || (ignoreNullValues && valueIsNull))
                    return false;
            }

            matchingNamedKeyValues = tmpMatchingNamedKeyValues;
            return true;
        }
    }
}
