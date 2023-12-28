using EasyFlow.Tests;
using System.Data.SqlClient;
using System.Transactions;
using Xunit.Abstractions;

namespace TestProject1;

public class TranTests(ITestOutputHelper output) : SqlServerIntegrationBaseTest(output)
{
	[Fact]
	public void Test1()
	{		
		using TransactionScope transactionScope = new TransactionScope();

		SqlConnection cnn1 = new(masterDbConnectionString);
		cnn1.Open();
		cnn1.Close();

		SqlConnection cnn2 = new(masterDbConnectionString);
		cnn2.Open();
		cnn2.Close();

		transactionScope.Complete();
	}
}