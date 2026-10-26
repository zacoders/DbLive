using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Reflection.Emit;

namespace DbLive.Tests.Builder;

public class DbLiveBuilderExtensionsTests
{
	[Fact]
	public void SetProject_ReadsScriptsNextToApplication_NotFromCurrentDirectory()
	{
		using DbLiveBuilder builder = new DbLiveBuilder()
			.SetProject(CreateAssembly("MyDb", projectDir: "c:/src/MyDb/"));

		IProjectPath projectPath = GetRegisteredProjectPath(builder);

		Assert.Equal(Path.Combine(AppContext.BaseDirectory, "MyDb.Scripts"), projectPath.Path);
		Assert.Equal(Path.Combine("c:/src/MyDb/", "Scripts"), projectPath.VisualStudioProjectPath);
	}

	[Fact]
	public void SetProject_UseVsProjectPath_ReadsScriptsFromSourceProject()
	{
		using DbLiveBuilder builder = new DbLiveBuilder()
			.SetProject(CreateAssembly("MyDb", projectDir: "c:/src/MyDb/"), useVsProjectPath: true);

		IProjectPath projectPath = GetRegisteredProjectPath(builder);

		Assert.Equal(Path.Combine("c:/src/MyDb/", "Scripts"), projectPath.Path);
		Assert.Equal(Path.Combine("c:/src/MyDb/", "Scripts"), projectPath.VisualStudioProjectPath);
	}

	[Fact]
	public void SetProject_WithoutProjectDirMetadata_ThrowsOnlyWhenSourceFolderIsNeeded()
	{
		using DbLiveBuilder builder = new DbLiveBuilder()
			.SetProject(CreateAssembly("MyDb", projectDir: null));

		IDbLiveProject project = builder.CreateProject();

		Exception ex = Assert.Throws<Exception>(project.GetVisualStudioProjectPath);
		Assert.Equal("ProjectDir metadata not found!", ex.Message);
	}

	[Fact]
	public void SetProject_UseVsProjectPath_WithoutProjectDirMetadata_Throws()
	{
		using DbLiveBuilder builder = new();

		Exception ex = Assert.Throws<Exception>(() =>
			builder.SetProject(CreateAssembly("MyDb", projectDir: null), useVsProjectPath: true));
		Assert.Equal("ProjectDir metadata not found!", ex.Message);
	}

	private static IProjectPath GetRegisteredProjectPath(DbLiveBuilder builder)
	{
		IProjectPath? projectPath = null;
		builder.ConfigureServices(services =>
			projectPath = (IProjectPath)services.Last(s => s.ServiceType == typeof(IProjectPath)).ImplementationInstance!);

		_ = builder.CreateProject();

		return projectPath!;
	}

	private static AssemblyBuilder CreateAssembly(string name, string? projectDir)
	{
		List<CustomAttributeBuilder> attributes = [];
		if (projectDir is not null)
		{
			ConstructorInfo ctor = typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!;
			attributes.Add(new CustomAttributeBuilder(ctor, ["ProjectDir", projectDir]));
		}

		return AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run, attributes);
	}
}
