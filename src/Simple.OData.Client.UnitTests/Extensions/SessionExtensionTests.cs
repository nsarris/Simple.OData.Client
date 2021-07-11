using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Simple.OData.Client.Extensions;
using Simple.OData.Client.Tests.Core;

namespace Simple.OData.Client.Tests.Extensions
{
    public class SessionExtensionTests : Core.CoreTestBase
    {
        public override string MetadataFile => "Northwind4WithAlternateKeys.xml";

        public override IFormatSettings FormatSettings => new ODataV4Format();

        // extract will work with entity classes only when:
        // keys properties are nullable on class model + nulls not allowed on key properties

        public enum MatchType
        {
            ExactMatch,
            ContainedIn
        }

        public enum KeyType
        {
            Any,
            Primary,
            Alternate
        }

        public class TestCase
        {
            public TestCase(MatchType matchType, KeyType keyType, string collectionName, object input, object expectedKey, bool isAlternateKey = false)
            {
                CollectionName = collectionName;
                MatchType = matchType;
                KeyType = keyType;
                Input = input;
                ExpectedKey = expectedKey;
                IsAlternateKey = isAlternateKey;
            }

            public string CollectionName { get; }
            public MatchType MatchType { get; }
            public KeyType KeyType { get; }
            public object Input { get; }
            public object ExpectedKey { get; }
            public bool IsAlternateKey { get; }
        }

        private TestCaseBuilder With(object o, string collectionName)
        {
            return new TestCaseBuilder(_session, o, collectionName);
        }

        private TestCaseBuilder With<T>(object o)
        {
            return new TestCaseBuilder(_session, o, typeof(T).Name);
        }

        private TestCaseBuilder With(object o)
        {
            return new TestCaseBuilder(_session, o, o.GetType().Name);
        }

        public class TestCaseBuilder
        {
            private readonly ISession session;

            public TestCaseBuilder(ISession session, object value, string collectionName)
            {
                this.session = session;
                Key = value;
                CollectionName = collectionName;
            }

            public object Key { get; }
            public string CollectionName { get; }
            
            public TestCaseBuilder ShouldMatchPrimaryKey(object expectedKey = null)
            {
                ExecuteTest(new TestCase(MatchType.ExactMatch, KeyType.Primary, CollectionName, Key, expectedKey ?? Key));
                ShouldNotMatchAlternateKey();
                ShouldMatchKey(expectedKey, false);
                return this;
            }

            public TestCaseBuilder ShouldMatchAlternateKey(object expectedKey = null)
            {
                ExecuteTest(new TestCase(MatchType.ExactMatch, KeyType.Alternate, CollectionName, Key, expectedKey ?? Key));
                ShouldNotMatchPrimaryKey();
                ShouldMatchKey(expectedKey, true);
                return this;
            }

            public TestCaseBuilder ShouldMatchKey(object expectedKey = null, bool isAlternateKey = false)
            {
                ExecuteTest(new TestCase(MatchType.ExactMatch, KeyType.Any, CollectionName, Key, expectedKey ?? Key, isAlternateKey));
                return this;
            }

            public TestCaseBuilder ShouldNotMatchPrimaryKey()
            {
                ExecuteTest(new TestCase(MatchType.ExactMatch, KeyType.Primary, CollectionName, Key, null));
                return this;
            }

            public TestCaseBuilder ShouldNotMatchAlternateKey()
            {
                ExecuteTest(new TestCase(MatchType.ExactMatch, KeyType.Alternate, CollectionName, Key, null));

                return this;
            }

            public TestCaseBuilder ShouldNotMatchKey()
            {
                ShouldNotMatchPrimaryKey();
                ShouldNotMatchAlternateKey();
                ExecuteTest(new TestCase(MatchType.ExactMatch, KeyType.Any, CollectionName, Key, null));
                return this;
            }

