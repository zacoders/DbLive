
[TestClass]
public class TaskTypeTests : TestsBase
{
	private static DeploySQL Deploy = Resolve<DeploySQL>();

	[TestMethod]
	public void GetMigrationType()
	{
		var testingValues = new[] {
			"migration",
			"UNDO",
			"data",
			"testdata",
			"Breaking"
		};

        foreach (var migrationStr in testingValues)
        {
            _ = Deploy.GetMigrationType(migrationStr);
        }        
	}

	[TestMethod]
	[ExpectedException(typeof(UnknowMigrationTaskTypeException))]
	public void GetMigrationType_Unknow()
	{
		Deploy.GetMigrationType("test");
	}
}