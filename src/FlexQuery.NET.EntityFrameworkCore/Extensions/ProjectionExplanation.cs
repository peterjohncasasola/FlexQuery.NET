namespace FlexQuery.NET.EntityFrameworkCore;

/// <summary>
/// Human-readable projection explanation.
/// </summary>
public sealed class ProjectionExplanation
{
    /// <summary>
    /// Selected field paths.
    /// </summary>
    public IReadOnlyList<string> SelectedFields { get; set; } = new List<string>();

    /// <summary>
    /// Navigation property usage.
    /// </summary>
    public IReadOnlyDictionary<string, string> NavigationUsage { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Optimization notes.
    /// </summary>
    public IReadOnlyList<string> OptimizationNotes { get; set; } = new List<string>();

    /// <summary>
    /// Estimated number of columns.
    /// </summary>
    public int EstimatedColumns { get; set; }

    /// <summary>Returns a formatted string representation of the projection explanation.</summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Projection Explanation ===");
        sb.AppendLine();
        sb.AppendLine($"Estimated Columns: {EstimatedColumns}");
        sb.AppendLine();

        sb.AppendLine("Selected Fields:");
        foreach (var field in SelectedFields)
        {
            sb.AppendLine($"  - {field}");
        }

        if (NavigationUsage.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Navigation Usage:");
            foreach (var nav in NavigationUsage)
            {
                sb.AppendLine($"  - {nav.Key}: {nav.Value}");
            }
        }

        if (OptimizationNotes.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Optimization Notes:");
            foreach (var note in OptimizationNotes)
            {
                sb.AppendLine($"  - {note}");
            }
        }

        return sb.ToString();
    }
}