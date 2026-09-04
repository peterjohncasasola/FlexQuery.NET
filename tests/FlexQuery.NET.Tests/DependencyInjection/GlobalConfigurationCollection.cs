using Xunit;

namespace FlexQuery.NET.Tests.DependencyInjection;

/// <summary>
/// Serializes test classes that mutate FlexQuery's global configuration statics
/// (<c>FlexQueryCore.Configure/Reset</c>, <c>FlexQueryEFCore.Reset</c>, parser
/// registrations, ...). Without this, parallel test classes race on the shared
/// globals (e.g. one class's Reset un-configures while another class's Configure
/// runs, producing intermittent "already been configured" failures).
/// </summary>
[CollectionDefinition("GlobalConfiguration")]
public sealed class GlobalConfigurationCollection
{
}
