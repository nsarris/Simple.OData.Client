using System;
using System.Collections.Generic;
using System.Linq;

using Simple.OData.Client.Extensions;

#pragma warning disable 1591

namespace Simple.OData.Client
{
    public class ResolvedCommand
    {
        private readonly ISession _session;

        internal ResolvedCommand(FluentCommand command, ISession session)
        {
            _session = session;
            Details = new FluentCommandDetails(command.Details);

            ResolveCollectionName(command.Details);
            ResolveDerivedCollectionName(command.Details);
            ResolveLinkName(command.Details);
            ResolveEntityCollection(command.Details);
            ResolveKeys(command.Details);
            ResolveFilter(command.Details);
            ResolveEntryData(command.Details);
        }

        internal FluentCommandDetails Details { get; private set; }

        internal EntityCollection EntityCollection { get; private set; }

        public string QualifiedEntityCollectionName
        {
            get
            {
                var entityCollection = this.EntityCollection;
                return entityCollection.BaseEntityCollection == null
                    ? entityCollection.Name
                    : $"{entityCollection.BaseEntityCollection.Name}/{_session.Metadata.GetQualifiedTypeName(entityCollection.Name)}";
            }
        }

        public string DynamicPropertiesContainerName => Details.DynamicPropertiesContainerName;

        private void ResolveCollectionName(FluentCommandDetails details)
        {
            if (Details.CollectionName == null && !ReferenceEquals(details.CollectionExpression, null))
            {
                var collectionName = details.CollectionExpression.AsString(_session);
                var items = collectionName.Split('/');
                if (items.Count() > 1)
                {
                    Details.CollectionName = items[0];
                    Details.DerivedCollectionName = items[1];
                }
                else
                {
                    Details.CollectionName = collectionName;
                }
            }
        }

        private void ResolveDerivedCollectionName(FluentCommandDetails details)
        {
            if (Details.DerivedCollectionName == null && !ReferenceEquals(details.DerivedCollectionExpression, null))
            {
                var derivedCollectionName = details.DerivedCollectionExpression.AsString(_session);
                Details.DerivedCollectionName = derivedCollectionName;
            }
        }

        private void ResolveLinkName(FluentCommandDetails details)
        {
            if (Details.LinkName == null && !ReferenceEquals(details.LinkExpression, null))
            {
                Details.LinkName = details.LinkExpression.AsString(_session);
            }
        }

        private void ResolveEntityCollection(FluentCommandDetails details)
        {
            if (string.IsNullOrEmpty(Details.CollectionName) && string.IsNullOrEmpty(Details.LinkName))
            {
                this.EntityCollection = null;
            }
            else
            {
                EntityCollection entityCollection;
                if (!string.IsNullOrEmpty(Details.LinkName))
                {
                    var parent = new FluentCommand(Details.Parent).Resolve(_session);
                    var collectionName = _session.Metadata.GetNavigationPropertyPartnerTypeName(
                        parent.EntityCollection.Name, Details.LinkName);
                    entityCollection = _session.Metadata.GetEntityCollection(collectionName);
                }
                else
                {
                    entityCollection = _session.Metadata.GetEntityCollection(Details.CollectionName);
                }

                this.EntityCollection =
                    string.IsNullOrEmpty(Details.DerivedCollectionName)
                    ? entityCollection
                    : _session.Metadata.GetDerivedEntityCollection(entityCollection, Details.DerivedCollectionName);
            }
        }

        private void ResolveKeys(FluentCommandDetails details)
        {
            var namedKeyValues =
                details.KeyValues != null && details.KeyValues.Count == 1 &&
                _session.TypeCache.IsAnonymousType(details.KeyValues.First().GetType())
                    ? _session.TypeCache.ToDictionary(details.KeyValues.First())
                    : details.NamedKeyValues;

            if (namedKeyValues != null)
            {
                if (_session.NamedKeyValuesMatchAnyKey(this.EntityCollection.Name, namedKeyValues, out var matchingKey, out var isAlternateKey))
                {
                    Details.NamedKeyValues = matchingKey.ToDictionary();
                    Details.IsAlternateKey = isAlternateKey;
                }
                else if (_session.TryExtractAnyKeyFromNamedValues(this.EntityCollection.Name, namedKeyValues, out var containedKey, out isAlternateKey))
                {
                    Details.NamedKeyValues = containedKey.ToDictionary();
                    Details.IsAlternateKey = isAlternateKey;
                }
                else
                {
                    Details.NamedKeyValues = null;
                }
                Details.KeyValues = null;
            }
        }

