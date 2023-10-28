namespace EasyFlow.Deployers;

public class CodeDeployer
{
	private static readonly ILogger Logger = Log.ForContext(typeof(CodeDeployer));

	private readonly IEasyFlowDA _da;
	private readonly ITimeProvider _timeProvider;
	private readonly IEasyFlowProject _project;

	private readonly RetryPolicy _codeItemRetryPolicy =
		Policy.Handle<Exception>()
			  .WaitAndRetry(
					10,
					retryAttempt => TimeSpan.FromSeconds(retryAttempt * retryAttempt)
			  );

	public CodeDeployer(
		IEasyFlowProject easyFlowProject,
		IEasyFlowDA easyFlowDA,
		ITimeProvider timeProvider
		)
	{
		_project = easyFlowProject;
		_da = easyFlowDA;
		_timeProvider = timeProvider;
	}

	public void DeployCode(bool isSelfDeploy, string sqlConnectionString, DeployParameters parameters)
	{
		if (!parameters.DeployCode)
		{
			return;
		}

		Logger.Information("Deploying Code.");

		var codeItems = _project.GetCodeItems();
		var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parameters.NumberOfThreadsForCodeDeploy };

		int failedCodeItemsCount = 0;
		Parallel.ForEach(codeItems, parallelOptions, codeItem =>
		{
			if (!DeployCodeItem(isSelfDeploy, sqlConnectionString, codeItem))
			{
				Interlocked.Increment(ref failedCodeItemsCount);
			}
		});

		if (failedCodeItemsCount > 0)
		{
			throw new EasyFlowSqlException($"Code deploy failed. Deployment of {failedCodeItemsCount} item(s) failed. See logs for details.");
		}

		Logger.Information("Code deploy successfully completed.");
	}

	/// <summary>
	/// Deploys code item.
	/// </summary>
	/// <param name="isSelfDeploy"></param>
	/// <param name="sqlConnectionString"></param>
	/// <param name="codeItem"></param>
	/// <returns>Returns false if there was any error during deployment.</returns>
	private bool DeployCodeItem(bool isSelfDeploy, string sqlConnectionString, CodeItem codeItem)
	{
		//TODO: add unit tests for thsi code!
		try
		{
			Logger.Information("Deploy code file: {filePath}", codeItem.FileData.FilePath.GetLastSegment());

			if (!isSelfDeploy)
			{
				CodeItemDto? codeItemDto = _da.FindCodeItem(sqlConnectionString, codeItem.FileData.RelativePath);

				if (codeItemDto != null)
				{
					if (codeItemDto.ContentHash == codeItem.FileData.Crc32Hash)
					{
						_da.MarkCodeAsVerified(sqlConnectionString, codeItem.FileData.RelativePath, _timeProvider.UtcNow());
						return true;
					}

					throw new FileContentChangedException(
						codeItem.FileData.RelativePath,
						codeItem.FileData.Crc32Hash,
						codeItemDto.ContentHash
					);
				}
			}

			DateTime migrationStartedUtc = _timeProvider.UtcNow();
			_codeItemRetryPolicy.Execute(() =>
			{
				_da.ExecuteNonQuery(sqlConnectionString, codeItem.FileData.Content);
			});
			DateTime migrationCompletedUtc = _timeProvider.UtcNow();

			if (!isSelfDeploy)
			{
				_da.MarkCodeAsApplied(sqlConnectionString, codeItem.FileData.RelativePath, codeItem.FileData.Crc32Hash, migrationCompletedUtc, (int)(migrationCompletedUtc - migrationStartedUtc).TotalMilliseconds);
			}

			return true;
		}
		catch (Exception ex)
		{
			Logger.Error(ex, "Deploy code file error. File path: {filePath}", codeItem.FileData.FilePath);
			return false;
		}
	}
}
