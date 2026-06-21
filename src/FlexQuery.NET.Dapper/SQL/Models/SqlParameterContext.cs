using FlexQuery.NET.Dapper.Dialects;

namespace FlexQuery.NET.Dapper.Sql.Models;

/// <summary>
/// Owns parameter naming and storage for a single SQL translation. Replaces the
/// previously shared mutable `_parameterIndex` field on <c>SqlTranslator</c> and the
/// raw <c>Dictionary&lt;string, object?&gt;</c> that was threaded through every builder
/// method. One instance is created per <c>Translate</c>/<c>TranslateAggregates</c> call,
/// so there is no shared state between concurrent translations.
/// </summary>
internal sealed class SqlParameterContext(ISqlDialect dialect)
{
    private readonly Dictionary<string, object?> _parameters = new();
    private int _index;

    /// <summary>The accumulated parameter values, keyed by their dialect-formatted name.</summary>
    public IReadOnlyDictionary<string, object?> Parameters => _parameters;

    /// <summary>
    /// The raw, mutable parameter dictionary. Exposed for interop with collaborators outside this
    /// translation pipeline (e.g. <c>SqlCountTranslator</c>, <c>SqlExistsTranslator</c>) that accept
    /// a <c>Dictionary&lt;string, object?&gt;</c> directly and write into it by reference. Prefer
    /// <see cref="Add"/> within this assembly's builders.
    /// </summary>
    public Dictionary<string, object?> RawParameters => _parameters;

    /// <summary>Allocates the next sequential parameter name (e.g. "p0", "p1", ...) without storing a value.</summary>
    public string NextName() => dialect.CreateParameterName($"p{_index++}");

    /// <summary>Allocates a parameter name, stores the given value under it, and returns the name.</summary>
    public string Add(object? value)
    {
        var name = NextName();
        _parameters[name] = value;
        return name;
    }

    /// <summary>Stores a value under an explicitly named parameter (used for dialect-specific names like paging offsets).</summary>
    public void AddNamed(string name, object? value)
    {
        _parameters[name] = value;
    }
}