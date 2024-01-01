using Microsoft.Extensions.DependencyInjection;

namespace EasyFlow.Common;

public class EasyFlowBuilder
{
	public readonly IServiceCollection Container = new ServiceCollection();

	public EasyFlowBuilder()
	{
		Container.AddSingleton(this);
	}

	public EasyFlowBuilder ClonBuilder()
	{
		EasyFlowBuilder newBuilder = new();

		foreach (var serviceDescriptor in Container)
		{
			newBuilder.Container.Add(serviceDescriptor);
		}

		return newBuilder;
	}
}
