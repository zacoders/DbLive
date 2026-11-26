namespace DbLive.Project;

internal static class ScriptSourceExtensions
{
	public static async Task<FileData> ReadFileDataAsync(this IScriptSource scriptSource, string relativePath)
	{
		return new FileData
		{
			Content = await scriptSource.ReadAsync(relativePath).ConfigureAwait(false),
			RelativePath = relativePath
		};
	}
}
