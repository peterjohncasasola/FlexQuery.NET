using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Helpers;

namespace FlexQuery.NET.Validation;

internal static class IdentifierValidator
{
    public static void ValidateAlias(string alias, string parameterKey)
    {
        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(parameterKey))
        {
            throw new QueryValidationException("Alias cannot be null or empty.");
        }
        
        if (string.IsNullOrWhiteSpace(parameterKey))
        {
            throw new QueryValidationException($"Parameter '{nameof(parameterKey)}' argument cannot be null or empty.");
        }
            
        if (ReservedKeywordHelper.IsReserved(alias))
        {
            throw new QueryValidationException($"Invalid alias '{alias}' in '{parameterKey}' parameter. '{alias}' is a reserved keyword and cannot be used as an alias.");
        }
    }
}