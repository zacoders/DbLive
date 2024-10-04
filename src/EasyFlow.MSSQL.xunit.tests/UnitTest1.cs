
using Xunit.Abstractions;

namespace EasyFlow.MSSQL.xunit.tests;

public class DBTests
{
	private readonly ITestOutputHelper _output;
	public DBTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Test1()
	{

	}

	[SqlFact(SqlAssemblyName = "DemoMSSQL")]
	public void Sql(string sqlTestPath)
	{

	}

	[SqlFact(DisplayName = "SqlScript", SqlAssemblyName = "DemoMSSQL")]
	public void Sql2(string sqlTestPath)
	{
		//_output.WriteLine(DemoMSSQL.ProjectInfo.ProjectDir);
		//string projectNameVar = "DemoMSSQL_ProjectDir";
		//string? projectDir = Environment.GetEnvironmentVariable(projectNameVar);
		//_output.WriteLine($"{projectNameVar} = {projectDir}");
	}
}