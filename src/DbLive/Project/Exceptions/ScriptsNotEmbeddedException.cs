namespace DbLive;

/// <summary>
/// The assembly passed to <see cref="DbLiveBuilderExtensions.SetProject"/> has no embedded DbLive scripts: the <c>.csproj</c> of the
/// SQL project does not name the scripts folder in <c>DbLiveScriptsFolder</c>, the project does not reference a DbLive package,
/// or it is another assembly, such as the deployer's.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ScriptsNotEmbeddedException : Exception
{
	internal ScriptsNotEmbeddedException(string assemblyName)
		: base($"""
			Assembly '{assemblyName}' has no embedded DbLive scripts. Pass the assembly of the SQL project, the project that contains the scripts.
			The DbLive package embeds the folder that the .csproj of the SQL project names. Check that:
			- the .csproj names the folder of the scripts, relative to the project: <DbLiveScriptsFolder>Scripts</DbLiveScriptsFolder>;
			- the SQL project references a DbLive package, directly or through DbLive.MSSQL, DbLive.PostgreSQL or DbLive.xunit; a project with only scripts needs <PackageReference Include="DbLive" />;
			- the folder exists and has the scripts.
			""")
	{
		AssemblyName = assemblyName;
	}

	/// <summary>
	/// The name of the assembly.
	/// </summary>
	public string AssemblyName { get; private set; }
}
