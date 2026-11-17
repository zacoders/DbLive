using System.Reflection;
using System.Text;

namespace DbLive.Project;

/// <summary>
/// Scripts embedded in an assembly as resources named <c>&lt;prefix&gt;&lt;relative path&gt;</c>.
/// </summary>
internal class AssemblyScriptSource : IScriptSource
{
	/// <summary>
	/// Prefix of the resources of a SQL project, see <see cref="DbLiveBuilderExtensions.SetProject"/>.
	/// </summary>
	internal const string ProjectPrefix = "DbLive/";

	private readonly Func<string, Stream?> _openResource;

	// relative path -> resource name
	private readonly Dictionary<string, string> _resourceNames;

	public AssemblyScriptSource(Assembly assembly, string prefix)
		: this($"{assembly.GetName().Name}.dll", assembly.GetManifestResourceNames(), assembly.GetManifestResourceStream, prefix)
	{
	}

	/// <param name="location">See <see cref="Location"/>.</param>
	/// <param name="resourceNames">All resource names; names without the <paramref name="prefix"/> are ignored.</param>
	/// <param name="openResource">Opens a resource by its name.</param>
	/// <param name="prefix">Prefix of the resource names of the scripts.</param>
	internal AssemblyScriptSource(string location, IEnumerable<string> resourceNames, Func<string, Stream?> openResource, string prefix)
	{
		Location = location;
		_openResource = openResource;
		_resourceNames = resourceNames
			.Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
			// %(RecursiveDir) uses '\' when the project is built on Windows.
			.ToDictionary(name => name[prefix.Length..].Replace('\\', '/'), name => name, StringComparer.Ordinal);
	}

	public string Location { get; }

	public IReadOnlyList<string> GetFiles() => [.. _resourceNames.Keys];

	/// <summary>
	/// Decodes as <see cref="ScriptEncoding"/> says, like <see cref="FolderScriptSource"/>, so content hashes do not depend on
	/// where the scripts are read from.
	/// </summary>
	/// <exception cref="FileNotUtf8Exception">The file has bytes that are not UTF-8.</exception>
	public async Task<string> ReadAsync(string relativePath)
	{
		if (!_resourceNames.TryGetValue(relativePath, out string? resourceName))
		{
			throw new FileNotFoundException($"Script '{relativePath}' is not embedded in {Location}.", relativePath);
		}

		Stream stream = _openResource(resourceName)
			?? throw new FileNotFoundException($"Resource '{resourceName}' cannot be read from {Location}.", relativePath);

		using StreamReader reader = new(stream, ScriptEncoding.StrictUtf8, detectEncodingFromByteOrderMarks: true);
		try
		{
			return await reader.ReadToEndAsync().ConfigureAwait(false);
		}
		catch (DecoderFallbackException ex)
		{
			throw new FileNotUtf8Exception(relativePath, Location, ex);
		}
	}
}
