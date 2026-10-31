namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class ScriptsNotEmbeddedException(string assemblyName)
	: Exception($"""
		Assembly '{assemblyName}' has no embedded DbLive scripts. Embed the Scripts folder in the .csproj of the SQL project:

		<ItemGroup>
		  <EmbeddedResource Include="Scripts\**\*" LogicalName="DbLive/%(RecursiveDir)%(Filename)%(Extension)" WithCulture="false" />
		</ItemGroup>
		""")
{
	public string AssemblyName { get; private set; } = assemblyName;
}
