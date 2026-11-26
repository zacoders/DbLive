using Serilog.Core;
using System.Reflection;

namespace DbLive;

/// <summary>
/// Configures a <see cref="DbLiveBuilder"/>: the connection, the project, the logger. The provider comes from its package:
/// <c>.SqlServer()</c> in <c>DbLive.MSSQL</c>, <c>.PostgreSQL()</c> in <c>DbLive.PostgreSQL</c>.
/// </summary>
[ExcludeFromCodeCoverage]
public static class DbLiveBuilderExtensions
{
	/// <summary>
	/// Logs to the console, with a Serilog console logger.
	/// </summary>
	/// <param name="builder">The builder instance to configure.</param>
	/// <returns>The same builder instance for chaining.</returns>
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
	/// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
	public static DbLiveBuilder LogTo(this DbLiveBuilder builder, ILogger logger)
	{
		ArgumentNullException.ThrowIfNull(logger);

		return builder.ConfigureServices(services =>
		{
			_ = services.AddSingleton(logger);
		});
	}

	/// <summary>
	/// The database to deploy to and to run the SQL tests against.
	/// </summary>
	/// <param name="builder">The builder instance to configure.</param>
	/// <param name="sqlDbConnectionString">The connection string of the database. A deployment creates the database only with
	/// <see cref="DeployParameters.CreateDbIfNotExists"/>.</param>
	/// <returns>The same builder instance for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="sqlDbConnectionString"/> is null.</exception>
	public static DbLiveBuilder SetDbConnection(this DbLiveBuilder builder, string sqlDbConnectionString)
	{
		ArgumentNullException.ThrowIfNull(sqlDbConnectionString);

		return builder.ConfigureServices(services =>
		{
			_ = services.AddSingleton<IDbLiveDbConnection>(new DbLiveDbConnection(sqlDbConnectionString));
		});
	}

	/// <summary>
	/// Reads the scripts embedded in the SQL project assembly: resources named <c>DbLive/&lt;path relative to Scripts&gt;</c>.
	/// </summary>
	/// <param name="builder">The builder instance to configure.</param>
	/// <param name="projectAssembly">The assembly of the SQL Project.</param>
	/// <returns>The same builder instance for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="projectAssembly"/> is null.</exception>
	/// <exception cref="ScriptsNotEmbeddedException">The assembly has no embedded scripts.</exception>
	public static DbLiveBuilder SetProject(this DbLiveBuilder builder, Assembly projectAssembly)
	{
		ArgumentNullException.ThrowIfNull(projectAssembly);

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
	/// <exception cref="ArgumentNullException"><paramref name="scriptsPath"/> is null.</exception>
	/// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
	public static DbLiveBuilder SetScriptsFolder(this DbLiveBuilder builder, string scriptsPath)
	{
		ArgumentNullException.ThrowIfNull(scriptsPath);

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
}

