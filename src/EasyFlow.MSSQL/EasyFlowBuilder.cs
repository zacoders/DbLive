using EasyFlow.Common;

namespace EasyFlow.MSSQL;

public static class EasyFlowBuilderExtensions
{
	public static IEasyFlowBuilder SqlServer(this IEasyFlowBuilder builder)
	{
		builder.Container.InitializeMSSQL();

		return builder;
	}
}
