namespace EasyFlow.Common;

public interface IEasyFlowBuilder
{
	IServiceCollection Container { get; }
	IEasyFlowBuilder CloneBuilder();
}