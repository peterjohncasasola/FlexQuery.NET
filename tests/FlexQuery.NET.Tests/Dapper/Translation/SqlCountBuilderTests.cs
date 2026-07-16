using FlexQuery.NET.Dapper.Sql.Builders;

namespace FlexQuery.NET.Tests.Dapper.Translation;

public class SqlCountBuilderTests
{
    [Fact]
    public void ExtractCountSql_SimpleSelect_WrapsInCount()
    {
        var sql = "SELECT Id, Name FROM Users WHERE Age > 18";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT Id, Name FROM Users WHERE Age > 18) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_WithOrderBy_StripsOrderBy()
    {
        var sql = "SELECT Id, Name FROM Users ORDER BY Name";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT Id, Name FROM Users) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_WithOrderByAndLimit_StripsBoth()
    {
        var sql = "SELECT Id, Name FROM Users ORDER BY Name LIMIT 10";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT Id, Name FROM Users) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_WithOrderByAndOffset_StripsAll()
    {
        var sql = "SELECT Id, Name FROM Users ORDER BY Name OFFSET 5 ROWS FETCH NEXT 10 ROWS ONLY";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT Id, Name FROM Users) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_WithLimitAndOffset_StripsBoth()
    {
        var sql = "SELECT Id, Name FROM Users ORDER BY Id LIMIT 10 OFFSET 20";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT Id, Name FROM Users) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_WithSubqueryOrderBy_KeepsInnerOrderBy()
    {
        var sql = "SELECT * FROM (SELECT Id, ROW_NUMBER() OVER (ORDER BY Name) AS rn FROM Users) AS sub WHERE rn > 0 ORDER BY rn";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT * FROM (SELECT Id, ROW_NUMBER() OVER (ORDER BY Name) AS rn FROM Users) AS sub WHERE rn > 0) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_WithSubqueryLimit_KeepsInnerLimit()
    {
        var sql = "SELECT * FROM (SELECT Id, Name FROM Users LIMIT 10) AS sub ORDER BY Name";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT * FROM (SELECT Id, Name FROM Users LIMIT 10) AS sub) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_EmptySql_ReturnsWrappedEmpty()
    {
        var sql = "";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM () AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_NoOrderByOrLimit_ReturnsWrapped()
    {
        var sql = "SELECT * FROM Products WHERE Category = 'Books'";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT * FROM Products WHERE Category = 'Books') AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_OrderByInsideSubquery_NotStripped()
    {
        var sql = "SELECT Id, (SELECT COUNT(*) FROM OrderItems WHERE OrderItems.ParentId = Parents.Id ORDER BY OrderItems.Name) AS ItemCount FROM Parents";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT Id, (SELECT COUNT(*) FROM OrderItems WHERE OrderItems.ParentId = Parents.Id ORDER BY OrderItems.Name) AS ItemCount FROM Parents) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_LimitInsideSubquery_NotStripped()
    {
        var sql = "SELECT * FROM (SELECT Id, Name FROM Users ORDER BY Name LIMIT 5) AS top5 ORDER BY Id";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT * FROM (SELECT Id, Name FROM Users ORDER BY Name LIMIT 5) AS top5) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_AllClauses_StripsAll()
    {
        var sql = "SELECT Id, Name FROM Users WHERE Active = 1 ORDER BY Name LIMIT 10 OFFSET 20";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT Id, Name FROM Users WHERE Active = 1) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_OnlyOrderBy_StripsOrderBy()
    {
        var sql = "SELECT * FROM Products ORDER BY Price DESC";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT * FROM Products) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_OnlyLimit_StripsLimit()
    {
        var sql = "SELECT * FROM Products LIMIT 5";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT * FROM Products) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_OnlyOffset_StripsOffset()
    {
        var sql = "SELECT * FROM Products OFFSET 10";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT * FROM Products) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_OrderByInSubquery_NotStripped()
    {
        var sql = "SELECT * FROM (SELECT * FROM OrderItems ORDER BY Name) AS sorted WHERE sorted.Price > 100 ORDER BY sorted.Price";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT * FROM (SELECT * FROM OrderItems ORDER BY Name) AS sorted WHERE sorted.Price > 100) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_MultipleOrderByKeywords_StripsOutermostOnly()
    {
        var sql = "SELECT * FROM (SELECT * FROM OrderItems ORDER BY Name) AS sub ORDER BY sub.Price";
        var result = SqlCountBuilder.ExtractCountSql(sql);
        result.Should().Be("SELECT COUNT(1) FROM (SELECT * FROM (SELECT * FROM OrderItems ORDER BY Name) AS sub) AS CountTable");
    }
}
