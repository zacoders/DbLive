//namespace EasyFlow.MSSQL;

//public static class GridReaderEx
//{
//	public static bool HasRows(SqlMapper.GridReader reader)//https://github.com/StackExchange/Dapper/issues/327
//	{
//		SqlDataReader internalReader = (SqlDataReader)typeof(SqlMapper.GridReader).
//		GetField
//		("reader",
//			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
//			.GetValue(reader);
//		return internalReader.HasRows;
//	}
//}