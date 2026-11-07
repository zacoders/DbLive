namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class ScriptsNotEmbeddedException(string assemblyName)
	: Exception($"""
		Assembly '{assemblyName}' has no embedded DbLive scripts. Pass the assembly of the SQL project, the project that contains the scripts.
		The DbLive package embeds the folder that the .csproj of the SQL project names. Check that:
		- the .csproj names the folder of the scripts, relative to the project: <DbLiveScriptsFolder>Scripts</DbLiveScriptsFolder>;
		- the SQL project references a DbLive package, directly or through DbLive.MSSQL, DbLive.PostgreSQL or DbLive.xunit; a project with only scripts needs <PackageReference Include="DbLive" />;
		- the folder exists and has the scripts.
		""")
{
	public string AssemblyName { get; private set; } = assemblyName;
}
