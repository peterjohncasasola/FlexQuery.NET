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

/// <summary>
/// Utility to visualize the structure of an Expression Tree.
/// </summary>
public static class ExpressionTreeVisualizer
{
    /// <summary>
    /// Visualizes the expression tree structure.
    /// </summary>
    /// <param name="expression">The expression to visualize.</param>
    /// <param name="indent">The current indentation level.</param>
    /// <returns>A string representation of the tree structure.</returns>
    public static string Visualize(Expression? expression, int indent = 0)
    {
        if (expression == null) return "null";
        var sb = new StringBuilder();
        VisualizeInternal(expression, indent, sb);
        return sb.ToString();
    }

    private static void VisualizeInternal(Expression node, int indent, StringBuilder sb)
    {
        sb.Append(' ', indent * 2);
        sb.Append(node.NodeType);
        
        switch (node)
        {
            case BinaryExpression binary:
                sb.AppendLine();
                VisualizeInternal(binary.Left, indent + 1, sb);
                VisualizeInternal(binary.Right, indent + 1, sb);
                break;
            case LambdaExpression lambda:
                sb.Append(" (");
                sb.Append(string.Join(", ", lambda.Parameters.Select(p => $"{p.Type.Name} {p.Name}")));
                sb.Append(")");
                sb.AppendLine();
                VisualizeInternal(lambda.Body, indent + 1, sb);
                break;
            case MemberExpression member:
                sb.Append($" -> {member.Member.Name}");
                if (member.Expression != null)
                {
                    sb.AppendLine();
                    VisualizeInternal(member.Expression, indent + 1, sb);
                }
                else
                {
                    sb.AppendLine();
                }
                break;
            case ConstantExpression constant:
                sb.Append($" (Value: {constant.Value ?? "null"})");
                sb.AppendLine();
                break;
            case MethodCallExpression call:
                sb.Append($" -> {call.Method.Name}");
                sb.AppendLine();
                foreach (var arg in call.Arguments)
                {
                    VisualizeInternal(arg, indent + 1, sb);
                }
                break;
            case ParameterExpression parameter:
                sb.Append($" (Name: {parameter.Name}, Type: {parameter.Type.Name})");
                sb.AppendLine();
                break;
            case UnaryExpression unary:
                sb.AppendLine();
                VisualizeInternal(unary.Operand, indent + 1, sb);
                break;
            default:
                sb.AppendLine();
                break;
        }
    }
}
