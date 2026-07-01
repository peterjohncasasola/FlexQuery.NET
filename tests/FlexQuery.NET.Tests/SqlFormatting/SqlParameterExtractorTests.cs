namespace FlexQuery.NET.Tests.SqlFormatting;

public class SqlParameterExtractorTests
{
    [Fact]
    public void Extracts_SqliteParamSetLines()
    {
        var sql = @".param set @__p_1 20
                .param set @__p_0 0

                SELECT c.""Id"", c.""Name""
                FROM ""Customers"" AS c
                WHERE c.""IsActive""
                ORDER BY c.""Name""
                LIMIT @__p_1
                OFFSET @__p_0;";

        var (result, parameters) = SqlParameterExtractor.Extract(sql);

        result.Should().Contain("SELECT");
        result.Should().Contain("FROM");
        result.Should().NotContain(".param set");
        parameters.Should().HaveCount(2);
        parameters.Should().Contain(p => p.Name == "@__p_1" && p.Value != null && p.Value.Equals(20));
        parameters.Should().Contain(p => p.Name == "@__p_0" && p.Value != null && p.Value.Equals(0));
    }

    [Fact]
    public void Extracts_Parameters_WithPreservedSqlPlaceholders()
    {
        var sql = $"SELECT * FROM T WHERE Id = @__p_0 AND Name = @__p_1";

        var (result, parameters) = SqlParameterExtractor.Extract(sql);

        result.Should().Be(sql);
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void Handles_NullOrEmptyInput()
    {
        var (result1, params1) = SqlParameterExtractor.Extract(null);
        var (result2, params2) = SqlParameterExtractor.Extract("");
        var (result3, params3) = SqlParameterExtractor.Extract("   ");

        result1.Should().Be("");
        params1.Should().BeEmpty();
        result2.Should().Be("");
        params2.Should().BeEmpty();
        result3.Should().Be("   ");
        params3.Should().BeEmpty();
    }

    [Fact]
    public void Strips_ParamLines_AndPreservesBlankLines()
    {
        var sql = @".param set @name 'hello'

SELECT 1";

        var (result, parameters) = SqlParameterExtractor.Extract(sql);

        result.Should().Contain("SELECT 1");
        result.Should().NotContain(".param set");
        parameters.Should().HaveCount(1);
        parameters[0].Name.Should().Be("@name");
        parameters[0].Value.Should().Be("hello");
    }

    [Fact]
    public void Handles_ParamLineWithoutValue()
    {
        var sql = ".param set @name";

        var (result, parameters) = SqlParameterExtractor.Extract(sql);

        result.Should().Be("");
        parameters.Should().HaveCount(1);
        parameters[0].Name.Should().Be("@name");
        parameters[0].Value.Should().BeNull();
    }
}
