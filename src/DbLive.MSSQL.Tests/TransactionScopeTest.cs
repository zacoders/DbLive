using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Transactions;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
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