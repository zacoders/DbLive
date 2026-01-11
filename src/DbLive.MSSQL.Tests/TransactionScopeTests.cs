using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Transactions;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class TransactionScopeTests(SqlServerIntegrationFixture _fixture) : IAssemblyFixture<SqlServerIntegrationFixture>
{
	[Fact]
	public async Task Test1()
	{
		using TransactionScope transactionScope = TransactionScopeManager.Create();

		SqlConnection cnn1 = new(_fixture.MasterDbConnectionString);
		await cnn1.OpenAsync();
		await cnn1.CloseAsync();

		SqlConnection cnn2 = new(_fixture.MasterDbConnectionString);
		await cnn2.OpenAsync();
		await cnn2.CloseAsync();

		transactionScope.Complete();
	}
}