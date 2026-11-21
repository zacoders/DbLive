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
		await mockSet.DbLiveDA.Received().ExecuteCodeItemAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
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
			.When(x => x.ExecuteCodeItemAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>()))
			.Do(x => throw new Exception("some exception"));

		// Act
		CodeItemDeployResult res = await deployer.DeployAsync(codeItem);

		// Assert
		Assert.False(res.IsSuccess);
		Assert.True(res.CanRetry);
		await mockSet.DbLiveDA.Received().ExecuteCodeItemAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
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
			.ExecuteCodeItemAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
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
	[InlineData(true, "Deploy code file error. File path: Code/dbo/GetUser.sql")]
	[InlineData(false, "Deploy code file error. File path: Code/dbo/GetUser.sql. The error ended the deployment transaction, so the file is not retried.")]
	public async Task Failed_code_item_in_a_transaction_saves_no_error_row(bool transactionUsable, string message)
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.IsTransactionUsableAsync().Returns(transactionUsable);

		CodeItem codeItem = GetCodeItem(relativePath: "Code/dbo/GetUser.sql");

		DbLiveSqlException scriptError = new("Incorrect syntax near 'selec'.");
		mockSet.DbLiveDA
			.ExecuteCodeItemAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
			.Returns(Task.FromException(scriptError));

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		CodeItemDeployResult res;
		using (TransactionScope _ = TransactionScopeManager.Create())
		{
			res = await deployer.DeployAsync(codeItem);
		}

		Assert.False(res.IsSuccess);
		Assert.Equal(transactionUsable, res.CanRetry);
		CodeDeploymentException ex = Assert.IsType<CodeDeploymentException>(res.Exception);
		Assert.Equal(message, ex.Message);
		Assert.Same(scriptError, ex.InnerException);
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SaveCodeItemAsync(default!);
	}

	[Fact]
	public async Task Failed_code_item_keeps_its_error_when_the_transaction_check_throws()
	{
		// the error broke the connection or the transaction timed out: the connection opened for the check cannot enlist.
		MockSet mockSet = new();
		TransactionException transactionError = new("The operation is not valid for the state of the transaction.");
		mockSet.DbLiveDA.IsTransactionUsableAsync().Returns(Task.FromException<bool>(transactionError));

		DbLiveSqlException scriptError = new("A severe error occurred on the current command.");
		mockSet.DbLiveDA
			.ExecuteCodeItemAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
			.Returns(Task.FromException(scriptError));

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		CodeItemDeployResult res;
		using (TransactionScope _ = TransactionScopeManager.Create())
		{
			res = await deployer.DeployAsync(GetCodeItem(relativePath: "Code/dbo/GetUser.sql"));
		}

		Assert.False(res.IsSuccess);
		Assert.False(res.CanRetry);
		CodeDeploymentException ex = Assert.IsType<CodeDeploymentException>(res.Exception);
		Assert.Equal("Deploy code file error. File path: Code/dbo/GetUser.sql. The error ended the deployment transaction, so the file is not retried.", ex.Message);
		Assert.Same(scriptError, ex.InnerException);
		mockSet.Logger.Received().Warning(transactionError, "The deployment transaction cannot be used after the error of {FilePath}.", "Code/dbo/GetUser.sql");
	}

	[Fact]
	public async Task Code_item_that_ended_the_deployment_transaction_is_not_retried_without_checking_the_transaction()
	{
		// on PostgreSQL the check would not see it if the check of the script had not rolled back what ran after the end.
		MockSet mockSet = new();
		mockSet.DbLiveDA.IsTransactionUsableAsync().Returns(true);

		DbLiveTransactionEndedException scriptError = new("for example ...");
		mockSet.DbLiveDA
			.ExecuteCodeItemAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
			.Returns(Task.FromException(scriptError));

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		CodeItemDeployResult res;
		using (TransactionScope _ = TransactionScopeManager.Create())
		{
			res = await deployer.DeployAsync(GetCodeItem(relativePath: "Code/dbo/GetUser.sql"));
		}

		Assert.False(res.IsSuccess);
		Assert.False(res.CanRetry);
		CodeDeploymentException ex = Assert.IsType<CodeDeploymentException>(res.Exception);
		Assert.Equal("Deploy code file error. File path: Code/dbo/GetUser.sql. The file ended the deployment transaction, so it is not retried.", ex.Message);
		Assert.Same(scriptError, ex.InnerException);
		_ = await mockSet.DbLiveDA.DidNotReceive().IsTransactionUsableAsync();
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SaveCodeItemAsync(default!);
	}

	[Fact]
	public async Task Code_item_that_ended_its_own_transaction_is_not_retried_and_saves_its_error_row()
	{
		// it ends its transaction on every run, and a retry would run on top of what its COMMIT committed.
		MockSet mockSet = new();

		DbLiveTransactionEndedException scriptError = new("for example ...");
		mockSet.DbLiveDA
			.ExecuteCodeItemAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
			.Returns(Task.FromException(scriptError));

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		CodeItemDeployResult res = await deployer.DeployAsync(GetCodeItem(relativePath: "Code/dbo/GetUser.sql"));

		Assert.False(res.IsSuccess);
		Assert.False(res.CanRetry);
		CodeDeploymentException ex = Assert.IsType<CodeDeploymentException>(res.Exception);
		Assert.Equal("Deploy code file error. File path: Code/dbo/GetUser.sql. The file ended its transaction, so it is not retried.", ex.Message);
		Assert.Same(scriptError, ex.InnerException);
		_ = await mockSet.DbLiveDA.DidNotReceive().IsTransactionUsableAsync();
		await mockSet.DbLiveDA.Received().SaveCodeItemAsync(Arg.Is<CodeItemDto>(i => i.Status == CodeItemStatus.Error && i.ErrorMessage == scriptError.Message));
	}

	[Fact]
	public async Task Failed_code_item_without_a_transaction_does_not_check_the_transaction()
	{
		// on SQL Server the check is '@@trancount > 0': without a transaction it would tell that no file can be retried.
		MockSet mockSet = new();
		mockSet.DbLiveDA
			.ExecuteCodeItemAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
			.Returns(Task.FromException(new DbLiveSqlException("Invalid object name 'dbo.C'.")));

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		CodeItemDeployResult res = await deployer.DeployAsync(GetCodeItem());

		Assert.False(res.IsSuccess);
		Assert.True(res.CanRetry);
		_ = await mockSet.DbLiveDA.DidNotReceive().IsTransactionUsableAsync();
	}

	[Fact]
	public async Task Code_item_is_executed_as_a_code_file()
	{
		// not as a plain statement: in the deployment transaction the provider keeps the transaction usable after an error of the file.
		MockSet mockSet = new();

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		CodeItemDeployResult res;
		using (TransactionScope _ = TransactionScopeManager.Create())
		{
			res = await deployer.DeployAsync(GetCodeItem(content: "create view a as select 1 as x;"));
		}

		Assert.True(res.IsSuccess);
		await mockSet.DbLiveDA.Received(1).ExecuteCodeItemAsync("create view a as select 1 as x;", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().ExecuteNonQueryAsync(default!, default, default);
	}

	[Fact]
	public async Task Failed_code_item_keeps_its_error_when_the_error_row_cannot_be_saved()
	{
		// the server is unreachable, for one: the result is the error of the file, with its path.
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		DbLiveSqlException scriptError = new("Incorrect syntax near 'selec'.");
		mockSet.DbLiveDA
			.ExecuteCodeItemAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
			.Returns(Task.FromException(scriptError));

		DbLiveSqlException saveError = new("A network-related or instance-specific error occurred while establishing a connection to SQL Server.");
		mockSet.DbLiveDA
			.SaveCodeItemAsync(Arg.Any<CodeItemDto>())
			.Returns(Task.FromException(saveError));

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		CodeItemDeployResult res = await deployer.DeployAsync(codeItem);

		Assert.False(res.IsSuccess);
		Assert.True(res.CanRetry);
		CodeDeploymentException ex = Assert.IsType<CodeDeploymentException>(res.Exception);
		Assert.Equal("Deploy code file error. File path: /path-to/some-code-item.sql", ex.Message);
		Assert.Same(scriptError, ex.InnerException);
		mockSet.Logger.Received().Warning(saveError, "The error row of {FilePath} is not saved.", "/path-to/some-code-item.sql");
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

	private const string WithoutTransactionMessage =
		"Code file {filePath} runs without a transaction of its own: it has DDL that the server does not allow in a transaction (In-Memory OLTP on SQL Server).";

	[Fact]
	public async Task Code_item_without_a_transaction_of_its_own_is_logged()
	{
		// a batch that fails leaves the batches before it applied: the log says why the file is not applied as a whole.
		MockSet mockSet = new();
		_ = mockSet.DbLiveDA.RunsCodeItemWithoutTransaction("create type dbo.Ids as table ...").Returns(true);

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		CodeItemDeployResult res = await deployer.DeployAsync(GetCodeItem(content: "create type dbo.Ids as table ...", relativePath: "Code/dbo/Ids.sql"));

		Assert.True(res.IsSuccess);
		mockSet.Logger.Received(1).Information(WithoutTransactionMessage, "Code/dbo/Ids.sql");
	}

	[Fact]
	public async Task Code_item_in_the_deployment_transaction_is_not_logged_as_without_a_transaction()
	{
		// it runs in the deployment transaction, whatever its text.
		MockSet mockSet = new();
		_ = mockSet.DbLiveDA.RunsCodeItemWithoutTransaction(Arg.Any<string>()).Returns(true);

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		CodeItemDeployResult res;
		using (TransactionScope _ = TransactionScopeManager.Create())
		{
			res = await deployer.DeployAsync(GetCodeItem());
		}

		Assert.True(res.IsSuccess);
		mockSet.Logger.DidNotReceive().Information(WithoutTransactionMessage, Arg.Any<string>());
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
