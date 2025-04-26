using EasyFlow.Deployers.Testing;

namespace EasyFlow.Adapter;

public class TestRunSqlResults
{
	public AssertType AssertType { get; set; }

	public List<SqlResult> ActualResults { get; set; } = [];
	
	public List<SqlResult> ExpectedResults { get; set; } = [];
}
