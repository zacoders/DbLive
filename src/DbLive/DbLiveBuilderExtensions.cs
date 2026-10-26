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
	/// Configures the base path for SQL scripts using the specified project assembly.
	/// </summary>
	/// <param name="builder">The builder instance to configure.</param>
	/// <param name="projectAssembly">The assembly of the SQL Project used to resolve paths. Cannot be null.</param>
	/// <param name="useVsProjectPath">
	/// Set to <see langword="true"/> to use the Visual Studio source project directory (best for unit testing);
	/// otherwise, <see langword="false"/> to use the <c>&lt;AssemblyName&gt;.Scripts</c> folder next to the application
	/// (<see cref="AppContext.BaseDirectory"/>), whatever the current working directory is.
	/// </param>
	/// <returns>The same builder instance for chaining.</returns>
	public static DbLiveBuilder SetProject(this DbLiveBuilder builder, Assembly projectAssembly, bool useVsProjectPath = false)
	{
		// ProjectDir metadata is read only when the source folder is needed: useVsProjectPath or [SqlFact] test discovery.
		string GetVsProjectPath() => Path.Combine(GetVisualStudioProjectPath(projectAssembly), "Scripts");

		string scriptsPath = useVsProjectPath
			? GetVsProjectPath()
			: Path.Combine(AppContext.BaseDirectory, projectAssembly.GetName().Name + ".Scripts");

		return builder.ConfigureServices(services =>
		{
			ProjectPath projectPath = new(scriptsPath, GetVsProjectPath);
			_ = services.AddSingleton<IProjectPath>(projectPath);
		});
	}

	private static string GetVisualStudioProjectPath(Assembly projectAssembly)
	{
		var attr = projectAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
						   .FirstOrDefault(a => a.Key == "ProjectDir");

		return attr?.Value ?? throw new Exception("ProjectDir metadata not found!");
	}
}

