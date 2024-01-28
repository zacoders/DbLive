
namespace EasyFlow.Tests.Project;

public class SqlTestingTests
{
	[Fact]
	public void GetTests()
	{
		var mockSet = new MockSet();

		mockSet.FileSystem.EnumerateDirectories(["Code1", "Tests"], "*", SearchOption.TopDirectoryOnly)
			.Returns([
				@"C:\DB\Tests\",
				@"C:\DB\Tests\Orders",
				@"C:\DB\Tests\Users"
			]);

		var sqlProject = new EasyFlowProject(mockSet.ProjectPath, mockSet.FileSystem);

		var tests = sqlProject.GetTests().ToArray();

		Assert.Equal(4, tests.Length);
		//Assert.Equal(1, tests[0].Version);
		//Assert.Equal(2, tests[1].Version);
		//Assert.Equal(3, tests[2].Version);
		//Assert.Equal(4, tests[3].Version);
	}
}