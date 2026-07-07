using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Metadata;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Dapper.Options;

/// <inheritdoc/>
public sealed class DapperQueryOptions : BaseQueryOptions
{
    /// <inheritdoc/>
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
