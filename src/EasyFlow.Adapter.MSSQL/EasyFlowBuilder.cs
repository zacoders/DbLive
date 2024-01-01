using EasyFlow.Adapter.MSSQL;
using EasyFlow.Common;

namespace EasyFlow;

public static class EasyFlowBuilderExtentions
{
	public static EasyFlowBuilder SqlServer(this EasyFlowBuilder builder)
	{
		builder.Container.InitializeMSSQL();

		return builder;
	}
}