            public TestCaseBuilder ShouldExtractPrimaryKey(object expectedKey)
            {
                ExecuteTest(new TestCase(MatchType.ContainedIn, KeyType.Primary, CollectionName, Key, expectedKey));
                return this;
            }

            public TestCaseBuilder ShouldExtractAlternateKey(object expectedKey)
            {
                ExecuteTest(new TestCase(MatchType.ContainedIn, KeyType.Alternate, CollectionName, Key, expectedKey));
                return this;
            }

            public TestCaseBuilder ShouldExtractKey(object expectedKey, bool isAlternateKey = false)
            {
                ExecuteTest(new TestCase(MatchType.ContainedIn, KeyType.Any, CollectionName, Key, expectedKey, isAlternateKey));
                return this;
            }

            public TestCaseBuilder ShouldNotExtractPrimaryKey()
            {
                ExecuteTest(new TestCase(MatchType.ContainedIn, KeyType.Primary, CollectionName, Key, null));
                return this;
            }

            public TestCaseBuilder ShouldNotExtractAlternateKey()
            {
                ExecuteTest(new TestCase(MatchType.ContainedIn, KeyType.Alternate, CollectionName, Key, null));
                return this;
            }

            public TestCaseBuilder ShouldNotExtractKey()
            {
                ExecuteTest(new TestCase(MatchType.ContainedIn, KeyType.Any, CollectionName, Key, null));
                return this;
            }

            private IDictionary<string, object> ToDictionary(object o)
            => o is IEnumerable<KeyValuePair<string, object>> d ?
                d.ToDictionary(x => x.Key, x => x.Value) :
                session.TypeCache.ToDictionary(o);

            private void AssertKey(IEnumerable<KeyValuePair<string, object>> keyValues, object expectedKey)
            {
                var expectedKeyValues = session.TypeCache.ToDictionary(expectedKey);
                Assert.Equal(expectedKeyValues.Count(), keyValues.Count());
                foreach (var keyValue in keyValues)
                {
                    Assert.True(expectedKeyValues.TryGetValue(keyValue.Key, out var value));
                    Assert.Equal(value, keyValue.Value);
                }
            }

            private void ExecuteTest(TestCase testCase)
            {
                IEnumerable<KeyValuePair<string, object>> keyValues;
                bool isAlternateKey = false;

                var match = (testCase.MatchType, testCase.KeyType) switch
                {
                    (MatchType.ExactMatch, KeyType.Primary) =>
                            session.NamedKeyValuesMatchPrimaryKey(
                            testCase.CollectionName,
                            ToDictionary(testCase.Input),
                            out keyValues),
                    (MatchType.ExactMatch, KeyType.Alternate) =>
                            session.NamedKeyValuesMatchAlternateKey(
                            testCase.CollectionName,
                            ToDictionary(testCase.Input),
                            out keyValues),
                    (MatchType.ExactMatch, KeyType.Any) =>
                            session.NamedKeyValuesMatchAnyKey(
                            testCase.CollectionName,
                            ToDictionary(testCase.Input),
                            out keyValues,
                            out isAlternateKey),
                    (MatchType.ContainedIn, KeyType.Primary) =>
                            session.TryExtractPrimaryKeyFromNamedValues(
                            testCase.CollectionName,
                            ToDictionary(testCase.Input),
                            out keyValues),
                    (MatchType.ContainedIn, KeyType.Alternate) =>
                            session.TryExtractAlternateKeyFromNamedValues(
                            testCase.CollectionName,
                            ToDictionary(testCase.Input),
                            out keyValues),
                    (MatchType.ContainedIn, KeyType.Any) =>
                            session.TryExtractAnyKeyFromNamedValues(
                            testCase.CollectionName,
                            ToDictionary(testCase.Input),
                            out keyValues,
                            out isAlternateKey),
                    _ => throw new NotSupportedException(),
                };

                if (testCase.ExpectedKey != null)
                {
                    Assert.True(match);
                    Assert.Equal(testCase.IsAlternateKey, isAlternateKey);
                    AssertKey(keyValues, testCase.ExpectedKey);
                }
                else
                {
                    Assert.False(match);
                }

            }
        }

