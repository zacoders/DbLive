using DbLive.xunit.SqlTest;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;

namespace DbLive.Tests.Project;

/// <summary>
/// Databases identify scripts by relative path and content hash, so the scripts embedded in a dll must give
/// the same project as the folder they were built from. Otherwise existing databases see changed or new scripts.
/// </summary>
public class EmbeddedScriptsParityTests
{
	[Theory]
	[InlineData("AdventureWorks.Database")]
	[InlineData("Demo.PostgreSQL.Chinook")]
	[InlineData("DemoMSSQL")]
	[InlineData("DemoMSSQLEmpty")]
	public async Task Project_from_dll_equals_project_from_source_folder(string assemblyName)
	{
		Assembly assembly = Assembly.Load(assemblyName);
		string sourceFolder = SqlFactDiscoverer.GetSourceScriptsPath(assembly)!;

		using DbLiveBuilder fromDll = new DbLiveBuilder().SetProject(assembly);
		using DbLiveBuilder fromFolder = new DbLiveBuilder().SetScriptsFolder(sourceFolder);

		List<string> expected = await DescribeAsync(fromFolder);
		List<string> actual = await DescribeAsync(fromDll);

		Assert.Contains(expected, item => item.StartsWith("migration ", StringComparison.Ordinal));
		Assert.Equal(expected, actual);
	}

	private static async Task<List<string>> DescribeAsync(DbLiveBuilder builder)
	{
		IScriptSource scriptSource = GetRegisteredScriptSource(builder);
		IDbLiveProject project = builder.CreateProject();

		DbLiveSettings settings = await new SettingsAccessor(scriptSource).GetProjectSettingsAsync();
		List<string> items = [$"settings {JsonSerializer.Serialize(settings)}"];

		foreach (Migration migration in await project.GetMigrationsAsync())
		{
			items.AddRange(migration.Items.Values
				.OrderBy(item => item.MigrationItemType)
				.Select(item => $"migration {migration.Version} {item.MigrationItemType} {item.Name} {Describe(item.FileData)}"));
		}

		// code groups, tests and folder items are compared in their order, which does not depend on the OS.
		int groupNumber = 0;
		foreach (CodeGroup group in await project.GetCodeGroupsAsync())
		{
			groupNumber++;
			items.AddRange(group.CodeItems
				.Select(item => $"code group {groupNumber} {group.Path} {item.Name} {Describe(item.FileData)}"));
		}

		items.AddRange((await project.GetCodeBreakingItemsAsync())
			.Select(item => $"code breaking {item.Name} {Describe(item.FileData)}"));

		items.AddRange((await project.GetTestsAsync())
			.Select(test => $"test {test.Name} {Describe(test.FileData)} setup {(test.SetupFileData is null ? "none" : Describe(test.SetupFileData))}"));

		foreach (ProjectFolder folder in Enum.GetValues<ProjectFolder>())
		{
			items.AddRange((await project.GetFolderItemsAsync(folder))
				.Select(item => $"{folder} {item.Name} {Describe(item.FileData)}"));
		}

		return items;
	}

	private static string Describe(FileData fileData) => $"{fileData.RelativePath} {fileData.ContentHash}";

	private static IScriptSource GetRegisteredScriptSource(DbLiveBuilder builder)
	{
		IScriptSource? scriptSource = null;
		builder.ConfigureServices(services =>
			scriptSource = (IScriptSource)services.Last(s => s.ServiceType == typeof(IScriptSource)).ImplementationInstance!);

		_ = builder.CreateProject();

		return scriptSource!;
	}
}
