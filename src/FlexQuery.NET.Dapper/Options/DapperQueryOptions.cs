using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Metadata;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Dapper.Options;

public sealed class DapperQueryOptions : BaseQueryOptions
{
    public DapperQueryOptions()
    {
        IncludeTotalCount = true;
    }

    internal FlexQueryModel? Model { get; private set; }

    public void UseModel(FlexQueryModel model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public ISqlDialect? Dialect { get; set; }

    public int CommandTimeoutSeconds { get; set; } = 30;
}
