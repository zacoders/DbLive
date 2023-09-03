using System.Data.SqlClient;
using System.Transactions;

namespace TestProject1;

public class UnitTest1
{
	string cnnString = "Data Source=.;Initial Catalog=master;Integrated Security=True;";

	[Fact]
	public void Test1()
	{
		using TransactionScope transactionScope = new TransactionScope();

		SqlConnection cnn1 = new(cnnString);
		cnn1.Open();
		cnn1.Close();

		SqlConnection cnn2 = new(cnnString);
		cnn2.Open();
		cnn2.Close();

		transactionScope.Complete();
	}

	[Fact]
	public void EnumerateDirectoriesTest()
	{
		string path = @"D:\Data\Personal\EasySqlFlow\src\TestProjects\TestProject_MSSQL\Migrations";
		var lst = Directory.EnumerateDirectories(path, "*.*", SearchOption.TopDirectoryOnly);
	}
}