
namespace EasyFlow.MSSQL.xunit.tests;

public class UnitTest1
{
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

	}
}