namespace EasyFlow.Common;

public class EasyFlowBuilder : IEasyFlowBuilder
{
	public readonly IServiceCollection Container = new ServiceCollection();

	public EasyFlowBuilder()
	{
		Container.AddSingleton<IEasyFlowBuilder>(this);
		Container.InitializeEasyFlow();
	}

	public EasyFlowBuilder CloneBuilder()
	{
		EasyFlowBuilder newBuilder = new();

		foreach (var serviceDescriptor in Container)
		{
			newBuilder.Container.Add(serviceDescriptor);
		}

		return newBuilder;
	}
}
