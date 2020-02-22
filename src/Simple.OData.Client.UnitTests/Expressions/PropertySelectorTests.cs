using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Simple.OData.Client.Tests.Expressions
{
    public class Parent
    {
        public Child Child { get; set; }
        public List<CollectionChild> CollectionChild { get; set; }
        public string Property { get; set; }
    }

    public class Child
    {
        public string ChildProperty { get; set; }
        public GrandChild GrandChild { get; set; }
        public List<CollectionGrandChild> CollectionGrandChild { get; set; }
    }

    public class CollectionChild
    {
        public string CollectionChildProperty { get; set; }
        public GrandChild GrandChild { get; set; }
        public List<CollectionGrandChild> CollectionGrandChild { get; set; }
    }

    public class GrandChild
    {
        public string GrandChildProperty { get; set; }
    }

    public class CollectionGrandChild
    {
        public string CollectionGrandChildProperty { get; set; }
    }

    public abstract class ODataAnnotation
    {
        public abstract string Serialize();
    }

    public abstract class ODataPropertyNameAnnotation : ODataAnnotation
    {
        public string PropertyName { get; }

        protected ODataPropertyNameAnnotation(string propertyName)
        {
            PropertyName = propertyName;
        }

        public override string Serialize()
        {
            return "$@{propertyname}";
        }
    }

    public abstract class ODataValueAnnotation : ODataAnnotation
    {

    }

    public class ODataDeleteItemAnnotation : ODataAnnotation
    {
        private const string payload = "\"@removed\": { \"reason\": \"deleted\" },";
        public override string Serialize()
        {
            return payload;
        }
    }

    public class ODataUnlinkItemAnnotation : ODataAnnotation
    {
        private const string payload = "\"@removed\": { \"reason\": \"changed\" },";
        public override string Serialize()
        {
            return payload;
        }
    }

    public class ODataInsertItemAnnotation : ODataAnnotation
    {
        public override string Serialize()
        {
            return string.Empty;
        }
    }

    public class ODataUpdateItemAnnotation : ODataAnnotation
    {
        public override string Serialize()
        {
            return string.Empty;
        }
    }

    public class ODataLinkItemAnnotation : ODataAnnotation
    {
        public override string Serialize()
        {
            return string.Empty;
        }
    }

    public class ODataLinkAnnotation : ODataPropertyNameAnnotation
    {
        public ODataLinkAnnotation()
            : base("id")
        {

        }
    }



    public class ODataDeltaAnnotation : ODataPropertyNameAnnotation
    {
        public ODataDeltaAnnotation(string propertyName)
            : base(propertyName)
        {

        }
        public override string Serialize()
        {
            return $"{PropertyName}@delta";
        }
    }


    public static class ODataAnnotationExtensions
    {
        public static ODataAnnotatedValue<T> AsAnnotatedValue<T>(this T value, params ODataAnnotation[] annotations)
        {
            return ODataAnnotator.Annotate(annotations, value);
        }
    }

    public enum ODataValueAnnotations
    {
        Insert,
        Update,
        Link,
        Unlink,
        Delta
    }

    public static class ODataAnnotator
    {
        public static ODataAnnotatedValue<T> Annotate<T>(IEnumerable<ODataAnnotation> annotations, T value)
        {
            var annotatedValue = new ODataAnnotatedValue<T>(value);
            foreach (var annotation in annotations)
                annotatedValue.AddAnnotation(annotation);
            return annotatedValue;
        }

        public static ODataAnnotatedValue<T> Annotate<T>(ODataAnnotation annotation, T value)
        {
            return Annotate(new[] { annotation }, value);
        }

        public static ODataAnnotatedValue<T> Insert<T>(T value)
            => Annotate(new ODataInsertItemAnnotation(), value);
    }

    public interface IODataAnnotatedValue
    {
        object Value { get; }
        IEnumerable<ODataAnnotation> Annotations { get; }
    }

    public class ODataAnnotatedValue<T> : IODataAnnotatedValue
    {
        public T Value { get; }
        public IEnumerable<ODataAnnotation> Annotations { get; } = new List<ODataAnnotation>();

        object IODataAnnotatedValue.Value => Value;

        IEnumerable<ODataAnnotation> IODataAnnotatedValue.Annotations => Annotations;

        public ODataAnnotatedValue(T value)
        {
            Value = value;
        }

        public ODataAnnotatedValue<T> AddAnnotation(ODataAnnotation annotation)
        {
            ((List<ODataAnnotation>)Annotations).Add(annotation);
            return this;
        }

        public static implicit operator T(ODataAnnotatedValue<T> annotator) => annotator.Value;
        public static implicit operator ODataAnnotatedValue<T>(T value) => new ODataAnnotatedValue<T>(value);
    }


    public class PropertySelectorTests
    {
        [Fact]
        public void Should_Select_Property()
        {
            Expression<Func<Parent, object>> expr =
                (Parent x) => x.Child;

            //var e = new ODataExpression(expr);

            //var d = new Dictionary<string, object>();
            //var c = e.ExtractLookupColumns(d);
            //var b = e.Reference;
            //IODataClient c;
            //c.For<Parent>()
            //    .Set(null, x => x.CollectionChild)

            //var entry = new
            //{
            //    id = 1,
            //    name = "",
            //    details = new[]
            //    {
            //        new { detailid = 1 }.AsAnnotatedValue(new ODataDeleteItemAnnotation()),
            //        new { detailid = 2 }.AsAnnotatedValue(new ODataInsertItemAnnotation()),
            //        ODataAnnotator.Insert(new { detailid = 3 })
            //    }
            //};

            //var entry2 = new Parent
            //{
            //    Child = new Child().AsAnnotatedValue(new ODataLinkAnnotation()),
            //    CollectionChild = new List<CollectionChild>
            //    {
            //        new CollectionChild(),
            //        new CollectionChild().AsAnnotatedValue(new ODataDeleteItemAnnotation()),
            //        ODataAnnotator.Insert(new CollectionChild())
            //    }.AsAnnotatedValue(new ODataDeltaAnnotation(""))
            //};

            //Expression<Func<int, int>> rr = x => new ODataAnnotatedValue<int>(x);
            //var interceptor = new ObjectInterceptor<IODataAnnotatedValue>();
            //var interceptedExpression = (Expression<Func<int, int>>)(new AnnotationVisitor(interceptor).Visit(rr));
            //var res = interceptedExpression.Compile()(0);

            //Expression<Func<Parent>> rrr = () => new Parent
            //{
            //    Child = new Child
            //    {
            //        ChildProperty = "Parent.Child.ChildPropery",
            //        CollectionGrandChild = new List<CollectionGrandChild>
            //        {
            //            new CollectionGrandChild { CollectionGrandChildProperty = "XXX "}
            //        }
            //    },
            //    CollectionChild = new List<CollectionChild>
            //    {
            //        new CollectionChild
            //        {
            //            CollectionChildProperty = "ASDA",
            //            CollectionGrandChild = new List<CollectionGrandChild>
            //            {
            //                new CollectionGrandChild
            //                {
            //                    CollectionGrandChildProperty = "DEEEP"
            //                }
            //            }
            //        }
            //    },
            //    Property = "!@#"
            //};

            //var initInterceptor = new ObjectInitInterceptor();
            //var inintInterceptedExpression = (Expression<Func<Parent>>)(new InitializationVisitor(initInterceptor).Visit(rrr));
            //var res2 = inintInterceptedExpression.Compile()();

            //var r = GetDeepMember((Parent x) => x.CollectionChild.SelectMany(y => y.CollectionGrandChild.Select(z => z.CollectionGrandChildProperty)));

            var r1 = GetDeepMember((Parent x) => new
            {
                x.Child,
                CollectionChild = x.CollectionChild
                    .Select(y => new
                    {
                        y.CollectionGrandChild,
                        CollectionGrandChildProperty = y.CollectionGrandChild
                            .Select(z => new
                            {
                                z.CollectionGrandChildProperty
                            })
                    })
            });
        }
        private void GetDeepMember(Type parameterType, Expression expression, List<System.Reflection.MemberInfo> memberExpressions)
        {
            //The outer call should only allow Func<T,object> to pass here
            //Validation is single parameter (must be a class) and a return type (not void)

            if (expression is MemberExpression memberExpression)
            {
                memberExpressions.Add(memberExpression.Member);
                return;
            }
            else if (expression is NewExpression newExpression)
            {
                foreach (var argument in newExpression.Arguments)
                {
                    GetDeepMember(parameterType, argument, memberExpressions);
                }
                return;
            }
            else if (expression is MethodCallExpression callExpression && callExpression.Arguments.Count == 2
                && (MethodDefinitionEquals(callExpression.Method, selectMethodDefinition)
                    || MethodDefinitionEquals(callExpression.Method, selectManyMethodDefinition)))
            {
                var @this = callExpression.Arguments[0];
                var member = callExpression.Arguments[1];

                if (@this is MemberExpression innerMemberExpression
                        && innerMemberExpression.Expression is ParameterExpression rootParameterExpresion
                        && rootParameterExpresion.Type == parameterType
                    && member is LambdaExpression lambda
                    && lambda.Parameters.Count == 1
                    && lambda.ReturnType != null)
                {
                    memberExpressions.Add(innerMemberExpression.Member);
                    GetDeepMember(lambda.Parameters.First().Type, lambda.Body, memberExpressions);
                    return;
                }
            }

            throw Utils.NotSupportedExpression(expression);
        }

        private IEnumerable<System.Reflection.MemberInfo> GetDeepMember<T>(Expression<Func<T, object>> expression)
        {
            var l = new List<System.Reflection.MemberInfo>();
            GetDeepMember(typeof(T), expression.Body, l);
            return l;
        }

        private static readonly System.Reflection.MethodInfo selectMethodDefinition
            = ExtractMethod(() => Enumerable.Select<object, object>(null, x => null));

        private static readonly System.Reflection.MethodInfo selectManyMethodDefinition
            = ExtractMethod(() => Enumerable.SelectMany<object, object>(null, x => null));

        private static System.Reflection.MethodInfo ExtractMethod(Expression<Func<object>> expression)
        {
            if (expression.Body is MethodCallExpression methodCallExpression)
                return methodCallExpression.Method;

            return null;
        }

        private static System.Reflection.MethodInfo ExtractMethodDefinition(Expression<Func<object>> expression)
        {
            var method = ExtractMethod(expression);

            if (method is null)
                return null;

            if (method.IsGenericMethod && !method.IsGenericMethodDefinition)
                return method.GetGenericMethodDefinition();

            return method;
        }

        private static bool MethodDefinitionEquals(System.Reflection.MethodInfo left, System.Reflection.MethodInfo right)
        {
            if (left.IsGenericMethod)
                left = left.GetGenericMethodDefinition();

            if (right.IsGenericMethod)
                right = right.GetGenericMethodDefinition();

            return left == right;
        }

        public class ObjectInterceptor<T>
        {
            public List<T> Objects { get; } = new List<T>();

            public T Intercept(T annotatedValue)
            {
                Objects.Add(annotatedValue);
                return annotatedValue;
            }
        }

        public class ObjectInitInterceptor
        {
            public Dictionary<object, List<string>> Objects { get; } = new Dictionary<object, List<string>>();

            public T Intercept<T>(T @object, List<string> assignedProperties)
            {
                Objects.Add(@object, assignedProperties);
                return @object;
            }
        }

        public class InitializationVisitor : ExpressionVisitor
        {
            private static readonly MethodInfo interceptionMethod = ExtractMethodDefinition(() => new ObjectInitInterceptor().Intercept<object>(null, null));
            private readonly ObjectInitInterceptor interceptor;

            public MethodInfo GetMethod(Type type)
                => interceptionMethod.MakeGenericMethod(type);

            public InitializationVisitor(ObjectInitInterceptor interceptor)
            {
                this.interceptor = interceptor;
            }

            protected override Expression VisitMemberInit(MemberInitExpression node)
            {
                var innerExpresion = base.VisitMemberInit(node);

                if (node.Type.IsValueType)
                    return innerExpresion;

                var assignedMembers = node.Bindings != null
                    ? Expression.Constant(node.Bindings.Select(x => x.Member.Name).ToList())
                    : Expression.Constant(new List<string>());

                Expression returnExpresion = Expression.Call(Expression.Constant(interceptor), interceptionMethod.MakeGenericMethod(node.Type), innerExpresion, assignedMembers);

                return returnExpresion;
            }
        }

        public class AnnotationVisitor : ExpressionVisitor
        {
            private static readonly MethodInfo interceptionMethod = ExtractMethod(() => new ObjectInterceptor<IODataAnnotatedValue>().Intercept(null));
            private readonly ObjectInterceptor<IODataAnnotatedValue> interceptor;

            //private static System.Reflection.MethodInfo ExtractMethod(Expression<Func<object>> expression)
            //{
            //    if (expression.Body is MethodCallExpression methodCallExpression)
            //        return methodCallExpression.Method;

            //    return null;
            //}

            public AnnotationVisitor(ObjectInterceptor<IODataAnnotatedValue> interceptor)
            {
                this.interceptor = interceptor;
            }

            protected override Expression VisitNew(NewExpression node)
            {
                var interceptionType = typeof(IODataAnnotatedValue);

                if (!interceptionType.IsAssignableFrom(node.Type))
                    return base.VisitNew(node);

                var castedExrpession = (interceptionType == node.Type) ?
                    (Expression)node :
                    Expression.Convert(node, interceptionType);

                Expression returnExpresion = Expression.Call(Expression.Constant(interceptor), interceptionMethod, castedExrpession);

                if (interceptionType != node.Type)
                    returnExpresion = Expression.Convert(returnExpresion, node.Type);

                return returnExpresion;
            }
        }
    }
}
