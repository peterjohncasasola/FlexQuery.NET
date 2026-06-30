using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace FlexQuery.NET.QueryDebug;

/// <summary>
/// Utility to convert LINQ Expressions into readable C# syntax.
/// </summary>
public sealed class ExpressionPrinter : ExpressionVisitor
{
    private readonly StringBuilder _sb = new();

    /// <summary>
    /// Prints the expression as a C#-like string.
    /// </summary>
    public static string Print(Expression? expression)
    {
        if (expression == null) return "null";
        var printer = new ExpressionPrinter();
        printer.Visit(expression);
        return printer._sb.ToString();
    }

    /// <inheritdoc />
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        var parameters = node.Parameters.Count == 1 
            ? node.Parameters[0].Name 
            : $"({string.Join(", ", node.Parameters.Select(p => p.Name))})";
            
        _sb.Append(parameters);
        _sb.Append(" => ");
        Visit(node.Body);
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitBinary(BinaryExpression node)
    {
        _sb.Append("(");
        Visit(node.Left);
        _sb.Append($" {GetBinaryOperator(node.NodeType)} ");
        Visit(node.Right);
        _sb.Append(")");
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression != null)
        {
            Visit(node.Expression);
            _sb.Append(".");
        }
        _sb.Append(node.Member.Name);
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is string s)
            _sb.Append($"\"{s}\"");
        else if (node.Value == null)
            _sb.Append("null");
        else if (node.Value is bool b)
            _sb.Append(b ? "true" : "false");
        else
            _sb.Append(node.Value);
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.IsStatic && node.Method.GetCustomAttributes(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false).Any())
        {
            Visit(node.Arguments[0]);
            _sb.Append(".");
            _sb.Append(node.Method.Name);
            _sb.Append("(");
            for (int i = 1; i < node.Arguments.Count; i++)
            {
                Visit(node.Arguments[i]);
                if (i < node.Arguments.Count - 1) _sb.Append(", ");
            }
            _sb.Append(")");
            return node;
        }

        if (node.Object != null)
        {
            Visit(node.Object);
            _sb.Append(".");
        }
        
        _sb.Append(node.Method.Name);
        _sb.Append("(");
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            Visit(node.Arguments[i]);
            if (i < node.Arguments.Count - 1) _sb.Append(", ");
        }
        _sb.Append(")");
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitParameter(ParameterExpression node)
    {
        _sb.Append(node.Name ?? node.Type.Name.ToLowerInvariant());
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitUnary(UnaryExpression node)
    {
        switch (node.NodeType)
        {
            case ExpressionType.Not:
                _sb.Append("!");
                Visit(node.Operand);
                break;
            case ExpressionType.Convert:
            case ExpressionType.Quote:
                Visit(node.Operand);
                break;
            default:
                base.VisitUnary(node);
                break;
        }
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitNew(NewExpression node)
    {
        _sb.Append("new ");
        _sb.Append(node.Type.Name);
        _sb.Append("(");
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            Visit(node.Arguments[i]);
            if (i < node.Arguments.Count - 1) _sb.Append(", ");
        }
        _sb.Append(")");
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        Visit(node.NewExpression);
        _sb.Append(" { ");
        for (int i = 0; i < node.Bindings.Count; i++)
        {
            var binding = node.Bindings[i];
            _sb.Append(binding.Member.Name);
            _sb.Append(" = ");
            if (binding is MemberAssignment assignment)
            {
                Visit(assignment.Expression);
            }
            if (i < node.Bindings.Count - 1) _sb.Append(", ");
        }
        _sb.Append(" }");
        return node;
    }

    private static string GetBinaryOperator(ExpressionType type) => type switch
    {
        ExpressionType.Equal              => "==",
        ExpressionType.NotEqual           => "!=",
        ExpressionType.GreaterThan        => ">",
        ExpressionType.GreaterThanOrEqual => ">=",
        ExpressionType.LessThan           => "<",
        ExpressionType.LessThanOrEqual    => "<=",
        ExpressionType.AndAlso            => "&&",
        ExpressionType.OrElse             => "||",
        ExpressionType.Add                => "+",
        ExpressionType.Subtract           => "-",
        ExpressionType.Multiply           => "*",
        ExpressionType.Divide             => "/",
        _                                 => type.ToString()
    };
}