namespace FlexQuery.NET.Dapper.Conventions;

/// <summary>
/// Default English pluralizer implementation.
/// </summary>
public class DefaultPluralizer : IPluralizer
{
    public string Pluralize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        // Simple english pluralization rules
        if (name.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
            !name.EndsWith("ay", StringComparison.OrdinalIgnoreCase) &&
            !name.EndsWith("ey", StringComparison.OrdinalIgnoreCase) &&
            !name.EndsWith("iy", StringComparison.OrdinalIgnoreCase) &&
            !name.EndsWith("oy", StringComparison.OrdinalIgnoreCase) &&
            !name.EndsWith("uy", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^1] + "ies";
        }

        if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("sh", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("z", StringComparison.OrdinalIgnoreCase))
        {
            return name + "es";
        }

        return name + "s";
    }
}
