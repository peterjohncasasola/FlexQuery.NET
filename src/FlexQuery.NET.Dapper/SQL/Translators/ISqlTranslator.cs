using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Sql.Translators;

/// <summary>
/// Translates Core QueryOptions into SQL commands for Dapper execution.
/// </summary>
internal interface ISqlTranslator
{
    /// <summary>Translates QueryOptions into fully parameterized SQL.</summary>
    SqlCommand Translate(QueryOptions options);

    /// <summary>Translates QueryOptions aggregates list into parameterized SQL.</summary>
    SqlCommand TranslateAggregates(QueryOptions options);

}