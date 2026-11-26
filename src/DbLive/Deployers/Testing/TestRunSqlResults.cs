namespace DbLive.Deployers.Testing;

internal class TestRunSqlResults
{
	public AssertInfo AssertInfo { get; set; } = new AssertInfo { AssertType = AssertType.None };

	public List<SqlResult> ActualResults { get; set; } = [];

	public List<SqlResult> ExpectedResults { get; set; } = [];
}
