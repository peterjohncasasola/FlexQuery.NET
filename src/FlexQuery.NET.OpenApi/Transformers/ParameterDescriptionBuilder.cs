using System.Text;

namespace FlexQuery.NET.OpenApi.Transformers;

internal static class ParameterDescriptionBuilder
{
    public static string? AppendFieldSection(string? description, string title, string[]? fields)
    {
        if (string.IsNullOrEmpty(title) || fields == null || fields.Length == 0)
            return description;

        var section = new StringBuilder();
        if (!string.IsNullOrEmpty(description))
        {
            section.Append(description);
            section.AppendLine();
            section.AppendLine();
        }

        section.Append(title);
        section.AppendLine();
        section.AppendLine();

        foreach (var field in fields)
        {
            section.Append("• ");
            section.AppendLine(field);
        }

        return section.ToString();
    }

    public static string? AppendExampleSection(string? description, string[]? examples)
    {
        if (examples == null || examples.Length == 0)
            return description;

        var section = new StringBuilder();
        if (!string.IsNullOrEmpty(description))
        {
            section.Append(description);
            section.AppendLine();
            section.AppendLine();
        }

        section.Append("Example");
        section.AppendLine();
        section.AppendLine();

        section.Append(string.Join(",", examples));

        return section.ToString();
    }
}
