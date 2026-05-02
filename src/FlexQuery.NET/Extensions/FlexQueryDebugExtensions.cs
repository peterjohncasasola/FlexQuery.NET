using FlexQuery.NET.Builders;
using FlexQuery.NET.QueryDebug;
using FlexQuery.NET.Models;
using System.Linq.Expressions;

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
    public static DebugResult ToFlexQueryDebug<T>(this IQueryable<T> query, QueryOptions options)
    {
        var provider = new DebugQueryProvider(query.Provider);
        var debugQuery = provider.CreateQuery<T>(query.Expression);
        
        // This will trigger the DebugQueryProvider to capture the expression
        var applied = debugQuery.ApplyQueryOptions(options);
        
        return new DebugResult
        {
            Ast = options.Ast,
            LinqLambda = ExpressionPrinter.Print(provider.LastExpression),
            ExpressionTree = ExpressionTreeVisualizer.Visualize(provider.LastExpression)
        };
    }

    private sealed class DebugQueryProvider : IQueryProvider
    {
        private readonly IQueryProvider _inner;
        public Expression? LastExpression { get; private set; }

        public DebugQueryProvider(IQueryProvider inner) => _inner = inner;

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

        public object? Execute(Expression expression) => _inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

        private static Type GetElementType(Type type)
        {
            var iface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return iface?.GetGenericArguments()[0] ?? type;
        }
    }

    private sealed class DebugQueryable<T> : IQueryable<T>
    {
        public DebugQueryable(Expression expression, IQueryProvider provider)
        {
            Expression = expression;
            Provider = provider;
        }

        public Type ElementType => typeof(T);
        public Expression Expression { get; }
        public IQueryProvider Provider { get; }

        public System.Collections.IEnumerator GetEnumerator() => Enumerable.Empty<T>().GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => Enumerable.Empty<T>().GetEnumerator();
    }
}

/// <summary>
/// Result of a FlexQuery debug operation.
/// </summary>
public sealed class DebugResult
{
    /// <summary>The parsed AST.</summary>
    public object? Ast { get; set; }
    /// <summary>The generated LINQ lambda as a string.</summary>
    public string LinqLambda { get; set; } = string.Empty;
    /// <summary>The structural visualization of the expression tree.</summary>
    public string ExpressionTree { get; set; } = string.Empty;
}
