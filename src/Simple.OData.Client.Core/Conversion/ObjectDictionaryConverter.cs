using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Simple.OData.Client.Extensions
{
    class ObjectDictionaryConverter

    {
        private static ConcurrentDictionary<Type, ActivatorDelegate> _defaultActivators = new ConcurrentDictionary<Type, ActivatorDelegate>();
        private static ConcurrentDictionary<Tuple<Type, Type>, ActivatorDelegate> _collectionActivators = new ConcurrentDictionary<Tuple<Type, Type>, ActivatorDelegate>();

        private readonly ISession session;
        private ITypeCache typeCache => session.TypeCache;
        private INameMatchResolver nameMatchResolver => session.Settings.NameMatchResolver;
        private ITypeConverter converter => session.Settings.TypeConverters;

        internal static Func<IDictionary<string, object>, ISession, ODataEntry> CreateDynamicODataEntry { get; set; }

        public ObjectDictionaryConverter(ISession session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public T ToObject<T>(IDictionary<string, object> source, bool dynamicObject = false)
            where T : class
        {
            if (source == null)
                return default(T);

            if (typeCache.IsTypeAssignableFrom(typeof(IDictionary<string, object>), typeof(T)))
                return source as T;

            if (typeof(T) == typeof(ODataEntry))
                return CreateODataEntry(source, dynamicObject) as T;

            if (typeof(T) == typeof(string) || typeCache.IsValue(typeof(T)))
                throw new InvalidOperationException($"Unable to convert structural data to {typeof(T).Name}.");

            return (T)ToObject(source, typeof(T), dynamicObject);
        }

        public object ToObject(IDictionary<string, object> source, Type type, bool dynamicObject = false)
        {
            if (source == null)
                return null;

            if (typeCache.IsTypeAssignableFrom(typeof(IDictionary<string, object>), type))
                return source;

            if (type == typeof(ODataEntry))
                return CreateODataEntry(source, dynamicObject);

            // Check before custom converter so we use the most appropriate type.
            if (source.ContainsKey(FluentCommand.AnnotationsLiteral))
            {
                type = GetTypeFromAnnotation(source, type);
            }

            if (converter.HasDictionaryConverter(type))
            {
                return converter.Convert(source, type);
            }

            if (type.HasCustomAttribute(typeof(CompilerGeneratedAttribute), false))
            {
                return CreateInstanceOfAnonymousType(source, type);
            }

            var instance = CreateInstance(type);

            IDictionary<string, object> dynamicProperties = null;
            var dynamicPropertiesContainerName = typeCache.DynamicContainerName(type);
            if (!string.IsNullOrEmpty(dynamicPropertiesContainerName))
            {
                dynamicProperties = CreateDynamicPropertiesContainer(type, instance, dynamicPropertiesContainerName);
            }

            foreach (var item in source)
            {
                var property = FindMatchingProperty(type, item);

                if (property != null && property.CanWrite)
                {
                    if (item.Value != null)
                    {
                        property.SetValue(instance, ConvertValue(property.PropertyType, item.Value));
                    }
                }
                else
                {
                    dynamicProperties?.Add(item.Key, item.Value);
                }
            }

            return instance;
        }

        private Type GetTypeFromAnnotation(IDictionary<string, object> source, Type type)
        {
            var annotations = source[FluentCommand.AnnotationsLiteral] as ODataEntryAnnotations;

            var odataType = annotations?.TypeName;

            if (string.IsNullOrEmpty(odataType))
            {
                return type;
            }

            if (!nameMatchResolver.IsMatch(odataType, type.Name))
            {
                // Ok, something other than the base type, see if we can match it
                var derived = typeCache
                    .GetDerivedTypes(type)
                    .FirstOrDefault(x => nameMatchResolver.IsMatch(odataType, typeCache.GetMappedName(x)));

                if (derived != null)
                {
                    return derived;
                }

                var typeFound =AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(x => x.GetType(odataType))
                    .FirstOrDefault(x => x is not null);
                    
                if (typeFound != null)
                {
                    typeCache.Register(typeFound);
                    return typeFound;
                }

                // TODO: Should we throw an exception here or log a warning here as we don't understand the data
            }

            return type;
        }

        private PropertyInfo FindMatchingProperty(Type type, KeyValuePair<string, object> item)
        {
            var property = typeCache.GetMappedPropertiesWithNames(type)
                .FirstOrDefault(x => nameMatchResolver.IsMatch(x.name, item.Key)).proprety;

            if (property == null && item.Key == FluentCommand.AnnotationsLiteral)
            {
                property = typeCache.GetAnnotationsProperty(type);
            }

            return property;
        }

        private object ConvertValue(Type type, object itemValue)
        {
            return IsCollectionType(type, itemValue)
                ? ConvertCollection(type, itemValue)
                : ConvertSingle(type, itemValue);
        }

        private bool IsCollectionType(Type type, object itemValue)
        {
            return
                (type.IsArray || typeCache.IsGeneric(type) &&
                 typeCache.IsTypeAssignableFrom(typeof(System.Collections.IEnumerable), type)) &&
                (itemValue as System.Collections.IEnumerable) != null;
        }

        private bool IsCompoundType(Type type)
        {
            return !typeCache.IsValue(type) && !type.IsArray && type != typeof(string);
        }

        private object ConvertEnum(Type type, object itemValue)
        {
            if (itemValue == null)
                return null;

            var stringValue = itemValue.ToString();
            if (int.TryParse(stringValue, out var intValue))
            {
                typeCache.TryConvert(intValue, type, out var result);
                return result;
            }
            else
            {
                return Enum.Parse(type, stringValue, false);
            }
        }

        private object ConvertSingle(Type type, object itemValue)
        {
            object TryConvert(object v, Type t) => typeCache.TryConvert(v, t, out var result) ? result : v;

            return type == typeof(ODataEntryAnnotations)
                ? itemValue
                : IsCompoundType(type)
                    ? ToObject(ToDictionary(itemValue), type)
                    : type.IsEnumType()
                        ? ConvertEnum(type, itemValue)
                        : TryConvert(itemValue, type);
        }

        private object ConvertCollection(Type type, object itemValue)
        {
            var elementType = type.IsArray
                ? type.GetElementType()
                : typeCache.IsGeneric(type) && typeCache.GetGenericTypeArguments(type).Length == 1
                    ? typeCache.GetGenericTypeArguments(type)[0]
                    : null;

            if (elementType == null)
                return null;

            var count = GetCollectionCount(itemValue);
            var arrayValue = Array.CreateInstance(elementType, count);

            count = 0;
            foreach (var item in (itemValue as System.Collections.IEnumerable))
            {
                arrayValue.SetValue(ConvertSingle(elementType, item), count++);
            }

            if (type.IsArray || typeCache.IsTypeAssignableFrom(type, arrayValue.GetType()))
            {
                return arrayValue;
            }
            else
            {
                var collectionTypes = new[]
                {
                    typeof(IList<>).MakeGenericType(elementType),
                    typeof(IEnumerable<>).MakeGenericType(elementType)
                };
                var collectionType = type.GetConstructor(new[] { collectionTypes[0] }) != null
                    ? collectionTypes[0]
                    : collectionTypes[1];
                var activator = _collectionActivators.GetOrAdd(new Tuple<Type, Type>(type, collectionType), t => type.CreateActivator(collectionType));
                return activator?.Invoke(arrayValue);
            }
        }

        private int GetCollectionCount(object collection)
        {
            if (collection is System.Collections.IList list)
            {
                return list.Count;
            }
            else if (collection is System.Collections.IDictionary dictionary)
            {
                return dictionary.Count;
            }
            else
            {
                int count = 0;
                var e = ((System.Collections.IEnumerable)collection).GetEnumerator();
                using (e as IDisposable)
                {
                    while (e.MoveNext()) count++;
                }

                return count;
            }
        }

        public IDictionary<string, object> ToDictionary(object source)
        {
            if (source == null)
                return new Dictionary<string, object>();
            if (source is IDictionary<string, object> objects)
                return objects;
            if (source is ODataEntry entry)
                return (Dictionary<string, object>)entry;

            return typeCache.ToDictionary(source);
        }

        private object CreateInstance(Type type)
        {
            if (type == typeof(IDictionary<string, object>))
            {
                return new Dictionary<string, object>();
            }
            else
            {
                var ctor = _defaultActivators.GetOrAdd(type, t => t.CreateActivator());
                return ctor.Invoke();
            }
        }

        private ODataEntry CreateODataEntry(IDictionary<string, object> source, bool dynamicObject = false)
        {
            return dynamicObject && CreateDynamicODataEntry != null ?
                CreateDynamicODataEntry(source, session) :
                new ODataEntry(source);
        }

        private IDictionary<string, object> CreateDynamicPropertiesContainer(Type type, object instance, string dynamicPropertiesContainerName)
        {
            var property = typeCache.GetNamedProperty(type, dynamicPropertiesContainerName);

            if (property == null)
                throw new ArgumentException($"Type {type} does not have property {dynamicPropertiesContainerName} ");

            if (!typeCache.IsTypeAssignableFrom(typeof(IDictionary<string, object>), property.PropertyType))
                throw new InvalidOperationException($"Property {dynamicPropertiesContainerName} must implement IDictionary<string,object> interface");

            var dynamicProperties = new Dictionary<string, object>();
            property.SetValue(instance, dynamicProperties);
            return dynamicProperties;
        }

        private object CreateInstanceOfAnonymousType(IDictionary<string, object> source, Type type)
        {
            var constructor = FindConstructorOfAnonymousType(type, source);
            if (constructor == null)
            {
                throw new ConstructorNotFoundException(type, source.Values.Select(v => v.GetType()));
            }

            var parameterInfos = constructor.GetParameters();
            var constructorParameters = new object[parameterInfos.Length];
            for (var parameterIndex = 0; parameterIndex < parameterInfos.Length; parameterIndex++)
            {
                var parameterInfo = parameterInfos[parameterIndex];
                constructorParameters[parameterIndex] = ConvertValue(parameterInfo.ParameterType, source[parameterInfo.Name]);
            }
            return constructor.Invoke(constructorParameters);
        }

        private ConstructorInfo FindConstructorOfAnonymousType(Type type, IDictionary<string, object> source)
        {
            return type.GetDeclaredConstructors().FirstOrDefault(c =>
            {
                var parameters = c.GetParameters();
                return parameters.Length == source.Count &&
                       parameters.All(p => source.ContainsKey(p.Name));
            });
        }
    }
}