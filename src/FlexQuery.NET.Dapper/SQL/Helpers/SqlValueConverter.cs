using FlexQuery.NET.Security;
using System.ComponentModel;
using FlexQuery.NET.Dapper.Mapping;

namespace FlexQuery.NET.Dapper.Sql.Helpers;

/// <summary>
/// Converts a raw filter/having string value into the CLR type of the target property,
/// using the property's <see cref="TypeConverter"/> when one is resolvable. Falls back to
/// the original string on any resolution or conversion failure, matching original behavior.
/// Used by both <c>SqlWhereBuilder</c> (filter conditions) and <c>SqlTranslator</c> (HAVING),
/// which is why it lives as a shared helper rather than a private method of either.
/// </summary>
internal static class SqlValueConverter
{
    public static object? Convert(string field, string? value, IEntityMapping mapping)
    {
        if (value == null) return null;

        if (SafePropertyResolver.TryResolveChain(mapping.Type, field, out var chain) && chain.Count > 0)
        {
            var targetType = chain.Last().PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            try
            {
                var converter = TypeDescriptor.GetConverter(underlyingType);
                if (converter.CanConvertFrom(typeof(string)))
                {
                    return converter.ConvertFromInvariantString(value);
                }
            }
            catch { /* fallback to original string */ }
        }

        return value;
    }
}