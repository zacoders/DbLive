using EasyFlow.Project.Exceptions;

namespace EasyFlow.Tests.Project;

public class MigrationItemTests
{
	[Fact]
	public void GetMigrationType()
	{
		MockSet mockSet = new();

		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		var sqlProject = new EasyFlowProject(mockSet.ProjectPathAccessor, mockSet.FileSystem, mockSet.DefaultSettingsAccessor);

		//var settings = sqlProject.GetSettings();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), "*.sql", true)
			.Returns(
			[
				@"C:\DB\Migrations\003.test3\migration.sql",
				@"C:\DB\Migrations\003.test3\undo.sql",
				@"C:\DB\Migrations\003.test3\breaking.sql"
			]);

		var migrationItems = sqlProject.GetMigrationItems("");

		Assert.Equal(3, migrationItems.Count);
	}

	[Fact]
	public void GetMigrationType_SimpleApproach()
	{
		MockSet mockSet = new();

		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		var sqlProject = new EasyFlowProject(mockSet.ProjectPathAccessor, mockSet.FileSystem, mockSet.DefaultSettingsAccessor);

		//var settings = sqlProject.GetSettings();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), "*.sql", true)
			.Returns([
				@"C:\DB\Migrations\002.test\m.sql",
				@"C:\DB\Migrations\002.test\u.sql",
				@"C:\DB\Migrations\002.test\b.sql"
			]);

		var migrationItems = sqlProject.GetMigrationItems("");

		Assert.Equal(3, migrationItems.Count);
	}


	[Fact]
	public void GetMigrationType_MultipleItemsWithTheSameType()
	{
		MockSet mockSet = new();

		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		var sqlProject = new EasyFlowProject(mockSet.ProjectPathAccessor, mockSet.FileSystem, mockSet.DefaultSettingsAccessor);

		//var settings = sqlProject.GetSettings();

		mockSet.FileSystem.ReadFileData(Arg.Any<string>(), Arg.Any<string>())
			.ReturnsForAnyArgs(call =>
			new FileData
			{
				FilePath = call.Args()[0].ToString()!,
				Content = "",
				RelativePath = ""
			});

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), "*.sql", true)
			.Returns([
				@"C:\DB\Migrations\002.test\m.first.sql",
				@"C:\DB\Migrations\002.test\m.second.sql",
				@"C:\DB\Migrations\002.test\undo.one.sql",
				@"C:\DB\Migrations\002.test\undo.2.sql",
				@"C:\DB\Migrations\002.test\undo.3.sql",
				@"C:\DB\Migrations\002.test\b.01.sql",
				@"C:\DB\Migrations\002.test\b.02.sql",
				@"C:\DB\Migrations\002.test\b.03.sql"
			]);

		var migrationItems = sqlProject.GetMigrationItems("");

		Assert.Equal(8, migrationItems.Count);

		// checking order, they will be deploed in this order.
		Assert.Equal(@"C:\DB\Migrations\002.test\b.01.sql", migrationItems[0].FileData.FilePath);
		Assert.Equal(@"C:\DB\Migrations\002.test\b.02.sql", migrationItems[1].FileData.FilePath);
		Assert.Equal(@"C:\DB\Migrations\002.test\b.03.sql", migrationItems[2].FileData.FilePath);
		Assert.Equal(@"C:\DB\Migrations\002.test\undo.one.sql", migrationItems[7].FileData.FilePath);
	}


	[Fact]
	public void GetMigrationItems_UnknownItemType()
	{
		MockSet mockSet = new();

		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		var sqlProject = new EasyFlowProject(mockSet.ProjectPathAccessor, mockSet.FileSystem, mockSet.DefaultSettingsAccessor);

		//var settings = sqlProject.GetSettings();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), "*.sql", true)
			.Returns(
			[
				@"C:\DB\Migrations\003.test3\some-unknown-type.sql",
				@"C:\DB\Migrations\003.test3\undo.sql",
				@"C:\DB\Migrations\003.test3\breaking.sql"
			]);
		Assert.Throws<UnknowMigrationItemTypeException>(() => sqlProject.GetMigrationItems(""));
	}

	[Fact]
	public void GetMigrationType_EmptySettingsTest()
	{
		MockSet mockSet = new();

		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		mockSet.FileSystem.FileExists(Arg.Any<string>()).Returns(true);
		mockSet.FileSystem.FileReadAllText(Arg.Any<string>()).Returns("");// empty string

		var sqlProject = new EasyFlowProject(mockSet.ProjectPathAccessor, mockSet.FileSystem, mockSet.DefaultSettingsAccessor);

		sqlProject.GetMigrations();
	}
}