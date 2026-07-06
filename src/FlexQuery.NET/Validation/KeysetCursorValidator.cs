using FlexQuery.NET.Constants;
using FlexQuery.NET.Exceptions;

namespace FlexQuery.NET.Validation;

internal static class KeysetCursorValidator
{
    public static void Validate(
        IReadOnlyList<object?> cursorValues,
        IReadOnlyList<Type> keyTypes)
    {
        if (cursorValues.Count != keyTypes.Count)
        {
            var result = new ValidationResult();
            result.Errors.Add(new ValidationError(
                $"Cursor has {cursorValues.Count} value(s) but the query has {keyTypes.Count} ordering column(s).",
                ValidationErrorCodes.CursorMismatch));
            throw new QueryValidationException(
                $"Cursor has {cursorValues.Count} value(s) but the query has {keyTypes.Count} ordering column(s).",
                result);
        }

        for (var i = 0; i < cursorValues.Count; i++)
        {
            if (cursorValues[i] is not null) continue;
            if (keyTypes[i].IsValueType && Nullable.GetUnderlyingType(keyTypes[i]) is null)
            {
                var result = new ValidationResult();
                result.Errors.Add(new ValidationError(
                    $"Cursor value at position {i} is null but key type '{keyTypes[i].Name}' is not nullable.",
                    ValidationErrorCodes.CursorNullValue));
                throw new QueryValidationException(
                    $"Cursor value at position {i} is null but key type '{keyTypes[i].Name}' is not nullable.",
                    result);
            }
        }
    }
}
