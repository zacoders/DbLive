namespace EasyFlow.Adapter.MSSQL;

internal class MsSqlTransaction : IEasyFlowTransaction
{
	public SqlTransaction SqlTransaction { get; private set; }

	public MsSqlTransaction(SqlTransaction sqlTran)
	{
		SqlTransaction = sqlTran;
	}

	public void Commit()
	{
		SqlTransaction.Commit();
	}
}