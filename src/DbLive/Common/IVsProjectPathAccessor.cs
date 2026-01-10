namespace DbLive.Common;

public interface IVsProjectPathAccessor
{
	Task<string> GetVisualStudioProjectPathAsync();
}
