using DbLive.Deployers.Testing;
#if (mssql)
using DbLive.MSSQL;
#else
using DbLive.PostgreSQL;
#endif
using DbLive.xunit.SqlTest;
using System.Reflection;
using Xunit.Abstractions;

namespace MyDbNamespace.Tests;

/// <summary>
/// Deploys MyDb into a new database once per test class and drops the database after the tests.
/// The database server runs in Docker, so Docker must be running. To use a server of your own instead,
#if (mssql)
/// replace the builder with <c>new MsSqlFixtureBuilder(Assembly.Load("MyDb"), "&lt;connection string&gt;")</c>.
#else
/// replace the builder with <c>new PostgreSqlFixtureBuilder(Assembly.Load("MyDb"), "&lt;connection string&gt;")</c>.
#endif
/// </summary>
public class SqlTestFixture()
#if (mssql)
    : DbLiveTestFixture(new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb")), dropDatabaseOnComplete: true);
#else
    : DbLiveTestFixture(new PostgreSqlContainerFixtureBuilder(Assembly.Load("MyDb")), dropDatabaseOnComplete: true);
#endif

/// <summary>
/// Every SQL test file of MyDb is a test case of its own, named after its path: the files in Scripts/Tests and
/// the *.test.sql files in Scripts/Code.
/// </summary>
public class SqlTests(ITestOutputHelper output, SqlTestFixture fixture) : IClassFixture<SqlTestFixture>
{
    [SqlFact(TestFixture = typeof(SqlTestFixture))]
    public async Task Sql(string testFileRelativePath)
    {
        TestRunResult result = await fixture.Tester!.RunTestAsync(output.WriteLine, testFileRelativePath);
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }
}
