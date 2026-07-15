using FlexQuery.NET.Internal;

namespace FlexQuery.NET.Tests.Filters;

public class FilterValueFormatterTests
{
    [Fact]
    public void Format_Null_ReturnsNull()
    {
        FilterValueFormatter.Format(null).Should().BeNull();
    }

    [Fact]
    public void Format_String_ReturnsSame()
    {
        FilterValueFormatter.Format("hello").Should().Be("hello");
    }

    [Fact]
    public void Format_DateTime_ReturnsIso()
    {
        var dt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        FilterValueFormatter.Format(dt).Should().Be("2024-01-15T10:30:00.0000000Z");
    }

    [Fact]
    public void Format_Bool_ReturnsLowercase()
    {
        FilterValueFormatter.Format(true).Should().Be("true");
        FilterValueFormatter.Format(false).Should().Be("false");
    }

    [Fact]
    public void Format_Int_ReturnsString()
    {
        FilterValueFormatter.Format(42).Should().Be("42");
    }

    [Fact]
    public void Format_Decimal_ReturnsInvariant()
    {
        FilterValueFormatter.Format(3.14m).Should().Be("3.14");
    }
}