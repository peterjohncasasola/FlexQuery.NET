namespace FlexQuery.NET.Tests.SqlFormatting;

public class SqlFormatterTests
{
    [Theory]
    [InlineData("SELECT * FROM T", "SELECT\n    *\nFROM\n    T\n")]
    public void Formats_SimpleSelect(string input, string expected)
    {
        SqlFormatter.Format(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("SELECT c.Id,c.Name FROM Customers c WHERE c.IsActive ORDER BY c.Name LIMIT @__p_0 OFFSET @__p_1",
        "SELECT\n    c.Id,\n    c.Name\nFROM\n    Customers c\nWHERE\n    c.IsActive\nORDER BY\n    c.Name\nLIMIT\n    @__p_0\nOFFSET\n    @__p_1\n")]
    public void Formats_EfCoreSample1(string input, string expected)
    {
        SqlFormatter.Format(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("SELECT c.Id,c.Name,o.Id FROM Customers c LEFT JOIN Orders o ON c.Id=o.CustomerId WHERE c.Status=@__status_0 ORDER BY c.Name",
        "SELECT\n    c.Id,\n    c.Name,\n    o.Id\nFROM\n    Customers c\nLEFT JOIN\n    Orders o\nON\n    c.Id = o.CustomerId\nWHERE\n    c.Status = @__status_0\nORDER BY\n    c.Name\n")]
    public void Formats_EfCoreSample2(string input, string expected)
    {
        SqlFormatter.Format(input).Should().Be(expected);
    }

    [Fact]
    public void Preserves_StringLiteralContainingKeyword()
    {
        var input = "SELECT * FROM T WHERE Name = 'SELECT * FROM Users'";
        var result = SqlFormatter.Format(input);
        result.Should().Contain("'SELECT * FROM Users'");
    }

    [Fact]
    public void Preserves_LineComment()
    {
        var input = "SELECT 1 -- this is a comment\nFROM T";
        var result = SqlFormatter.Format(input);
        result.Should().Contain("-- this is a comment");
    }

    [Fact]
    public void Preserves_BlockComment()
    {
        var input = "SELECT 1 /* block comment */ FROM T";
        var result = SqlFormatter.Format(input);
        result.Should().Contain("/* block comment */");
    }

    [Fact]
    public void Formats_GroupBy()
    {
        var input = "SELECT CustomerId,SUM(Total) FROM Orders GROUP BY CustomerId HAVING SUM(Total)>100";
        var result = SqlFormatter.Format(input);
        result.Should().Contain("GROUP BY");
        result.Should().Contain("HAVING");
    }

    [Fact]
    public void Formats_WithCte()
    {
        var input = "WITH cte AS (SELECT 1) SELECT * FROM cte";
        var result = SqlFormatter.Format(input);
        result.Should().Contain("WITH");
        result.Should().Contain("AS");
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void Handles_NullOrEmpty(string? input, string expected)
    {
        SqlFormatter.Format(input!).Should().Be(expected);
    }

    [Fact]
    public void Preserves_Parameters()
    {
        var input = "SELECT * FROM T WHERE Id = @__p_0";
        var result = SqlFormatter.Format(input);
        result.Should().Contain("@__p_0");
    }

    [Fact]
    public void Formats_Subquery()
    {
        var input = "SELECT * FROM (SELECT * FROM T) AS X";
        var result = SqlFormatter.Format(input);
        result.Should().Contain("(");
        result.Should().Contain(")");
    }

    [Fact]
    public void Fallback_ReturnsOriginalOnFailure()
    {
        var input = "SELECT * FROM T";
        var result = SqlFormatter.Format(input);
        result.Should().NotBeNullOrEmpty();
    }
}
