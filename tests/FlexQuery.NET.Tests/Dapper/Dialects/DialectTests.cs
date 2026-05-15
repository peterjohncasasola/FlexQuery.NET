using FlexQuery.NET.Models;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Dialects;
using FluentAssertions;

namespace FlexQuery.NET.Tests.Dapper.Dialects;

public class DialectTests
{
    private readonly IMappingRegistry _registry = new MappingRegistry();
    

    // ========================
    // Pagination Tests
    // ========================

    [Fact]
    public void SqlServer_Pagination_Uses_Offset_Fetch()
    {
        var options = CreatePagedOptions();
        var command = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);

        command.Sql.Should().Contain("OFFSET");
        command.Sql.Should().Contain("FETCH NEXT");
        command.Sql.Should().Contain("ROWS ONLY");
    }

    [Fact]
    public void PostgreSQL_Pagination_Uses_Offset_Limit()
    {
        var options = CreatePagedOptions();
        var command = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);

        command.Sql.Should().Contain("OFFSET");
        command.Sql.Should().Contain("LIMIT");
        // PostgreSQL uses: LIMIT y OFFSET x
        var limitIndex = command.Sql.IndexOf("LIMIT");
        var offsetIndex = command.Sql.IndexOf("OFFSET");
        limitIndex.Should().BeLessThan(offsetIndex);
    }

    [Fact]
    public void MySQL_Pagination_Uses_Limit_Offset()
    {
        var options = CreatePagedOptions();
        var command = new SqlTranslator(_registry, new MySqlDialect()).Translate(options);

        command.Sql.Should().Contain("LIMIT");
        command.Sql.Should().Contain("OFFSET");
        // MySQL uses: LIMIT y OFFSET x
        var limitIndex = command.Sql.IndexOf("LIMIT");
        var offsetIndex = command.Sql.IndexOf("OFFSET");
        limitIndex.Should().BeLessThan(offsetIndex);
    }

    [Fact]
    public void MariaDb_Pagination_Uses_Limit_Offset()
    {
        var options = CreatePagedOptions();
        var command = new SqlTranslator(_registry, new MariaDbDialect()).Translate(options);

        command.Sql.Should().Contain("LIMIT");
        command.Sql.Should().Contain("OFFSET");
        // MariaDB uses: LIMIT y OFFSET x
        var limitIndex = command.Sql.IndexOf("LIMIT");
        var offsetIndex = command.Sql.IndexOf("OFFSET");
        limitIndex.Should().BeLessThan(offsetIndex);
    }

    [Fact]
    public void Sqlite_Pagination_Uses_Limit_Offset()
    {
        var options = CreatePagedOptions();
        var command = new SqlTranslator(_registry, new SqliteDialect()).Translate(options);

        command.Sql.Should().Contain("LIMIT");
        command.Sql.Should().Contain("OFFSET");
        // SQLite uses: LIMIT y OFFSET x
        var limitIndex = command.Sql.IndexOf("LIMIT");
        var offsetIndex = command.Sql.IndexOf("OFFSET");
        limitIndex.Should().BeLessThan(offsetIndex);
    }

    [Fact]
    public void Oracle_Pagination_Uses_Offset_Fetch()
    {
        var options = CreatePagedOptions();
        var command = new SqlTranslator(_registry, new OracleDialect()).Translate(options);

        command.Sql.Should().Contain("OFFSET");
        command.Sql.Should().Contain("FETCH NEXT");
        command.Sql.Should().Contain("ROWS ONLY");
    }

    // ========================
    // Identifier Escaping Tests
    // ========================

    [Fact]
    public void SqlServer_QuoteIdentifier_Uses_Brackets()
    {
        var dialect = new SqlServerDialect();
        dialect.QuoteIdentifier("ColumnName").Should().Be("[ColumnName]");
    }

    [Fact]
    public void PostgreSQL_QuoteIdentifier_Uses_DoubleQuotes()
    {
        var dialect = new PostgreSqlDialect();
        dialect.QuoteIdentifier("ColumnName").Should().Be("\"ColumnName\"");
    }

    [Fact]
    public void MySQL_QuoteIdentifier_Uses_Backticks()
    {
        var dialect = new MySqlDialect();
        dialect.QuoteIdentifier("ColumnName").Should().Be("`ColumnName`");
    }

    [Fact]
    public void MariaDb_QuoteIdentifier_Uses_Backticks()
    {
        var dialect = new MariaDbDialect();
        dialect.QuoteIdentifier("ColumnName").Should().Be("`ColumnName`");
    }

    [Fact]
    public void Sqlite_QuoteIdentifier_Uses_DoubleQuotes()
    {
        var dialect = new SqliteDialect();
        dialect.QuoteIdentifier("ColumnName").Should().Be("\"ColumnName\"");
    }

    [Fact]
    public void Oracle_QuoteIdentifier_Uses_DoubleQuotes()
    {
        var dialect = new OracleDialect();
        dialect.QuoteIdentifier("ColumnName").Should().Be("\"ColumnName\"");
    }

    // ========================
    // Quote Character Tests
    // ========================

    [Fact]
    public void SqlServer_QuoteChars_Are_Brackets()
    {
        var dialect = new SqlServerDialect();
        dialect.QuotePrefix.Should().Be('[');
        dialect.QuoteSuffix.Should().Be(']');
    }

    [Fact]
    public void PostgreSQL_QuoteChars_Are_DoubleQuotes()
    {
        var dialect = new PostgreSqlDialect();
        dialect.QuotePrefix.Should().Be('"');
        dialect.QuoteSuffix.Should().Be('"');
    }

    [Fact]
    public void MySQL_QuoteChars_Are_Backticks()
    {
        var dialect = new MySqlDialect();
        dialect.QuotePrefix.Should().Be('`');
        dialect.QuoteSuffix.Should().Be('`');
    }

    [Fact]
    public void MariaDb_QuoteChars_Are_Backticks()
    {
        var dialect = new MariaDbDialect();
        dialect.QuotePrefix.Should().Be('`');
        dialect.QuoteSuffix.Should().Be('`');
    }

    [Fact]
    public void Sqlite_QuoteChars_Are_DoubleQuotes()
    {
        var dialect = new SqliteDialect();
        dialect.QuotePrefix.Should().Be('"');
        dialect.QuoteSuffix.Should().Be('"');
    }

    [Fact]
    public void Oracle_QuoteChars_Are_DoubleQuotes()
    {
        var dialect = new OracleDialect();
        dialect.QuotePrefix.Should().Be('"');
        dialect.QuoteSuffix.Should().Be('"');
    }

    // ========================
    // Parameter Prefix Tests
    // ========================

    [Fact]
    public void SqlServer_ParameterPrefix_IsAtSign()
    {
        new SqlServerDialect().ParameterPrefix.Should().Be("@");
    }

    [Fact]
    public void PostgreSQL_ParameterPrefix_IsColon()
    {
        new PostgreSqlDialect().ParameterPrefix.Should().Be(":");
    }

    [Fact]
    public void MySQL_ParameterPrefix_IsQuestionMark()
    {
        new MySqlDialect().ParameterPrefix.Should().Be("?");
    }

    [Fact]
    public void MariaDb_ParameterPrefix_IsQuestionMark()
    {
        new MariaDbDialect().ParameterPrefix.Should().Be("?");
    }

    [Fact]
    public void Sqlite_ParameterPrefix_IsAtSign()
    {
        new SqliteDialect().ParameterPrefix.Should().Be("@");
    }

    [Fact]
    public void Oracle_ParameterPrefix_IsColon()
    {
        new OracleDialect().ParameterPrefix.Should().Be(":");
    }

    // ========================
    // Parameter Name Generation Tests
    // ========================

    [Fact]
    public void SqlServer_CreateParameterName_IncludesAtSign()
    {
        new SqlServerDialect().CreateParameterName("Offset").Should().Be("@Offset");
    }

    [Fact]
    public void PostgreSQL_CreateParameterName_IncludesColon()
    {
        new PostgreSqlDialect().CreateParameterName("Offset").Should().Be(":Offset");
    }

    [Fact]
    public void MySQL_CreateParameterName_IncludesQuestionMark()
    {
        new MySqlDialect().CreateParameterName("Offset").Should().Be("?Offset");
    }

    [Fact]
    public void MariaDb_CreateParameterName_IncludesQuestionMark()
    {
        new MariaDbDialect().CreateParameterName("Offset").Should().Be("?Offset");
    }

    [Fact]
    public void Sqlite_CreateParameterName_IncludesAtSign()
    {
        new SqliteDialect().CreateParameterName("Offset").Should().Be("@Offset");
    }

    [Fact]
    public void Oracle_CreateParameterName_IncludesColon()
    {
        new OracleDialect().CreateParameterName("Offset").Should().Be(":Offset");
    }

    // ========================
    // COUNT Expression Tests
    // ========================

    [Fact]
    public void All_Dialects_Use_Count1_Expression()
    {
        new SqlServerDialect().GetCountExpression.Should().Be("COUNT(1)");
        new PostgreSqlDialect().GetCountExpression.Should().Be("COUNT(1)");
        new MySqlDialect().GetCountExpression.Should().Be("COUNT(1)");
        new MariaDbDialect().GetCountExpression.Should().Be("COUNT(1)");
        new SqliteDialect().GetCountExpression.Should().Be("COUNT(1)");
        new OracleDialect().GetCountExpression.Should().Be("COUNT(1)");
    }

    // ========================
    // Boolean Literal Tests
    // ========================

    [Fact]
    public void SqlServer_BooleanLiterals_Are_1_And_0()
    {
        var dialect = new SqlServerDialect();
        dialect.BooleanTrue.Should().Be("1");
        dialect.BooleanFalse.Should().Be("0");
    }

    [Fact]
    public void PostgreSQL_BooleanLiterals_Are_True_False()
    {
        var dialect = new PostgreSqlDialect();
        dialect.BooleanTrue.Should().Be("TRUE");
        dialect.BooleanFalse.Should().Be("FALSE");
    }

    [Fact]
    public void MySQL_BooleanLiterals_Are_True_False()
    {
        var dialect = new MySqlDialect();
        dialect.BooleanTrue.Should().Be("TRUE");
        dialect.BooleanFalse.Should().Be("FALSE");
    }

    [Fact]
    public void MariaDb_BooleanLiterals_Are_True_False()
    {
        var dialect = new MariaDbDialect();
        dialect.BooleanTrue.Should().Be("TRUE");
        dialect.BooleanFalse.Should().Be("FALSE");
    }

    [Fact]
    public void Sqlite_BooleanLiterals_Are_1_And_0()
    {
        var dialect = new SqliteDialect();
        dialect.BooleanTrue.Should().Be("1");
        dialect.BooleanFalse.Should().Be("0");
    }

    [Fact]
    public void Oracle_BooleanLiterals_Are_1_And_0()
    {
        var dialect = new OracleDialect();
        dialect.BooleanTrue.Should().Be("1");
        dialect.BooleanFalse.Should().Be("0");
    }

    // ========================
    // String Concatenation Tests
    // ========================

    [Fact]
    public void SqlServer_Uses_Plus_For_Concatenation()
    {
        var dialect = new SqlServerDialect();
        dialect.Concatenate("a", "b").Should().Be("a + b");
    }

    [Fact]
    public void PostgreSQL_Uses_PipePipe_For_Concatenation()
    {
        var dialect = new PostgreSqlDialect();
        dialect.Concatenate("a", "b").Should().Be("a || b");
    }

    [Fact]
    public void MySQL_Uses_Concat_For_Concatenation()
    {
        var dialect = new MySqlDialect();
        dialect.Concatenate("a", "b").Should().Be("CONCAT(a, b)");
    }

    [Fact]
    public void MariaDb_Uses_Concat_For_Concatenation()
    {
        var dialect = new MariaDbDialect();
        dialect.Concatenate("a", "b").Should().Be("CONCAT(a, b)");
    }

    [Fact]
    public void Sqlite_Uses_PipePipe_For_Concatenation()
    {
        var dialect = new SqliteDialect();
        dialect.Concatenate("a", "b").Should().Be("a || b");
    }

    [Fact]
    public void Oracle_Uses_PipePipe_For_Concatenation()
    {
        var dialect = new OracleDialect();
        dialect.Concatenate("a", "b").Should().Be("a || b");
    }

    // ========================
    // Limit Expression (Top-N) Tests
    // ========================

    [Fact]
    public void SqlServer_LimitExpression_Uses_Top()
    {
        var dialect = new SqlServerDialect();
        dialect.GetLimitExpression("@p0").Should().Be("TOP (@p0)");
    }

    [Fact]
    public void PostgreSQL_LimitExpression_Uses_Limit()
    {
        var dialect = new PostgreSqlDialect();
        dialect.GetLimitExpression("?p0").Should().Be("LIMIT ?p0");
    }

    [Fact]
    public void MySQL_LimitExpression_Uses_Limit()
    {
        var dialect = new MySqlDialect();
        dialect.GetLimitExpression("?p0").Should().Be("LIMIT ?p0");
    }

    [Fact]
    public void MariaDb_LimitExpression_Uses_Limit()
    {
        var dialect = new MariaDbDialect();
        dialect.GetLimitExpression("?p0").Should().Be("LIMIT ?p0");
    }

    [Fact]
    public void Sqlite_LimitExpression_Uses_Limit()
    {
        var dialect = new SqliteDialect();
        dialect.GetLimitExpression("@p0").Should().Be("LIMIT @p0");
    }

    [Fact]
    public void Oracle_LimitExpression_Uses_FetchFirst()
    {
        var dialect = new OracleDialect();
        dialect.GetLimitExpression(":p0").Should().Be("FETCH FIRST :p0 ROWS ONLY");
    }

    // ========================
    // Full SQL Generation Tests
    // ========================

    [Fact]
    public void SqlServer_Generates_Correct_Select_SQL()
    {
        var options = CreatePagedOptions();
        var command = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);

        command.Sql.Should().Contain("SELECT");
        command.Sql.Should().Contain("FROM");
        command.Sql.Should().Contain("OFFSET");
        command.Sql.Should().Contain("FETCH NEXT");
        command.Parameters.Should().ContainKey("@Offset");
        command.Parameters.Should().ContainKey("@PageSize");
    }

    [Fact]
    public void PostgreSQL_Generates_Correct_Select_SQL()
    {
        var options = CreatePagedOptions();
        var command = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);

        command.Sql.Should().Contain("SELECT");
        command.Sql.Should().Contain("FROM");
        command.Sql.Should().Contain("OFFSET");
        command.Sql.Should().Contain("LIMIT");
        command.Parameters.Should().ContainKey(":Offset");
        command.Parameters.Should().ContainKey(":PageSize");
    }

    [Fact]
    public void MySQL_Generates_Correct_Select_SQL()
    {
        var options = CreatePagedOptions();
        var command = new SqlTranslator(_registry, new MySqlDialect()).Translate(options);

        command.Sql.Should().Contain("SELECT");
        command.Sql.Should().Contain("FROM");
        command.Sql.Should().Contain("LIMIT");
        command.Sql.Should().Contain("OFFSET");
        command.Parameters.Should().ContainKey("?Offset");
        command.Parameters.Should().ContainKey("?PageSize");
    }

    [Fact]
    public void MariaDb_Generates_Correct_Select_SQL()
    {
        var options = CreatePagedOptions();
        var command = new SqlTranslator(_registry, new MariaDbDialect()).Translate(options);

        command.Sql.Should().Contain("SELECT");
        command.Sql.Should().Contain("FROM");
        command.Sql.Should().Contain("LIMIT");
        command.Sql.Should().Contain("OFFSET");
        command.Parameters.Should().ContainKey("?Offset");
        command.Parameters.Should().ContainKey("?PageSize");
    }

    [Fact]
    public void Sqlite_Generates_Correct_Select_SQL()
    {
        var options = CreatePagedOptions();
        var command = new SqlTranslator(_registry, new SqliteDialect()).Translate(options);

        command.Sql.Should().Contain("SELECT");
        command.Sql.Should().Contain("FROM");
        command.Sql.Should().Contain("LIMIT");
        command.Sql.Should().Contain("OFFSET");
        command.Parameters.Should().ContainKey("@Offset");
        command.Parameters.Should().ContainKey("@PageSize");
    }

    [Fact]
    public void Oracle_Generates_Correct_Select_SQL()
    {
        var options = CreatePagedOptions();
        var command = new SqlTranslator(_registry, new OracleDialect()).Translate(options);

        command.Sql.Should().Contain("SELECT");
        command.Sql.Should().Contain("FROM");
        command.Sql.Should().Contain("OFFSET");
        command.Sql.Should().Contain("FETCH NEXT");
        command.Parameters.Should().ContainKey(":Offset");
        command.Parameters.Should().ContainKey(":PageSize");
    }

    // ========================
    // Filter SQL Generation Tests
    // ========================

    [Fact]
    public void All_Dialects_Generate_Where_Clause_For_Equal_Filter()
    {
        var options = CreateFilteredOptions();

        var sqlServerCmd = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);
        var pgCmd = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);
        var mySqlCmd = new SqlTranslator(_registry, new MySqlDialect()).Translate(options);
        var mariadbCmd = new SqlTranslator(_registry, new MariaDbDialect()).Translate(options);
        var sqliteCmd = new SqlTranslator(_registry, new SqliteDialect()).Translate(options);
        var oracleCmd = new SqlTranslator(_registry, new OracleDialect()).Translate(options);

        sqlServerCmd.Sql.Should().Contain("WHERE");
        pgCmd.Sql.Should().Contain("WHERE");
        mySqlCmd.Sql.Should().Contain("WHERE");
        mariadbCmd.Sql.Should().Contain("WHERE");
        sqliteCmd.Sql.Should().Contain("WHERE");
        oracleCmd.Sql.Should().Contain("WHERE");
    }

    [Fact]
    public void All_Dialects_Generate_Like_Clause_For_Contains_Filter()
    {
        var options = CreateContainsFilterOptions();

        var sqlServerCmd = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);
        var pgCmd = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);
        var mySqlCmd = new SqlTranslator(_registry, new MySqlDialect()).Translate(options);
        var mariadbCmd = new SqlTranslator(_registry, new MariaDbDialect()).Translate(options);
        var sqliteCmd = new SqlTranslator(_registry, new SqliteDialect()).Translate(options);
        var oracleCmd = new SqlTranslator(_registry, new OracleDialect()).Translate(options);

        sqlServerCmd.Sql.Should().Contain("LIKE");
        pgCmd.Sql.Should().Contain("LIKE");
        mySqlCmd.Sql.Should().Contain("LIKE");
        mariadbCmd.Sql.Should().Contain("LIKE");
        sqliteCmd.Sql.Should().Contain("LIKE");
        oracleCmd.Sql.Should().Contain("LIKE");
    }

    [Fact]
    public void All_Dialects_Generate_In_Clause()
    {
        var options = CreateInFilterOptions();

        var sqlServerCmd = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);
        var pgCmd = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);
        var mySqlCmd = new SqlTranslator(_registry, new MySqlDialect()).Translate(options);
        var mariadbCmd = new SqlTranslator(_registry, new MariaDbDialect()).Translate(options);
        var sqliteCmd = new SqlTranslator(_registry, new SqliteDialect()).Translate(options);
        var oracleCmd = new SqlTranslator(_registry, new OracleDialect()).Translate(options);

        sqlServerCmd.Sql.Should().Contain("IN");
        pgCmd.Sql.Should().Contain("IN");
        mySqlCmd.Sql.Should().Contain("IN");
        mariadbCmd.Sql.Should().Contain("IN");
        sqliteCmd.Sql.Should().Contain("IN");
        oracleCmd.Sql.Should().Contain("IN");
    }

    [Fact]
    public void All_Dialects_Generate_Between_Clause()
    {
        var options = CreateBetweenFilterOptions();

        var sqlServerCmd = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);
        var pgCmd = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);
        var mySqlCmd = new SqlTranslator(_registry, new MySqlDialect()).Translate(options);
        var mariadbCmd = new SqlTranslator(_registry, new MariaDbDialect()).Translate(options);
        var sqliteCmd = new SqlTranslator(_registry, new SqliteDialect()).Translate(options);
        var oracleCmd = new SqlTranslator(_registry, new OracleDialect()).Translate(options);

        sqlServerCmd.Sql.Should().Contain("BETWEEN");
        pgCmd.Sql.Should().Contain("BETWEEN");
        mySqlCmd.Sql.Should().Contain("BETWEEN");
        mariadbCmd.Sql.Should().Contain("BETWEEN");
        sqliteCmd.Sql.Should().Contain("BETWEEN");
        oracleCmd.Sql.Should().Contain("BETWEEN");
    }

    // ========================
    // Aggregate SQL Generation Tests
    // ========================

    [Fact]
    public void All_Dialects_Generate_Aggregate_Select()
    {
        var options = CreateAggregateOptions();

        var sqlServerCmd = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);
        var pgCmd = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);
        var mySqlCmd = new SqlTranslator(_registry, new MySqlDialect()).Translate(options);
        var mariadbCmd = new SqlTranslator(_registry, new MariaDbDialect()).Translate(options);
        var sqliteCmd = new SqlTranslator(_registry, new SqliteDialect()).Translate(options);
        var oracleCmd = new SqlTranslator(_registry, new OracleDialect()).Translate(options);

        sqlServerCmd.Sql.Should().Contain("COUNT");
        pgCmd.Sql.Should().Contain("COUNT");
        mySqlCmd.Sql.Should().Contain("COUNT");
        mariadbCmd.Sql.Should().Contain("COUNT");
        sqliteCmd.Sql.Should().Contain("COUNT");
        oracleCmd.Sql.Should().Contain("COUNT");
    }

    [Fact]
    public void All_Dialects_Generate_Quoted_Aggregate_Alias()
    {
        var options = CreateAggregateOptions();

        var sqlServerCmd = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);
        var pgCmd = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);
        var mySqlCmd = new SqlTranslator(_registry, new MySqlDialect()).Translate(options);
        var mariadbCmd = new SqlTranslator(_registry, new MariaDbDialect()).Translate(options);
        var sqliteCmd = new SqlTranslator(_registry, new SqliteDialect()).Translate(options);
        var oracleCmd = new SqlTranslator(_registry, new OracleDialect()).Translate(options);

        // Each dialect should quote the alias using its own identifier escaping
        sqlServerCmd.Sql.Should().Contain("[TotalCount]");
        pgCmd.Sql.Should().Contain("\"TotalCount\"");
        mySqlCmd.Sql.Should().Contain("`TotalCount`");
        mariadbCmd.Sql.Should().Contain("`TotalCount`");
        sqliteCmd.Sql.Should().Contain("\"TotalCount\"");
        oracleCmd.Sql.Should().Contain("\"TotalCount\"");
    }

    // ========================
    // OrderBy SQL Generation Tests
    // ========================

    [Fact]
    public void All_Dialects_Generate_OrderBy_Clause()
    {
        var options = CreateSortedOptions();

        var sqlServerCmd = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);
        var pgCmd = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);
        var mySqlCmd = new SqlTranslator(_registry, new MySqlDialect()).Translate(options);
        var mariadbCmd = new SqlTranslator(_registry, new MariaDbDialect()).Translate(options);
        var sqliteCmd = new SqlTranslator(_registry, new SqliteDialect()).Translate(options);
        var oracleCmd = new SqlTranslator(_registry, new OracleDialect()).Translate(options);

        sqlServerCmd.Sql.Should().Contain("ORDER BY");
        pgCmd.Sql.Should().Contain("ORDER BY");
        mySqlCmd.Sql.Should().Contain("ORDER BY");
        mariadbCmd.Sql.Should().Contain("ORDER BY");
        sqliteCmd.Sql.Should().Contain("ORDER BY");
        oracleCmd.Sql.Should().Contain("ORDER BY");
    }

    [Fact]
    public void All_Dialects_Generate_Descending_OrderBy()
    {
        var options = CreateSortedOptions(descending: true);

        var sqlServerCmd = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);
        var pgCmd = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);

        sqlServerCmd.Sql.Should().Contain("DESC");
        pgCmd.Sql.Should().Contain("DESC");
    }

    // ========================
    // Distinct SQL Generation Tests
    // ========================

    [Fact]
    public void All_Dialects_Generate_Distinct_Clause()
    {
        var options = CreateDistinctOptions();

        var sqlServerCmd = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);
        var pgCmd = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);
        var mySqlCmd = new SqlTranslator(_registry, new MySqlDialect()).Translate(options);
        var mariadbCmd = new SqlTranslator(_registry, new MariaDbDialect()).Translate(options);
        var sqliteCmd = new SqlTranslator(_registry, new SqliteDialect()).Translate(options);
        var oracleCmd = new SqlTranslator(_registry, new OracleDialect()).Translate(options);

        sqlServerCmd.Sql.Should().Contain("SELECT DISTINCT");
        pgCmd.Sql.Should().Contain("SELECT DISTINCT");
        mySqlCmd.Sql.Should().Contain("SELECT DISTINCT");
        mariadbCmd.Sql.Should().Contain("SELECT DISTINCT");
        sqliteCmd.Sql.Should().Contain("SELECT DISTINCT");
        oracleCmd.Sql.Should().Contain("SELECT DISTINCT");
    }

    // ========================
    // GroupBy SQL Generation Tests
    // ========================

    [Fact]
    public void All_Dialects_Generate_GroupBy_Clause()
    {
        var options = CreateGroupByOptions();

        var sqlServerCmd = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);
        var pgCmd = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);
        var mySqlCmd = new SqlTranslator(_registry, new MySqlDialect()).Translate(options);
        var mariadbCmd = new SqlTranslator(_registry, new MariaDbDialect()).Translate(options);
        var sqliteCmd = new SqlTranslator(_registry, new SqliteDialect()).Translate(options);
        var oracleCmd = new SqlTranslator(_registry, new OracleDialect()).Translate(options);

        sqlServerCmd.Sql.Should().Contain("GROUP BY");
        pgCmd.Sql.Should().Contain("GROUP BY");
        mySqlCmd.Sql.Should().Contain("GROUP BY");
        mariadbCmd.Sql.Should().Contain("GROUP BY");
        sqliteCmd.Sql.Should().Contain("GROUP BY");
        oracleCmd.Sql.Should().Contain("GROUP BY");
    }

    // ========================
    // Having SQL Generation Tests
    // ========================

    [Fact]
    public void All_Dialects_Generate_Having_Clause()
    {
        var options = CreateHavingOptions();

        var sqlServerCmd = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);
        var pgCmd = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);
        var mySqlCmd = new SqlTranslator(_registry, new MySqlDialect()).Translate(options);
        var mariadbCmd = new SqlTranslator(_registry, new MariaDbDialect()).Translate(options);
        var sqliteCmd = new SqlTranslator(_registry, new SqliteDialect()).Translate(options);
        var oracleCmd = new SqlTranslator(_registry, new OracleDialect()).Translate(options);

        sqlServerCmd.Sql.Should().Contain("HAVING");
        pgCmd.Sql.Should().Contain("HAVING");
        mySqlCmd.Sql.Should().Contain("HAVING");
        mariadbCmd.Sql.Should().Contain("HAVING");
        sqliteCmd.Sql.Should().Contain("HAVING");
        oracleCmd.Sql.Should().Contain("HAVING");
    }

    // ========================
    // Join SQL Generation Tests
    // ========================

    [Fact]
    public void All_Dialects_Generate_Join_Clause()
    {
        var entityWithJoin = new EntityMapping(typeof(TestEntityWithJoin), "users", null);
        entityWithJoin.MapJoin("Roles", typeof(object), "roles", "users.Id = roles.UserId");
        ((MappingRegistry)_registry).Register(entityWithJoin);

        var options = new QueryOptions
        {
            Includes = new List<string> { "Roles" }
        };
        options.Items["EntityType"] = typeof(TestEntityWithJoin);

        var sqlServerCmd = new SqlTranslator(_registry, new SqlServerDialect()).Translate(options);
        var pgCmd = new SqlTranslator(_registry, new PostgreSqlDialect()).Translate(options);
        var mySqlCmd = new SqlTranslator(_registry, new MySqlDialect()).Translate(options);
        var mariadbCmd = new SqlTranslator(_registry, new MariaDbDialect()).Translate(options);
        var sqliteCmd = new SqlTranslator(_registry, new SqliteDialect()).Translate(options);
        var oracleCmd = new SqlTranslator(_registry, new OracleDialect()).Translate(options);

        sqlServerCmd.Sql.Should().Contain("LEFT JOIN");
        pgCmd.Sql.Should().Contain("LEFT JOIN");
        mySqlCmd.Sql.Should().Contain("LEFT JOIN");
        mariadbCmd.Sql.Should().Contain("LEFT JOIN");
        sqliteCmd.Sql.Should().Contain("LEFT JOIN");
        oracleCmd.Sql.Should().Contain("LEFT JOIN");
    }

    // ========================
    // ISqlDialect Polymorphism Test
    // ========================

    [Fact]
    public void All_Dialects_Implement_ISqlDialect()
    {
        ISqlDialect sqlServer = new SqlServerDialect();
        ISqlDialect postgres = new PostgreSqlDialect();
        ISqlDialect mysql = new MySqlDialect();
        ISqlDialect mariaDb = new MariaDbDialect();
        ISqlDialect sqlite = new SqliteDialect();
        ISqlDialect oracle = new OracleDialect();

        // Verify they all implement the interface and return non-empty values
        sqlServer.ParameterPrefix.Should().NotBeNullOrEmpty();
        postgres.ParameterPrefix.Should().NotBeNullOrEmpty();
        mysql.ParameterPrefix.Should().NotBeNullOrEmpty();
        mariaDb.ParameterPrefix.Should().NotBeNullOrEmpty();
        sqlite.ParameterPrefix.Should().NotBeNullOrEmpty();
        oracle.ParameterPrefix.Should().NotBeNullOrEmpty();

        sqlServer.QuoteIdentifier("test").Should().NotBeNullOrEmpty();
        postgres.QuoteIdentifier("test").Should().NotBeNullOrEmpty();
        mysql.QuoteIdentifier("test").Should().NotBeNullOrEmpty();
        mariaDb.QuoteIdentifier("test").Should().NotBeNullOrEmpty();
        sqlite.QuoteIdentifier("test").Should().NotBeNullOrEmpty();
        oracle.QuoteIdentifier("test").Should().NotBeNullOrEmpty();

        sqlServer.GetCountExpression.Should().NotBeNullOrEmpty();
        postgres.GetCountExpression.Should().NotBeNullOrEmpty();
        mysql.GetCountExpression.Should().NotBeNullOrEmpty();
        mariaDb.GetCountExpression.Should().NotBeNullOrEmpty();
        sqlite.GetCountExpression.Should().NotBeNullOrEmpty();
        oracle.GetCountExpression.Should().NotBeNullOrEmpty();
    }

    // ========================
    // MySQL vs MariaDB Distinction Test
    // ========================

    [Fact]
    public void MySQL_And_MariaDb_Are_Separate_Implementations()
    {
        var mySql = new MySqlDialect();
        var mariaDb = new MariaDbDialect();

        // Both should be separate types
        mySql.Should().NotBeSameAs(mariaDb);
        mySql.GetType().Should().NotBe(mariaDb.GetType());

        // They should have the same parameter prefix and quoting style
        // but are independently replaceable
        mySql.ParameterPrefix.Should().Be(mariaDb.ParameterPrefix);
        mySql.QuotePrefix.Should().Be(mariaDb.QuotePrefix);
        mySql.QuoteSuffix.Should().Be(mariaDb.QuoteSuffix);

        // Both implement ISqlDialect
        ((ISqlDialect)mySql).GetCountExpression.Should().Be("COUNT(1)");
        ((ISqlDialect)mariaDb).GetCountExpression.Should().Be("COUNT(1)");
    }

    // ========================
    // Oracle-Specific Tests
    // ========================

    [Fact]
    public void Oracle_Has_Dedicated_Implementation()
    {
        var oracle = new OracleDialect();

        // Oracle should NOT be lumped with PostgreSQL even though both use : prefix
        oracle.ParameterPrefix.Should().Be(":");
        oracle.QuotePrefix.Should().Be('"');

        // Oracle uses 1/0 for booleans, not TRUE/FALSE keywords in SQL
        oracle.BooleanTrue.Should().Be("1");
        oracle.BooleanFalse.Should().Be("0");

        // Oracle-specific limit syntax
        oracle.GetLimitExpression(":p0").Should().Be("FETCH FIRST :p0 ROWS ONLY");
    }

    // ========================
    // SQLite-Specific Tests
    // ========================

    [Fact]
    public void Sqlite_Has_Dedicated_Implementation()
    {
        var sqlite = new SqliteDialect();

        // SQLite uses @ prefix (Microsoft.Data.Sqlite convention)
        sqlite.ParameterPrefix.Should().Be("@");

        // SQLite uses double-quote for identifiers
        sqlite.QuotePrefix.Should().Be('"');
        sqlite.QuoteSuffix.Should().Be('"');

        // SQLite uses 1/0 for booleans
        sqlite.BooleanTrue.Should().Be("1");
        sqlite.BooleanFalse.Should().Be("0");

        // SQLite uses standard LIMIT/OFFSET
        var paging = sqlite.GetPagingClause("@Offset", "@PageSize");
        paging.Should().Contain("LIMIT");
        paging.Should().Contain("OFFSET");
    }

    // ========================
    // MariaDB-Specific Tests
    // ========================

    [Fact]
    public void MariaDb_Has_Dedicated_Implementation()
    {
        var mariaDb = new MariaDbDialect();

        // MariaDB uses ? prefix
        mariaDb.ParameterPrefix.Should().Be("?");

        // MariaDB uses backtick quoting
        mariaDb.QuotePrefix.Should().Be('`');
        mariaDb.QuoteSuffix.Should().Be('`');

        // MariaDB supports TRUE/FALSE keywords
        mariaDb.BooleanTrue.Should().Be("TRUE");
        mariaDb.BooleanFalse.Should().Be("FALSE");

        // MariaDB uses standard MySQL-style LIMIT/OFFSET
        var paging = mariaDb.GetPagingClause("?Offset", "?PageSize");
        paging.Should().Contain("LIMIT");
        paging.Should().Contain("OFFSET");
    }

    // ========================
    // Helper Methods
    // ========================

    private static QueryOptions CreatePagedOptions()
    {
        var options = new QueryOptions
        {
            Paging = { Page = 2, PageSize = 10 }
        };
        options.Items["EntityType"] = typeof(TestEntity);
        return options;
    }

    private static QueryOptions CreateFilteredOptions()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "Test" }]
            }
        };
        options.Items["EntityType"] = typeof(TestEntity);
        return options;
    }

    private static QueryOptions CreateContainsFilterOptions()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "contains", Value = "test" }]
            }
        };
        options.Items["EntityType"] = typeof(TestEntity);
        return options;
    }

    private static QueryOptions CreateInFilterOptions()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Status", Operator = "in", Value = "Active,Pending" }]
            }
        };
        options.Items["EntityType"] = typeof(TestEntity);
        return options;
    }

    private static QueryOptions CreateBetweenFilterOptions()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Age", Operator = "between", Value = "20,30" }]
            }
        };
        options.Items["EntityType"] = typeof(TestEntity);
        return options;
    }

    private static QueryOptions CreateAggregateOptions()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = "count", Alias = "TotalCount", Field = "*" }]
        };
        options.Items["EntityType"] = typeof(TestEntity);
        return options;
    }

    private static QueryOptions CreateSortedOptions(bool descending = false)
    {
        var options = new QueryOptions
        {
            Sort = [new SortNode { Field = "Name", Descending = descending }]
        };
        options.Items["EntityType"] = typeof(TestEntity);
        return options;
    }

    private static QueryOptions CreateDistinctOptions()
    {
        var options = new QueryOptions
        {
            Distinct = true
        };
        options.Items["EntityType"] = typeof(TestEntity);
        return options;
    }

    private static QueryOptions CreateGroupByOptions()
    {
        var options = new QueryOptions
        {
            GroupBy = ["Status"]
        };
        options.Items["EntityType"] = typeof(TestEntity);
        return options;
    }

    private static QueryOptions CreateHavingOptions()
    {
        var options = new QueryOptions
        {
            GroupBy = ["Status"],
            Having = new HavingCondition
            {
                Field = "Amount",
                Operator = "gt",
                Value = "100",
                Function = "sum"
            }
        };
        options.Items["EntityType"] = typeof(TestEntity);
        return options;
    }

    private static QueryOptions CreateJoinOptions()
    {
        var registry = new MappingRegistry();
        var entityWithJoin = new EntityMapping(typeof(TestEntityWithJoin), "users", null);
        entityWithJoin.MapJoin("Roles", typeof(object), "roles", "users.Id = roles.UserId");
        ((MappingRegistry)registry).Register(entityWithJoin);

        var options = new QueryOptions
        {
            Includes = new List<string> { "Roles" }
        };
        options.Items["EntityType"] = typeof(TestEntityWithJoin);

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var _ = translator.Translate(options); // warm-up

        return options;
    }

    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string City { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private class TestEntityWithJoin
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
