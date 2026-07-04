using System.Globalization;

namespace FlexQuery.NET.Internal;

/// <summary>Shared value formatting for all fluent builders. Mirrors FilterConditionBuilder.FormatValue.</summary>
internal static class FilterValueFormatter
{
    /// <summary>Formats a raw value to its string representation for storage in FilterCondition.Value.</summary>
    /// <param name="value">The raw value to format. Supports null, string, DateTime, bool, and IFormattable types.</param>
    /// <returns>The formatted string, or null if the input value was null.</returns>
    public static string? Format(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            DateTime dateTime => dateTime.ToString("o", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}
