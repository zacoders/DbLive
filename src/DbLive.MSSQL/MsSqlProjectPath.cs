using DbLive.Adapter;

namespace DbLive.MSSQL;

internal class MsSqlProjectPath : IInternalProjectPath
{
	public string Path
	{
		get => System.IO.Path.Combine(
					AppContext.BaseDirectory,
					GetType().Assembly.GetName().Name ?? throw new Exception("Unknown assembly name.")
			   );
	}
}