namespace DbLive.Tests.Deployers.Code;

public class CodeItemDeployerTests
{
	[Fact]
	public async Task Deploy_CodeItem_Success()
	{
		// Arrange
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		// Act
		CodeItemDeployResult res = await deployer.DeployAsync(codeItem);

		// Assert
		Assert.True(res.IsSuccess);
		await mockSet.DbLiveDA.Received().ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		await mockSet.DbLiveDA.Received().SaveCodeItemAsync(Arg.Is<CodeItemDto>(i => i.Status == CodeItemStatus.Applied));
	}

	[Fact]
	public async Task DeployCodeItem_Execute_Throws_Exception()
	{
		// Arrange
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		mockSet.DbLiveDA
			.When(x => x.ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>()))
			.Do(x => throw new Exception("some exception"));

		// Act
		CodeItemDeployResult res = await deployer.DeployAsync(codeItem);

		// Assert
		Assert.False(res.IsSuccess);
		Assert.True(res.CanRetry);
		await mockSet.DbLiveDA.Received().ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		await mockSet.DbLiveDA.Received().SaveCodeItemAsync(Arg.Is<CodeItemDto>(i => i.Status == CodeItemStatus.Error && i.ErrorMessage == "some exception"));
	}

	[Fact]
	public async Task Failed_code_item_saves_the_sql_error_without_stack_traces()
	{
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		DbLiveSqlException scriptError = new(
			"Invalid object name 'dbo.Missing'.",
			"SQL Server error:\nMsg 208, Level 16, State 1, Line 3\nInvalid object name 'dbo.Missing'.\n",
			new InvalidOperationException("inner")
		);
		mockSet.DbLiveDA
			.ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan?>())
			.Returns(Task.FromException(scriptError));

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		CodeItemDeployResult res = await deployer.DeployAsync(codeItem);

		Assert.False(res.IsSuccess);
		Assert.Same(scriptError, res.Exception!.InnerException);
		await mockSet.DbLiveDA.Received().SaveCodeItemAsync(Arg.Is<CodeItemDto>(i =>
			i.Status == CodeItemStatus.Error
			&& i.ErrorMessage == "SQL Server error:\nMsg 208, Level 16, State 1, Line 3\nInvalid object name 'dbo.Missing'."
		));
	}

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public async Task Failed_code_item_in_a_transaction_saves_no_error_row(bool errorAbortsTransaction, bool canRetry)
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.ErrorAbortsTransaction.Returns(errorAbortsTransaction);

		CodeItem codeItem = GetCodeItem(relativePath: "Code/dbo/GetUser.sql");

		DbLiveSqlException scriptError = new("Incorrect syntax near 'selec'.");
		mockSet.DbLiveDA
			.ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan?>())
			.Returns(Task.FromException(scriptError));

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		CodeItemDeployResult res;
		using (TransactionScope _ = TransactionScopeManager.Create())
		{
			res = await deployer.DeployAsync(codeItem);
		}

		Assert.False(res.IsSuccess);
		Assert.Equal(canRetry, res.CanRetry);
		CodeDeploymentException ex = Assert.IsType<CodeDeploymentException>(res.Exception);
		Assert.Equal("Deploy code file error. File path: Code/dbo/GetUser.sql", ex.Message);
		Assert.Same(scriptError, ex.InnerException);
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SaveCodeItemAsync(default!);
	}

	[Fact]
	public async Task Failed_code_item_fails_with_the_error_of_its_error_row_when_the_row_cannot_be_saved()
	{
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		mockSet.DbLiveDA
			.ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan?>())
			.Returns(Task.FromException(new DbLiveSqlException("Incorrect syntax near 'selec'.")));

		DbLiveSqlException saveError = new("Invalid column name 'error'.");
		mockSet.DbLiveDA
			.SaveCodeItemAsync(Arg.Any<CodeItemDto>())
			.Returns(Task.FromException(saveError));

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		DbLiveSqlException ex = await Assert.ThrowsAsync<DbLiveSqlException>(() => deployer.DeployAsync(codeItem));

		Assert.Same(saveError, ex);
	}

	[Fact]
	public async Task DeployCodeItem_Keeps_The_Real_Casing_Of_The_Path()
	{
		// Arrange
		MockSet mockSet = new();

		// the database stores the path in lower case, the deployer passes and logs the path of the file.
		CodeItem codeItem = GetCodeItem(relativePath: "Code/dbo/GetUser.sql");

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		// Act
		CodeItemDeployResult res = await deployer.DeployAsync(codeItem);

		// Assert
		Assert.True(res.IsSuccess);
		await mockSet.DbLiveDA.Received().SaveCodeItemAsync(Arg.Is<CodeItemDto>(i => i.RelativePath == "Code/dbo/GetUser.sql"));
		mockSet.Logger.Received().Information("Deploying code file: {filePath}", "Code/dbo/GetUser.sql");
	}

	private static CodeItem GetCodeItem(string content = "select * from table", string relativePath = "/path-to/some-code-item.sql")
	{
		CodeItem codeItem = new()
		{
			Name = "some-code-item",
			FileData = new FileData
			{
				Content = content,
				RelativePath = relativePath
			}
		};

		return codeItem;
	}
}
