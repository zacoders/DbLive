namespace EasySqlFlow.Tests;

[TestClass]
public class DeploySqlTest : TestsBase
{
	[TestMethod]
	public void FirstTest()
	{
		var mockSet = new MockSet();

		var deploy = new EasyFlowDeploy(mockSet.EasyFlowProject.Object, mockSet.EasyFlowDA.Object);

		var migrations = deploy.GetMigrationsToApply("", "");

		Assert.AreEqual(4, migrations.Count());
	}
}