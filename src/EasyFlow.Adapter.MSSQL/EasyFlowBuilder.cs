using EasyFlow.Common;

namespace EasyFlow.Adapter.MSSQL;

public static class EasyFlowBuilderExtentions
{
	public static EasyFlowBuilder SqlServer(this EasyFlowBuilder builder)
	{
		builder.Container.InitializeMSSQL();

		return builder;
	}
}
