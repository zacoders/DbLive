namespace EasyFlow.Adapter;

public static class Bootstrapper
{
	public static void InitializeEasyFlowAdapter(this IServiceCollection container)
	{
		container.AddSingleton<IAdapterFactory, AdapterFactory>();
	}
}
