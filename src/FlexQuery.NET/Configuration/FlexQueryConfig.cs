using FlexQuery.NET.Options;

namespace FlexQuery.NET.Configuration;

public interface IFlexQueryConfig
{
    FlexQueryOptions Options { get; }
    Action<QueryExecutionOptions>? ConfigureExecution { get; }
}

public sealed class FlexQueryConfig : IFlexQueryConfig
{
    public FlexQueryOptions Options { get; }
    public Action<QueryExecutionOptions>? ConfigureExecution { get; private set; }

    public FlexQueryConfig()
    {
        Options = new FlexQueryOptions();
    }

    public FlexQueryConfig WithExecution(Action<QueryExecutionOptions> configure)
    {
        ConfigureExecution = configure;
        return this;
    }
}
