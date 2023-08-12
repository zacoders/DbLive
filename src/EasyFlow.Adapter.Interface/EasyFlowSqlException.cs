namespace EasyFlow.Adapter.Interface;

public class EasyFlowSqlException : Exception
{
	public EasyFlowSqlException(string errorMessage, Exception innerException)
		: base($"Execution failed. {errorMessage}", innerException)
	{
	}
}
