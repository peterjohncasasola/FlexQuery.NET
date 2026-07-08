namespace FlexQuery.NET.Dapper.Options;

public static class DapperQueryOptionsExtensions
{
    public static DapperQueryOptions UseSqlServer(this DapperQueryOptions options)
    {
        options.Dialect = new Dialects.SqlServerDialect();
        return options;
    }

    public static DapperQueryOptions UsePostgreSql(this DapperQueryOptions options)
    {
        options.Dialect = new Dialects.PostgreSqlDialect();
        return options;
    }

    public static DapperQueryOptions UseSqlite(this DapperQueryOptions options)
    {
        options.Dialect = new Dialects.SqliteDialect();
        return options;
    }
    
    public static DapperQueryOptions UseMariaDb(this DapperQueryOptions options)
    {
        options.Dialect = new Dialects.MariaDbDialect();
        return options;
    }
    
    public static DapperQueryOptions UseMySql(this DapperQueryOptions options)
    {
        options.Dialect = new Dialects.MySqlDialect();
        return options;
    }
    
    public static DapperQueryOptions UseOracle(this DapperQueryOptions options)
    {
        options.Dialect = new Dialects.OracleDialect();
        ;
        return options;
    }
}
