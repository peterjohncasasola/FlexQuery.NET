using System.Linq.Expressions;
using System.Text;

namespace FlexQuery.NET.QueryDebug;

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