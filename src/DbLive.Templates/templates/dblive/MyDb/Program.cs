using DbLive;
#if (mssql)
using DbLive.MSSQL;
#else
using DbLive.PostgreSQL;
#endif
using System.Reflection;

// Deploys the scripts of this project, embedded in its assembly, to a local database.
#if (mssql)
string connectionString = "Server=localhost;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True;";
#else
string connectionString = "Host=localhost;Database=mydb;Username=postgres;Password=postgres;";
#endif

await new DbLiveBuilder()
    .LogToConsole()
#if (mssql)
    .SqlServer()
#else
    .PostgreSQL()
#endif
    .SetDbConnection(connectionString)
    .SetProject(Assembly.GetExecutingAssembly())
    .CreateDeployer()
    .DeployAsync(DeployParameters.Default with
    {
        // For a local database only: a production deployer must not create the database.
        CreateDbIfNotExists = true
    });
