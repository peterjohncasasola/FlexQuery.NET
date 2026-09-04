namespace FlexQuery.NET.Models;

/// <summary>
/// Describes a single field on the effective select result surface for a typed DTO query.
/// </summary>
/// <remarks>
/// A selected field keeps three separate identities:
/// <list type="bullet">
///   <item><description><see cref="SourceName"/> — the public/source field requested by the client.</description></item>
///   <item><description><see cref="SourcePropertyName"/> — the CLR property on <typeparamref name="TResponse"/> that holds the materialized value.</description></item>
///   <item><description><see cref="OutputName"/> — the public response field name (the alias when present, otherwise the source name).</description></item>
/// </list>
/// The alias is result/output metadata only; it never has to match a <typeparamref name="TResponse"/> property.
/// </remarks>
public sealed record SelectOutputField
{
    /// <summary>The public/source field requested by the client (e.g. <c>CustomerFullName</c>).</summary>
    public string SourceName { get; init; } = string.Empty;

    /// <summary>The CLR property on the response DTO that holds the materialized value.</summary>
    public string SourcePropertyName { get; init; } = string.Empty;

    /// <summary>The public response field name (alias when present, otherwise the source name).</summary>
    public string OutputName { get; init; } = string.Empty;

    /// <summary>
    /// Nested output fields for navigation selections (e.g. <c>Orders(OrderId,Status)</c>).
    /// Each child's <see cref="SourcePropertyName"/> is a property on the navigation's
    /// element type. Null when the field has no nested selection.
    /// </summary>
    public IReadOnlyList<SelectOutputField>? Children { get; init; }
}
