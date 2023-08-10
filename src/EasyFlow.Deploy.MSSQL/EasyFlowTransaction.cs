using Microsoft.Data.SqlClient;

namespace EasyFlow.Deploy.MSSQL
{
	internal class EasyFlowTransaction : IEasyFlowTransaction
	{
		public SqlTransaction SqlTransaction { get; private set; }

		public EasyFlowTransaction(SqlTransaction sqlTran)
		{
			SqlTransaction = sqlTran;
		}
	}
}