        [Fact]
        public void Should_ExactlyMatch_Key()
        {
            With<Employee>(new { EmployeeID = 1 })
                .ShouldMatchPrimaryKey();

            With<Category>(new { CategoryID = 1 })
                .ShouldMatchPrimaryKey();

            With<Category>(new { Dummy = 1 })
                .ShouldNotMatchKey();

            With<Category>(new { CategoryID = 1, CategoryName = "Beverages" })
                .ShouldNotMatchKey();

            With(new Category { CategoryID = 1, CategoryName = "Beverages" })
                .ShouldNotMatchKey();

            With<Employee>(new { FirstName = "First", LastName = "Last" })
                .ShouldMatchAlternateKey()
                .ShouldMatchAlternateKey(new { LastName = "Last", FirstName = "First" });

            With<Employee>(new { HomePhone = "123", Title = "Mr" })
                .ShouldMatchAlternateKey();

            With<Order>(new { OrderID = "556" })
                .ShouldMatchPrimaryKey();

            With<Order>(new { CustomerID = "ALFKI" })
                .ShouldMatchAlternateKey();

            With<Order>(new { ShipName = "ALFKI" })
                .ShouldMatchAlternateKey();
        }

        [Fact]
        public void Should_Extract_Key()
        {
            With<Category>(new { CategoryID = 1, CategoryName = "Beverages" })
                .ShouldExtractPrimaryKey(new { CategoryID = 1 })
                .ShouldExtractAlternateKey(new { CategoryName = "Beverages" })
                .ShouldExtractKey(new { CategoryID = 1 });

            With(new Category { CategoryID = 1, CategoryName = "Beverages" })
                .ShouldExtractPrimaryKey(new { CategoryID = 1 })
                .ShouldExtractAlternateKey(new { CategoryName = "Beverages" })
                .ShouldExtractKey(new { CategoryID = 1 });

            With<Employee>(new { FirstName = "First", LastName = "Last" })
                .ShouldNotExtractPrimaryKey()
                .ShouldExtractAlternateKey(new { FirstName = "First", LastName = "Last" })
                .ShouldExtractKey(new { FirstName = "First", LastName = "Last" }, true);

            With<Order>(new { OrderID = 3, CustomerID = "ALFKI", ShipName = "Ship" })
                .ShouldExtractPrimaryKey(new { OrderID = 3 })
                .ShouldExtractAlternateKey(new { CustomerID = "ALFKI" });

            With<Order>(new { CustomerID = "ALFKI" })
                .ShouldNotExtractPrimaryKey()
                .ShouldExtractAlternateKey(new { CustomerID = "ALFKI" });

            With<Order>(new { ShipName = "ALFKI" })
                .ShouldNotExtractPrimaryKey()
                .ShouldExtractAlternateKey(new { ShipName = "ALFKI" });
        }

        [Fact]
        public void Should_Extract_Key_Do_Not_Allow_Nulls()
        {
            With<Employee>(new { EmployeeID = default(int?), FirstName = "First", LastName = "Last" })
                .ShouldNotExtractPrimaryKey()
                .ShouldExtractAlternateKey(new { FirstName = "First", LastName = "Last" })
                .ShouldExtractKey(new { FirstName = "First", LastName = "Last" }, true);
        }

        [Fact]
        public void Should_Extract_Key_Allow_Nulls()
        {
            _session.Settings.SupportNullsAsKeyPropertyValues = true;
            
            With<Category>(new { CategoryID = default(int?), CategoryName = "Beverages" })
                .ShouldExtractPrimaryKey(new { CategoryID = (int?)null })
                .ShouldExtractAlternateKey(new { CategoryName = "Beverages" })
                .ShouldExtractKey(new { CategoryID = (int?)null });
        }
    }
}
