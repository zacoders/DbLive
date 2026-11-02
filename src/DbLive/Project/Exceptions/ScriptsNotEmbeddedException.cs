namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class ScriptsNotEmbeddedException(string assemblyName)
	: Exception($"""
		Assembly '{assemblyName}' has no embedded DbLive scripts. Pass the assembly of the SQL project, the project that contains the scripts.
		The DbLive package embeds the Scripts folder of every project that references it, directly or through DbLive.MSSQL, DbLive.PostgreSQL or DbLive.xunit. Check that:
		- the SQL project references a DbLive package; a project with only scripts needs <PackageReference Include="DbLive" />;
		- the scripts are in its Scripts folder, or its .csproj names their folder: <DbLiveScriptsFolder>Db</DbLiveScriptsFolder>;
		- its .csproj does not set <DbLiveEmbedScripts>false</DbLiveEmbedScripts>.
		""")
{
	public string AssemblyName { get; private set; } = assemblyName;
}
