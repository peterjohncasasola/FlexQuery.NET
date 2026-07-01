namespace FlexQuery.NET.Diagnostics;

public sealed class DiagnosticsDuration
{
    /// <summary>
    /// Total elapsed time across all stages, in milliseconds.
    /// </summary>
    public double TotalMs { get; init; }

    /// <summary>
    /// Time spent parsing the raw FlexQuery parameters, in milliseconds.
    /// </summary>
    public double? ParseMs { get; init; }

    /// <summary>
    /// Time spent translating the parsed query into a database-specific query (e.g. SQL), in milliseconds.
    /// </summary>
    public double? TranslateMs { get; init; }

    /// <summary>
    /// Time spent executing the query against the database, in milliseconds.
    /// </summary>
    public double? DatabaseMs { get; init; }

    /// <summary>
    /// Time spent materializing the database results into the final output, in milliseconds.
    /// </summary>
    public double? MaterializeMs { get; init; }
}