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

	/// <summary>
	/// Logs to <paramref name="logger"/>, any Serilog logger.
	/// </summary>
	/// <param name="builder">The builder instance to configure.</param>
	/// <param name="logger">The logger. The builder does not dispose it.</param>
	/// <returns>The same builder instance for chaining.</returns>
	public static DbLiveBuilder LogTo(this DbLiveBuilder builder, ILogger logger)
	{
		ArgumentNullException.ThrowIfNull(logger);

		return builder.ConfigureServices(services =>
		{
			_ = services.AddSingleton(logger);
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
		AssemblyScriptSource scriptSource = new(projectAssembly, AssemblyScriptSource.ProjectPrefix);

		// otherwise a forgotten EmbeddedResource item deploys an empty project.
		if (scriptSource.GetFiles().Count == 0)
		{
			throw new ScriptsNotEmbeddedException(projectAssembly.GetName().Name ?? projectAssembly.FullName ?? "");
		}

		return builder.ConfigureServices(services =>
		{
			_ = services.AddSingleton<IScriptSource>(scriptSource);
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

		FolderScriptSource scriptSource = new(fullPath);

		return builder.ConfigureServices(services =>
		{
			_ = services.AddSingleton<IScriptSource>(scriptSource);
		});
	}

	/// <summary>
	/// Reads the scripts from <paramref name="scriptSource"/>, instead of <see cref="SetProject"/> or <see cref="SetScriptsFolder"/>.
	/// </summary>
	/// <param name="builder">The builder instance to configure.</param>
	/// <param name="scriptSource">The scripts. A builder calls <see cref="IScriptSource.ReadAsync"/> once per file.</param>
	/// <returns>The same builder instance for chaining.</returns>
	public static DbLiveBuilder SetScriptSource(this DbLiveBuilder builder, IScriptSource scriptSource)
	{
		ArgumentNullException.ThrowIfNull(scriptSource);

		return builder.ConfigureServices(services =>
		{
			_ = services.AddSingleton(scriptSource);
		});
	}
}

