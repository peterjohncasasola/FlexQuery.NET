namespace FlexQuery.NET.Models;

internal sealed class KeysetCursor(params object?[] values)
{
    public IReadOnlyList<object?> Values { get; } = values;
}
