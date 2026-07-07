using Dapper;
using FlexQuery.NET.Dapper.Sql.Models;

namespace FlexQuery.NET.Dapper.Sql.Adapters;

internal static class CommandParameterAdapter
{
    public static DynamicParameters CreateParametersFromCommand(SqlCommand command)
    {
        var parameters = new DynamicParameters();
        foreach (var param in command.Parameters)
        {
            var cleanName = param.Key.TrimStart('@', ':', '?');
            parameters.Add(cleanName, param.Value);
        }
        return parameters;
    }
}