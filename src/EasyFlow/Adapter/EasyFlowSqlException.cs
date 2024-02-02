namespace EasyFlow.Adapter;

[ExcludeFromCodeCoverage]
public class EasyFlowSqlException : Exception
{
	public EasyFlowSqlException(string errorMessage, Exception innerException)
		: base(errorMessage, innerException)
	{
	}
	public EasyFlowSqlException(string errorMessage)
		: base(errorMessage)
	{
	}
}
