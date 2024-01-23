using Microsoft.Data.SqlClient;
using System.Transactions;
using Xunit.Extensions.AssemblyFixture;

namespace EasyFlow.MSSQL.Tests;

public class TransactionScopeTest(SqlServerIntegrationFixture _fixture) : IAssemblyFixture<SqlServerIntegrationFixture>
{
	[Fact]
	public void Test1()
	{
		using TransactionScope transactionScope = new();

		SqlConnection cnn1 = new(_fixture.MasterDbConnectionString);
		cnn1.Open();
		cnn1.Close();

		SqlConnection cnn2 = new(_fixture.MasterDbConnectionString);
		cnn2.Open();
		cnn2.Close();

		transactionScope.Complete();
	}
}