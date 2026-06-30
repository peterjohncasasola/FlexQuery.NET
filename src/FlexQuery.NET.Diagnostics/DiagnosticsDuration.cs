namespace FlexQuery.NET.Diagnostics;

public sealed class DiagnosticsDuration
{
    public double TotalMs { get; init; }
    public double? ParseMs { get; init; }
    public double? TranslateMs { get; init; }
    public double? DatabaseMs { get; init; }
    public double? MaterializeMs { get; init; }
}