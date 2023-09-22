namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowSqlConnection : IDisposable
{
	/// <exception cref="EasyFlowSqlException"/>
	void ExecuteNonQuery(string sqlStatementt);

	/// <exception cref="EasyFlowSqlException"/>
	void Close();
}
