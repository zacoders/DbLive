using DbLive.SelfDeployer;

namespace DbLive.Tests.Common;

/// <summary>
/// Scripts in memory, by relative path. <see cref="GetFiles"/> returns them in the order they were added.
/// </summary>
public sealed class InMemoryScriptSource : IInternalScriptSource
{
	private readonly Dictionary<string, string> _files = [];

	public string Location => "memory";

	public InMemoryScriptSource AddFile(string relativePath, string content = "")
	{
		_files[relativePath] = content;
		return this;
	}

	public InMemoryScriptSource AddFiles(params string[] relativePaths)
	{
		foreach (string relativePath in relativePaths)
		{
			_ = AddFile(relativePath);
		}
		return this;
	}

	public IReadOnlyList<string> GetFiles() => [.. _files.Keys];

	public Task<string> ReadAsync(string relativePath) =>
		_files.TryGetValue(relativePath, out string? content)
			? Task.FromResult(content)
			: throw new FileNotFoundException($"Script '{relativePath}' is not in memory.", relativePath);
}
