namespace DbLive.Tests.Deployers;

public class DeployLockResourceTests
{
	[Theory]
	[InlineData("Server=localhost;Database=MyDb;Trusted_Connection=True;", "MyDb")]
	[InlineData("Server=localhost;Initial Catalog=MyDb;Trusted_Connection=True;", "MyDb")]
	[InlineData("Host=localhost;Database=my_pg_db;Username=postgres", "my_pg_db")]
	public void Build_uses_database_name_from_connection_string(string connectionString, string expectedDatabase)
	{
		var connection = Substitute.For<IDbLiveDbConnection>();
		connection.ConnectionString.Returns(connectionString);

		string resource = DeployLockResource.Build(connection, new DbLiveSettings());

		Assert.Equal($"DbLive:Deploy:{expectedDatabase}", resource);
	}

	[Fact]
	public void Build_appends_project_id_when_set()
	{
		var connection = Substitute.For<IDbLiveDbConnection>();
		connection.ConnectionString.Returns("Server=localhost;Database=MyDb;Trusted_Connection=True;");

		string resource = DeployLockResource.Build(connection, new DbLiveSettings
		{
			ProjectId = "demo"
		});

		Assert.Equal("DbLive:Deploy:MyDb:demo", resource);
	}

	[Fact]
	public void Build_throws_when_database_name_missing()
	{
		var connection = Substitute.For<IDbLiveDbConnection>();
		connection.ConnectionString.Returns("Server=localhost;Trusted_Connection=True;");

		DeployLockFailedException ex = Assert.Throws<DeployLockFailedException>(() =>
			DeployLockResource.Build(connection, new DbLiveSettings()));

		Assert.Contains("database name", ex.Message, StringComparison.OrdinalIgnoreCase);
	}
}
