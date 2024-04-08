namespace EasyFlow.Common;

public class EasyFlowBuilder : IEasyFlowBuilder
{
	public IServiceCollection Container { get; }

	public EasyFlowBuilder()
	{
		Container = new ServiceCollection();
		Container.AddSingleton<IEasyFlowBuilder>(this);
		Container.InitializeEasyFlow();
	}

	public IEasyFlowBuilder CloneBuilder()
	{
		EasyFlowBuilder newBuilder = new();

		foreach (var serviceDescriptor in Container)
		{
			newBuilder.Container.Add(serviceDescriptor);
		}

		return newBuilder;
	}
}
