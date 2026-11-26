using DbLive.SelfDeployer;
using System.Collections.Concurrent;

namespace DbLive.Tests;

/// <summary>
/// Scripts in memory, by relative path. <see cref="GetFiles"/> returns them in the order they were added.
/// </summary>
internal sealed class InMemoryScriptSource : IInternalScriptSource
{
	private readonly Dictionary<string, string> _files = [];
	private readonly ConcurrentDictionary<string, Exception> _readErrors = new();
	private readonly ConcurrentDictionary<string, int> _readCounts = new();

	public string Location => "memory";

	/// <summary>
	/// The next read of <paramref name="relativePath"/> fails with <paramref name="exception"/>; the reads after it succeed.
	/// </summary>
	public InMemoryScriptSource FailNextRead(string relativePath, Exception exception)
	{
		_readErrors[relativePath] = exception;
		return this;
	}

	/// <summary>
	/// How many times <paramref name="relativePath"/> was read, the failed reads included.
	/// </summary>
	public int ReadCount(string relativePath) => _readCounts.GetValueOrDefault(relativePath);

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

	public Task<string> ReadAsync(string relativePath)
	{
		_ = _readCounts.AddOrUpdate(relativePath, 1, (_, count) => count + 1);

		if (_readErrors.TryRemove(relativePath, out Exception? exception))
		{
			return Task.FromException<string>(exception);
		}

		return _files.TryGetValue(relativePath, out string? content)
			? Task.FromResult(content)
			: throw new FileNotFoundException($"Script '{relativePath}' is not in memory.", relativePath);
	}
}
