using FlexQuery.NET.Security;

namespace FlexQuery.NET.Tests.Security;

public class WildcardMatcherTests
{
    [Fact]
    public void IsMatch_ExactMatch_ReturnsTrue()
    {
        WildcardMatcher.IsMatch("Name", ["Name"]).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_CaseInsensitiveMatch_ReturnsTrue()
    {
        WildcardMatcher.IsMatch("name", ["Name"]).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_StarWildcard_ReturnsTrue()
    {
        WildcardMatcher.IsMatch("AnyField", ["*"]).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_PrefixWildcard_ReturnsTrue()
    {
        WildcardMatcher.IsMatch("UserName", ["User*"]).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_SuffixWildcard_ReturnsTrue()
    {
        WildcardMatcher.IsMatch("FullName", ["*Name"]).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_MiddleWildcard_ReturnsTrue()
    {
        WildcardMatcher.IsMatch("StartMiddleEnd", ["Start*End"]).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_NoMatch_ReturnsFalse()
    {
        WildcardMatcher.IsMatch("FirstName", ["LastName"]).Should().BeFalse();
    }

    [Fact]
    public void IsMatch_EmptyPatterns_ReturnsFalse()
    {
        WildcardMatcher.IsMatch("Name", []).Should().BeFalse();
    }

    [Fact]
    public void IsMatch_MultiplePatterns_OneMatches_ReturnsTrue()
    {
        WildcardMatcher.IsMatch("Name", ["Age", "Name", "City"]).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WildcardMatchesLiteralStarSuffix()
    {
        WildcardMatcher.IsMatch("Name*", ["Name*"]).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_MultipleAsterisks_MatchesCorrectly()
    {
        WildcardMatcher.IsMatch("A.B.C", ["A.*.C"]).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_DotNotation_MatchesCorrectly()
    {
        WildcardMatcher.IsMatch("Profile.Bio", ["Profile.*"]).Should().BeTrue();
    }
}
