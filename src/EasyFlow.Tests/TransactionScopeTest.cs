using System.Transactions;

namespace EasyFlow.Tests;

public class TransactionScopeTest(ITestOutputHelper output) : SqlServerIntegrationBaseTest(output)
{
	[Fact]
	public void Test1()
	{
		using TransactionScope transactionScope = new();

		SqlConnection cnn1 = new(masterDbConnectionString);
		cnn1.Open();
		cnn1.Close();

		SqlConnection cnn2 = new(masterDbConnectionString);
		cnn2.Open();
		cnn2.Close();

		transactionScope.Complete();
	}
}