        private void ResolveFilter(FluentCommandDetails details)
        {
            if (Details.Filter == null && !ReferenceEquals(details.FilterExpression, null))
            {
                Details.NamedKeyValues = TryInterpretFilterExpressionAsKey(details.FilterExpression, out var isAlternateKey);
                Details.IsAlternateKey = isAlternateKey;

                if (Details.NamedKeyValues == null)
                {
                    var entityCollection = this.EntityCollection;
                    if (details.HasFunction)
                    {
                        var collection = _session.Metadata.GetFunctionReturnCollection(details.FunctionName);
                        if (collection != null)
                        {
                            entityCollection = collection;
                        }
                    }
                    Details.Filter = details.FilterExpression.Format(
                        new ExpressionContext(_session, entityCollection, null, this.DynamicPropertiesContainerName));
                }
                else
                {
                    Details.KeyValues = null;
                    Details.TopCount = -1;
                }
                if (details.FilterExpression.HasTypeConstraint(details.DerivedCollectionName))
                {
                    Details.DerivedCollectionName = null;
                }
            }
        }

        private void ResolveEntryData(FluentCommandDetails details)
        {
            if (details.EntryValue != null)
            {
                Details.EntryData = _session.TypeCache.ToDictionary(details.EntryValue);
                Details.BatchEntries?.GetOrAdd(details.EntryValue, Details.EntryData);
            }
        }

        public IDictionary<string, object> FilterAsKey => Details.NamedKeyValues;

        public ResolvedCommand WithCount()
        {
            Details.IncludeCount = true;
            return this;
        }

        public override string ToString()
        {
            return Format();
        }

        public string Format()
        {
            return _session.Adapter.GetCommandFormatter().FormatCommand(this);
        }

        internal IDictionary<string, object> KeyValues
        {
            get
            {
                if (!Details.HasKey)
                    return null;

                var keyNames = _session.Metadata.GetDeclaredKeyPropertyNames(this.EntityCollection.Name).ToList();
                var namedKeyValues = Details.KeyValues?.Zip(
                     keyNames,
                     (keyValue, keyName) => new KeyValuePair<string, object>(keyName, keyValue))
                    ??
                    Details.NamedKeyValues;
                return namedKeyValues?.ToDictionary(
                    x => x.Key,
                    x => x.Value is ODataExpression oDataExpression ? oDataExpression.Value : x.Value);
            }
        }

        internal IDictionary<string, object> CommandData
        {
            get
            {
                if (Details.EntryData == null)
                    return new Dictionary<string, object>();
                if (string.IsNullOrEmpty(Details.DynamicPropertiesContainerName))
                    return Details.EntryData;

                var entryData = new Dictionary<string, object>();
                foreach (var key in Details.EntryData.Keys.Where(x =>
                    !string.Equals(x, Details.DynamicPropertiesContainerName, StringComparison.OrdinalIgnoreCase)))
                {
                    entryData.Add(key, Details.EntryData[key]);
                }

                if (Details.EntryData.TryGetValue(Details.DynamicPropertiesContainerName, out var dynamicProperties) && dynamicProperties != null)
                {
                    if (dynamicProperties is IDictionary<string, object> kv)
                    {
                        foreach (var key in kv.Keys)
                        {
                            entryData.Add(key, kv[key]);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Property {Details.DynamicPropertiesContainerName} must implement IDictionary<string,object> interface");
                    }
                }

                return entryData;
            }
        }

        private IDictionary<string, object> TryInterpretFilterExpressionAsKey(ODataExpression expression, out bool isAlternateKey)
        {
            isAlternateKey = false;
            var namedKeyValues = new Dictionary<string, object>();

            if (expression is not null
                && expression.ExtractLookupColumns(namedKeyValues)
                && _session.NamedKeyValuesMatchAnyKey(this.EntityCollection.Name, namedKeyValues, out var matchingNamedKeyValues, out isAlternateKey)
                && (_session.Settings.SupportNullsAsKeyPropertyValues || matchingNamedKeyValues.All(x => x.Value is not null)))
            {
                return matchingNamedKeyValues.ToIDictionary();
            }

            return default;
        }
    }
}
