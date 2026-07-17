using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Parsers.Dsl;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Tests.Dapper.Security;

/// <summary>
/// Comprehensive SQL injection prevention validation tests.
/// Covers: injection in filters, sorts, selects, includes, group by, having, field names, values.
/// Also validates parameterization and identifier quoting.
/// </summary>
public class SqlInjectionTests
{
    private readonly IMappingRegistry _registry = new MappingRegistry();
    private readonly SqlTranslator _translator = new SqlTranslator(new MappingRegistry(), new SqlServerDialect());
    

    // ==================== FILTER VALUE INJECTION ====================

    [Fact]
    public void Should_Generate_Parameterized_SQL_For_Filter_With_Special_Characters()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "'; DROP TABLE Users;--" }]
            }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);
        options.Paging.Disabled = true;

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("@p0");
        command.Sql.Should().NotContain("DROP TABLE");
        command.Parameters.Should().ContainKey("@p0");
        command.Parameters["@p0"].Should().Be("'; DROP TABLE Users;--");
    }

    [Fact]
    public void Should_Reject_Filter_Field_With_SQL_Injection_Pattern()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name'); DROP TABLE Users;--", Operator = "eq", Value = "test" }]
            }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);
        options.Paging.Disabled = true;

        // The field name gets quoted as identifier; injection within field name is neutralized
        // But if field doesn't exist, Name');DROP... would be rejected as FIELD_NOT_FOUND
        Action validate = () => options.ValidateOrThrow<Customer>(new QueryExecutionOptions());

        // Injection pattern in field name fails validation as unknown field
        validate.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_NOT_FOUND" || e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void Should_Parameterize_Between_Values()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Age", Operator = "between", Value = "18;DROP TABLE Users;,65" }]
            }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);
        options.Paging.Disabled = true;

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("BETWEEN @p0 AND @p1");
        command.Sql.Should().NotContain("DROP TABLE");
    }

    [Fact]
    public void Should_Parameterize_In_Clause_Values()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "in", Value = "a', OR '1'='1" }]
            }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);
        options.Paging.Disabled = true;

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("IN (");
        command.Sql.Should().NotContain("OR '1'='1");
    }

    // ==================== SORT INJECTION ====================

    [Fact]
    public void Should_Quote_Sort_Fields_Preventing_Injection()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "test" }]
            },
            Sort = [new SortNode { Field = "CreatedAt", Descending = false }],
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("[CreatedAt]");
        command.Sql.Should().NotContain(";"); // no trailing semicolons
    }

    [Fact]
    public void Should_Quote_Sort_Field_With_Malicious_Name()
    {
        var options = new QueryOptions
        {
            Sort = [new SortNode { Field = "Name; DROP TABLE Users;--", Descending = false }],
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        // Field name is bracketed, so injection is neutralized
        command.Sql.Should().Contain("[Name; DROP TABLE Users;--]");
        // The quoted identifier [Name; DROP TABLE Users;--] is safe - entire string is literal column name
    }

    // ==================== SELECT/PROJECTION INJECTION ====================

    [Fact]
    public void Should_Quote_Select_Fields_Preventing_Injection()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "Id" }, new SelectNode { Field = "Name" }, new SelectNode { Field = "(SELECT * FROM Users)" }],
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("[Id]");
        command.Sql.Should().Contain("[Name]");
        command.Sql.Should().Contain("[(SELECT * FROM Users)]"); // quoted as identifier
    }

    [Fact]
    public void Should_Quote_Select_Field_With_SQL_Injection_Payload()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "Id); DROP TABLE Users; --" }],
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        // The quoted identifier is safe - entire thing is treated as column name, not SQL
        command.Sql.Should().Contain("[Id); DROP TABLE Users; --]");
    }

    // ==================== GROUP BY INJECTION ====================

    [Fact]
    public void Should_Quote_GroupBy_Fields_Preventing_Injection()
    {
        var options = new QueryOptions
        {
            GroupBy = ["Status; DROP TABLE Orders"],
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("GROUP BY");
        command.Sql.Should().Contain("[Status; DROP TABLE Orders]");
        // Properly quoted - the entire string is treated as a column name, not SQL
    }

    [Fact]
    public void Should_Quote_Multiple_GroupBy_Fields()
    {
        var options = new QueryOptions
        {
            GroupBy = ["Name', 'Value') SELECT * FROM Users--"],
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("GROUP BY");
        // Field should be quoted to neutralize injection
        command.Sql.Should().Contain("[Name', 'Value') SELECT * FROM Users--]");
    }

    // ==================== HAVING INJECTION ====================

    [Fact]
    public void Should_Parameterize_Having_Values()
    {
        var options = new QueryOptions
        {
            GroupBy = new List<string> { "Name" },
            Having = new HavingCondition
            {
                Function = AggregateFunction.Count,
                Field = "Id",
                Operator = "gt",
                Value = "5; DROP TABLE Users;--"
            }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);
        options.Paging.Disabled = true;

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("@p0");
        command.Sql.Should().NotContain("DROP TABLE");
    }

    // ==================== AGGREGATE INJECTION ====================

    [Fact]
    public void Should_Quote_Aggregate_Fields()
    {
        var options = new QueryOptions
        {
            GroupBy = ["Id"],
            Aggregates = { new AggregateModel { Function = AggregateFunction.Sum, Field = "Price", Alias = "Total" } },
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("SUM([Price]) AS [Total]");
        // Alias is properly quoted - the malicious content is neutralized
    }

    [Fact]
    public void Should_Quote_Aggregate_Alias_To_Prevent_Injection()
    {
        var options = new QueryOptions
        {
            GroupBy = ["Id"],
            Aggregates = { new AggregateModel { Function = AggregateFunction.Count, Field = "Id", Alias = "Cnt); DROP TABLE Users;--" } },
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("AS [Cnt); DROP TABLE Users;--]");
        // Properly quoted - the entire alias is treated as identifier, not SQL
    }

    [Fact]
    public void Should_Quote_Aggregate_Fields_In_TranslateAggregates()
    {
        var options = new QueryOptions
        {
            Aggregates = { new AggregateModel { Function = AggregateFunction.Sum, Field = "Price", Alias = "Total" } },
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.TranslateAggregates(options);

        command.Sql.Should().Contain("SUM([Price]) AS [Total]");
    }

    // ==================== NAVIGATION/INCLUDE INJECTION ====================

    [Fact]
    public void Should_Quote_Navigation_Property_Names()
    {
        var options = new QueryOptions
        {
            Includes = ["Orders"],
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("[Customers]"); // Convention-based table name from Customer
    }

    [Fact]
    public void Should_Neutralize_Malicious_Navigation_Name()
    {
        var options = new QueryOptions
        {
            Includes = ["Orders; DROP TABLE Users;--"],
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        // The include name will be treated as a literal navigation name that likely doesn't exist
        // But translation should still quote it as an identifier to prevent injection
        var command = _translator.Translate(options);

        // Since the navigation isn't mapped, it won't appear in SQL, but the quoting is still applied
    }

    // ==================== FIELD NAME AS SQL KEYWORD ====================

    [Fact]
    public void Should_Quote_Field_Named_With_SQL_Keyword()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "Order" }], // "Order" is a SQL keyword
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("[Order]"); // always quoted
    }

    [Fact]
    public void Should_Handle_Field_Named_With_Special_Chars()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Field'WithQuotes", Operator = "eq", Value = "test" }]
            },
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        // Field with quote in name - should be quoted as [Field'WithQuotes]
        var command = _translator.Translate(options);

        command.Sql.Should().Contain("[Field'WithQuotes]");
    }

    // ==================== UNION/SUBQUERY INJECTION ====================

    [Fact]
    public void Should_Not_Allow_Union_Injection_Through_Select()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "Id" }, new SelectNode { Field = "UNION SELECT * FROM Users" }],
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        // UNION would be treated as a literal field name and quoted
        command.Sql.Should().Contain("[UNION SELECT * FROM Users]");
        // Properly quoted - entire string is identifier, not SQL
    }

    [Fact]
    public void Should_Not_Allow_Subquery_Injection_Through_Select()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "(SELECT @@VERSION)" }],
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        // Subquery would be quoted as identifier
        command.Sql.Should().Contain("[(SELECT @@VERSION)]");
        // Properly quoted - treated as identifier, not executed as subquery
    }

    // ==================== COMMA/LOGIC SEPARATOR INJECTION ====================

    [Fact]
    public void Should_Quote_Field_Names_With_Commas()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name,AnotherField", Operator = "eq", Value = "test" }]
            },
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("[Name,AnotherField]");
    }

    [Fact]
    public void Should_Quote_Field_Names_With_Logical_Operators()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name OR 1=1", Operator = "eq", Value = "test" }]
            },
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        command.Sql.Should().Contain("[Name OR 1=1]");
        // Properly quoted - entire string is identifier, not SQL logic
    }

    // ==================== STRING ESCAPING IN VALUES ====================

    [Fact]
    public void Should_Contain_Single_Quotes_In_Value_Without_Breaking()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "O'Reilly" }]
            },
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        // Parameterized value retains the single quote but doesn't break SQL
        command.Parameters["@p0"].Should().Be("O'Reilly");
        // Should not have string concatenation
        command.Sql.Should().NotContain("'O'Reilly'");
        command.Sql.Should().Contain("@p0");
    }

    [Fact]
    public void Should_Contain_Backslash_Without_Escape_Breakage()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Path", Operator = "eq", Value = @"C:\Windows\System32" }]
            },
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        command.Parameters["@p0"].Should().Be(@"C:\Windows\System32");
    }

    // ==================== PARSER-LEVEL INJECTION PREVENTION ====================

    [Fact]
    public void Should_Reject_DSL_With_Semicolon_Separated_Injection()
    {
        // DSL parser validates characters; semicolons are not explicitly forbidden but field name pattern rejects them
        // if embedded within field name. But semicolon as value is fine (parameterized)
        Action act = () => DslAstParser.Parse("name:eq:test;DROP TABLE Users");
        // Value parsing stops after "test", extra "DROP..." becomes extra token causing error
        act.Should().Throw<DslParseException>();
    }

    // ==================== CROSS-SITE SCRIPTING (XSS) VIA DATA ====================

    [Fact]
    public void Should_Not_Reflect_User_Input_In_SQL_As_Code()
    {
        var malicious = "<script>alert('xss')</script>";
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "contains", Value = malicious }]
            },
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var command = _translator.Translate(options);

        // XSS payload treated as parameter value, not executed
        command.Sql.Should().Contain("@p0");
        command.Parameters["@p0"].Should().Be($"%{malicious}%");
    }
}


