
namespace EasyFlow.Adapter;

public class MultipleResults
{
	public List<SqlResult> Results { get; } = [];


	public static MultipleResults Empty => new MultipleResults([]);


	public MultipleResults(List<List<object>> dapperMultipleResults)
	{
		foreach(List<object> result in dapperMultipleResults)
		{
			SqlResult sqlResult = new(result);
			Results.Add(sqlResult);
		}
	}
}
