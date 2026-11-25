using DbLive.Adapter;
using DbLive.Testing;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.PostgreSQL.Tests;

/// <summary>
/// 0096-175: Npgsql splits a script into statements at each ';' outside quotes, comments and parentheses, also inside the SQL-standard
/// body <c>begin atomic ... end</c> of PostgreSQL 14. The cut statement fails with 42601 'syntax error at end of input' on every
/// deployment, and DbLive adds <see cref="PostgreSqlDA.BeginAtomicHint"/> to the error of a script with the words.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class BeginAtomicBodyTests(PostgreSqlFixture fixture, ITestOutputHelper output)
	: PostgreSqlDbTests(fixture, output)
{
	private const string BeginAtomicFunction = """
		create or replace function add_numbers(a int, b int) returns int
		language sql
		begin atomic
			select a + b;
		end;
		""";

	[Fact]
	public async Task Code_file_with_a_begin_atomic_body_fails_with_the_hint_and_deploys_with_a_return_body()
	{
		using TempProject project = new TempProject().AddFile("Code/public/add_numbers.sql", BeginAtomicFunction);

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		string message = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions)).Message.ReplaceLineEndings();
		Assert.Contains("SqlState: 42601", message);
		Assert.Contains("syntax error at end of input", message);
		Assert.EndsWith(Environment.NewLine + PostgreSqlDA.BeginAtomicHint, message);

		// the form that the hint names deploys.
		_ = project.AddFile("Code/public/add_numbers.sql", """
			create or replace function add_numbers(a int, b int) returns int
			language sql
			return a + b;
			""");
		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(3, await builder.GetRequiredService<IDbLiveDA>().ScalarAsync("select add_numbers(1, 2)"));
	}

	// a failed SQL test shows the message of the error, not the SQL error.
	[Fact]
	public async Task Sql_test_with_a_begin_atomic_body_fails_with_the_hint()
	{
		using TempProject project = new TempProject()
			.AddFile("Tests/add_numbers.sql", BeginAtomicFunction + Environment.NewLine + "select add_numbers(1, 2) as x;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/add_numbers.sql");

		Assert.False(result.IsSuccess);
		Assert.StartsWith("42601: syntax error at end of input", result.ErrorMessage);
		Assert.EndsWith(Environment.NewLine + PostgreSqlDA.BeginAtomicHint, result.ErrorMessage);
	}
}
