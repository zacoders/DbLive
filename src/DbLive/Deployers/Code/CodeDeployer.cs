namespace DbLive.Deployers.Code;


public class CodeDeployer(
	ILogger logger,
	IDbLiveProject _project,
	ICodeItemDeployer _codeItemDeployer,
	ISettingsAccessor settingsAccessor
) : ICodeDeployer
{
	private readonly ILogger _logger = logger.ForContext(typeof(CodeDeployer));

	public async Task DeployAsync(DeployParameters parameters)
	{
		if (!parameters.DeployCode)
		{
			return;
		}

		_logger.Information("Deploying code.");

		foreach (CodeGroup group in await _project.GetCodeGroupsAsync().ConfigureAwait(false))
		{
			await DeployGroupAsync(group.CodeItems).ConfigureAwait(false);
		}

		_logger.Information("Code deploy successfully completed.");
	}

	internal async Task DeployGroupAsync(IReadOnlyCollection<CodeItem> codeItems)
	{
		DbLiveSettings _projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);
		int maxRetries = Math.Max(1, _projectSettings.MaxCodeDeployRetries);

		using var cts = new CancellationTokenSource();

		var queue = new ConcurrentQueue<CodeItem>();
		var retryCounters = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		foreach (CodeItem item in codeItems)
		{
			queue.Enqueue(item);
			retryCounters[item.FileData.RelativePath] = 0;
		}

		bool inAmbientTransaction = Transaction.Current is not null;
		if (inAmbientTransaction && _projectSettings.NumberOfThreadsForCodeDeploy > 1)
		{
			_logger.Information(
				"Parallel code deploy disabled because deployment runs inside a transaction."
			);
		}

		int workersCount = inAmbientTransaction
			? 1
			: Math.Min(Math.Max(1, _projectSettings.NumberOfThreadsForCodeDeploy), codeItems.Count);

		Task<Exception?>[] workers = new Task<Exception?>[workersCount];
		for (int workerId = 0; workerId < workersCount; workerId++)
		{
			Task<Exception?> worker = Task.Run(()
				=> CreateWorkerAsync(maxRetries, queue, retryCounters, cts)
			);
			workers[workerId] = worker;
		}

		_ = await Task.WhenAll(workers).ConfigureAwait(false);

		List<Exception> exceptions = [.. workers.Where(w => w.Result is not null).Select(w => w.Result!)];

		if (exceptions.Count > 0)
		{
			throw new CodeDeploymentAggregateException(
				$"Code deployment failed after {maxRetries} attempts.",
				exceptions
			);
		}
	}

	internal async Task<Exception?> CreateWorkerAsync(
		int maxRetries,
		ConcurrentQueue<CodeItem> queue,
		ConcurrentDictionary<string, int> retryCounters,
		CancellationTokenSource cts
	)
	{
		while (!cts.IsCancellationRequested)
		{
			if (!queue.TryDequeue(out CodeItem? codeItem))
			{
				break;
			}

			string relativePath = codeItem.FileData.RelativePath;

			_logger.Debug("Deploying {FilePath}", relativePath);

			CodeItemDeployResult result = await _codeItemDeployer.DeployAsync(codeItem).ConfigureAwait(false);

			if (result.IsSuccess)
			{
				_logger.Debug("Successfully deployed {FilePath}", relativePath);
				continue;
			}

			int retry = retryCounters.AddOrUpdate(
				relativePath,
				1,
				static (_, current) => current + 1
			);

			if (retry >= maxRetries)
			{
				_logger.Error(
					result.Exception,
					"Deployment failed for {FilePath} after {RetryCount} attempts",
					relativePath, retry
				);

				await cts.CancelAsync().ConfigureAwait(false);

				return result.Exception;
			}

			_logger.Debug(
				result.Exception,
				"Deployment failed for {FilePath}. Retry {Retry}/{MaxRetries}. Returning to queue.",
				relativePath, retry, maxRetries
			);

			// Queue-based retry is intentional: SQL objects may fail until their dependencies are deployed.
			// Moving the item to the tail lets other objects deploy first, then retries this item later.
			queue.Enqueue(codeItem);
		}
		return null;
	}
}
