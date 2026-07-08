using FlexQuery.NET.Models;
using System.Linq.Expressions;
using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.Expressions;

namespace FlexQuery.NET;

/// <summary>
/// Extensions for debugging FlexQuery operations.
/// </summary>
public static class FlexQueryDebugExtensions
{
    /// <summary>
    /// Analyzes the query application process and returns a detailed debug result.
    /// Does not execute the query.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable to analyze.</param>
    /// <param name="options">The query options to apply for analysis.</param>
    /// <returns>A <see cref="QueryDebugInfo"/> containing the AST, expression tree, and LINQ lambda string.</returns>
    public static QueryDebugInfo ToFlexQueryDebug<T>(this IQueryable<T> query, QueryOptions options)
    {
        var provider = new DebugQueryProvider(query.Provider);
        var debugQuery = provider.CreateQuery<T>(query.Expression);
        
        // This will trigger the DebugQueryProvider to capture the expression
        debugQuery.Apply(options);
        
        return new QueryDebugInfo
        {
            LinqLambda = ExpressionPrinter.Print(provider.LastExpression),
            ExpressionTree = ExpressionTreeVisualizer.Visualize(provider.LastExpression)
        };
    }

    private sealed class DebugQueryProvider(IQueryProvider inner) : IQueryProvider
    {
        public Expression? LastExpression { get; private set; }

        public IQueryable CreateQuery(Expression expression)
        {
            LastExpression = expression;
            var elementType = GetElementType(expression.Type);
            return (IQueryable)Activator.CreateInstance(typeof(DebugQueryable<>).MakeGenericType(elementType), expression, this)!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            LastExpression = expression;
            return new DebugQueryable<TElement>(expression, this);
        }

        public object? Execute(Expression expression) => inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

        private static Type GetElementType(Type type)
        {
            var interfaceType = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return interfaceType?.GetGenericArguments()[0] ?? type;
        }
    }

    private sealed class DebugQueryable<T>(Expression expression, IQueryProvider provider) : IQueryable<T>
    {
        public Type ElementType => typeof(T);
        public Expression Expression { get; } = expression;
        public IQueryProvider Provider { get; } = provider;

        public System.Collections.IEnumerator GetEnumerator() => Enumerable.Empty<T>().GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => Enumerable.Empty<T>().GetEnumerator();
    }
}

