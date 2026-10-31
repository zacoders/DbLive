using Serilog.Core;
using System.Reflection;

namespace DbLive;

[ExcludeFromCodeCoverage]
public static class DbLiveBuilderExtensions
{
	public static DbLiveBuilder LogToConsole(this DbLiveBuilder builder)
	{
		return builder.ConfigureServices(services =>
		{
			Logger logger = new LoggerConfiguration()
				.WriteTo.Console()
				.CreateLogger();

			_ = services.AddSingleton<ILogger>(logger);
		});
	}

	public static DbLiveBuilder SetDbConnection(this DbLiveBuilder builder, string sqlDbConnectionString)
	{
		return builder.ConfigureServices(services =>
		{
			_ = services.AddSingleton<IDbLiveDbConnection>(new DbLiveDbConnection(sqlDbConnectionString));
		});
	}

	/// <summary>
	/// Reads the scripts embedded in the SQL project assembly: resources named <c>DbLive/&lt;path relative to Scripts&gt;</c>.
	/// </summary>
	/// <param name="builder">The builder instance to configure.</param>
	/// <param name="projectAssembly">The assembly of the SQL Project. Cannot be null.</param>
	/// <returns>The same builder instance for chaining.</returns>
	/// <exception cref="ScriptsNotEmbeddedException">The assembly has no embedded scripts.</exception>
	public static DbLiveBuilder SetProject(this DbLiveBuilder builder, Assembly projectAssembly)
	{
		EmbeddedFileSystem fileSystem = EmbeddedFileSystem.FromAssembly(projectAssembly);

		// otherwise a forgotten EmbeddedResource item deploys an empty project.
		if (fileSystem.IsEmpty)
		{
			throw new ScriptsNotEmbeddedException(projectAssembly.GetName().Name ?? projectAssembly.FullName ?? "");
		}

		ProjectPath projectPath = new(fileSystem.Root, fileSystem, GetSourceScriptsPath(projectAssembly));

		return builder.ConfigureServices(services =>
		{
			_ = services.AddSingleton<IProjectPath>(projectPath);
		});
	}

	/// <summary>
	/// Reads the scripts from a folder.
	/// </summary>
	/// <param name="builder">The builder instance to configure.</param>
	/// <param name="scriptsPath">The scripts folder. A relative path is resolved against the current directory.</param>
	/// <returns>The same builder instance for chaining.</returns>
	/// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
	public static DbLiveBuilder SetScriptsFolder(this DbLiveBuilder builder, string scriptsPath)
	{
		string fullPath = Path.GetFullPath(scriptsPath);

		if (!Directory.Exists(fullPath))
		{
			throw new DirectoryNotFoundException($"Scripts folder '{fullPath}' not found.");
		}

		ProjectPath projectPath = new(fullPath, new FileSystem(), fullPath);

		return builder.ConfigureServices(services =>
		{
			_ = services.AddSingleton<IProjectPath>(projectPath);
		});
	}

	/// <summary>
	/// The Scripts folder in the source project, from the <c>ProjectDir</c> assembly metadata. Null without the metadata.
	/// </summary>
	internal static string? GetSourceScriptsPath(Assembly projectAssembly)
	{
		string? projectDir = projectAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
			.FirstOrDefault(a => a.Key == "ProjectDir")?.Value;

		return string.IsNullOrEmpty(projectDir) ? null : Path.Combine(projectDir, "Scripts");
	}
}

