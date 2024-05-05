
namespace EasyFlow.Adapter;

public class MultipleResults
{
	public List<SqlResult> Results { get; } = [];


	public MultipleResults(List<List<object>> dapperMultipleResults)
	{
		foreach(var result in dapperMultipleResults)
		{
			SqlResult sqlResult = new(result);
			Results.Add(sqlResult);
		}
	}
}
