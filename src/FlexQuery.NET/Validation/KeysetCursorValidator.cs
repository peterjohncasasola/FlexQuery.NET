using FlexQuery.NET.Exceptions;

namespace FlexQuery.NET.Validation;

internal static class KeysetCursorValidator
{
    public static void Validate(
        IReadOnlyList<object?> cursorValues,
        IReadOnlyList<Type> keyTypes)
    {
        if (cursorValues.Count != keyTypes.Count)
            throw new KeysetPaginationException(
                $"Cursor has {cursorValues.Count} value(s) but the query has {keyTypes.Count} ordering column(s).");

        for (var i = 0; i < cursorValues.Count; i++)
        {
            if (cursorValues[i] is not null) continue;
            if (keyTypes[i].IsValueType && Nullable.GetUnderlyingType(keyTypes[i]) is null)
                throw new KeysetPaginationException(
                    $"Cursor value at position {i} is null but key type '{keyTypes[i].Name}' is not nullable.");
        }
    }
}
