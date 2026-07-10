using FlexQuery.NET.Parsers.MiniOData;
using FlexQuery.NET.Parsers.MiniOData.Models;

namespace FlexQuery.NET.Tests.MiniOData;

/// <summary>
/// Verifies that unsupported / deferred OData features fail with a clear <see cref="MiniODataParseException"/>
/// rather than being silently ignored.
/// </summary>
public class MiniODataUnsupportedFeaturesTests
{
    [Fact]
    public void Apply_Deferred_ThrowsClearException()
    {
        var request = new MiniODataRequest { Apply = "groupby((Category))" };

        Action act = () => ODataQueryParameterParser.Parse(request);

        act.Should().Throw<MiniODataParseException>()
            .WithMessage("*$apply*deferred*");
    }

    [Fact]
    public void Apply_Empty_DoesNotThrow()
    {
        // Empty $apply is ignored (consistent with other unset parameters).
        var request = new MiniODataRequest { Apply = "   " };

        Action act = () => ODataQueryParameterParser.Parse(request);

        act.Should().NotThrow();
    }

    [Fact]
    public void Expand_NestedQueryOption_ThrowsClearException()
    {
        Action act = () => MiniODataExpandParser.Parse("Orders($filter=Status eq 'Pending')");

        act.Should().Throw<MiniODataParseException>()
            .WithMessage("*Nested query options inside $expand*");
    }

    [Fact]
    public void ToQueryOptions_ApplyDeferred_ThrowsClearException()
    {
        var request = new MiniODataRequest { Apply = "groupby((Category),aggregate(Price with sum as Total))" };

        Action act = () => request.ToQueryOptions();

        act.Should().Throw<MiniODataParseException>()
            .WithMessage("*$apply*deferred*");
    }
}
