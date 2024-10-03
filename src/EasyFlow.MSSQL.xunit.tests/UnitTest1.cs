
namespace EasyFlow.MSSQL.xunit.tests;

public class UnitTest1
{
	[Fact]
	public void Test1()
	{

	}

	[SqlFact(SqlAssemblyName = "DemoMSSQL")]
	public void Sql()
	{

	}
}