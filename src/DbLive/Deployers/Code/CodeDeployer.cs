namespace DbLive.Deployers.Code;


internal class CodeDeployer(
	ILogger logger,
	ICodeItemDeployer _codeItemDeployer,
	ISettingsAccessor settingsAccessor,
	IDbLiveDA _da,
	ITimeProvider _timeProvider
) : ICodeDeployer
{
	private readonly ILogger _logger = logger.ForContext(typeof(CodeDeployer));

	public async Task DeployAsync(ProjectSnapshot project, DeployParameters parameters)
	{
		if (!parameters.DeployCode)
		{
			return;
		}

		_logger.Information("Deploying code.");

		List<IReadOnlyCollection<CodeItem>> groupsToDeploy = await SkipDeployedAsync(project.CodeGroups).ConfigureAwait(false);

		foreach (IReadOnlyCollection<CodeItem> codeItems in groupsToDeploy.Where(items => items.Count > 0))
		{
			await DeployGroupAsync(codeItems).ConfigureAwait(false);
		}

		await MarkRemovedFilesAsync(project).ConfigureAwait(false);

		_logger.Information("Code deploy successfully completed.");
	}

	/// <summary>
	/// The code items left to execute, by group. A deployment where nothing changed reads <c>dblive.code</c> with one query and sets
	/// <c>verified_utc</c> of the files it skips with one command, instead of two round trips per file.
	/// Before any file runs, the applied rows of the files left to execute become <see cref="CodeItemStatus.Applying"/>, with one command:
	/// a file whose run is not confirmed by its applied row runs again on the next deployment, also when its content comes back to the
	/// hash of the row. A run can change the database and not confirm it: the batches of an In-Memory OLTP file commit one by one, and a
	/// file commits before its row is written, so a broken connection or a stopped process leaves the old row. Nothing is written on
	/// failure, so an error of a write in <c>catch</c> cannot hide the error of the file. In the deployment transaction the mark is
	/// rolled back with the files, or replaced by the applied rows. The files of all groups are marked at once: a file of a later group
	/// that never started stays applying and runs once more, which is harmless for code.
	/// </summary>
	private async Task<List<IReadOnlyCollection<CodeItem>>> SkipDeployedAsync(IReadOnlyList<CodeGroup> codeGroups)
	{
		CodeRows rows = new(await _da.GetCodeItemsAsync().ConfigureAwait(false));

		List<string> deployedFiles = [];
		List<string> rerunFiles = [];
		List<IReadOnlyCollection<CodeItem>> groupsToDeploy = [];

		foreach (CodeGroup group in codeGroups)
		{
			List<CodeItem> codeItems = [];

			foreach (CodeItem codeItem in group.CodeItems)
			{
				if (rows.IsDeployed(codeItem.FileData))
				{
					_logger.Information("Deploying code file: {filePath} [hash match]", codeItem.FileData.RelativePath);
					deployedFiles.Add(codeItem.FileData.RelativePath);
				}
				else
				{
					codeItems.Add(codeItem);

					if (rows.HasAppliedRow(codeItem.FileData))
					{
						rerunFiles.Add(codeItem.FileData.RelativePath);
					}
				}
			}

			groupsToDeploy.Add(codeItems);
		}

		if (deployedFiles.Count > 0)
		{
			await _da.MarkCodeAsVerifiedAsync(deployedFiles, _timeProvider.UtcNow()).ConfigureAwait(false);
		}

		if (rerunFiles.Count > 0)
		{
			await _da.MarkCodeAsApplyingAsync(rerunFiles).ConfigureAwait(false);
		}

		return groupsToDeploy;
	}

	/// <summary>
	/// A file deleted from the project keeps its applied row, so the same file brought back by a later release (git revert)
	/// would be skipped by its hash, and the object it creates would not be restored. Its row is marked removed instead,
	/// and a removed file is executed again. Breaking scripts of the Code folder are files of the project too.
	/// </summary>
	private async Task MarkRemovedFilesAsync(ProjectSnapshot project)
	{
		List<string> projectFiles =
		[
			.. project.CodeGroups.SelectMany(g => g.CodeItems).Select(i => i.FileData.RelativePath),
			.. project.CodeBreakingItems.Select(i => i.FileData.RelativePath)
		];

		IReadOnlyCollection<string> removedFiles = await _da.MarkMissingCodeAsRemovedAsync(projectFiles, _timeProvider.UtcNow()).ConfigureAwait(false);

		if (removedFiles.Count > 0)
		{
			_logger.Information(
				"Code files removed from the project, marked as removed in dblive.code: {RelativePaths}",
				string.Join(", ", removedFiles.Order(StringComparer.Ordinal))
			);
		}
	}

	/// <summary>
	/// Deploys the files of a group in rounds, because SQL objects fail until the objects they use are deployed. The workers deploy
	/// every file of a round; the files that failed form the next round, which starts when the round before has finished. A file is
	/// then not tried again while a file it may need still runs on another worker.
	/// The group fails when a round deploys no file: the files left do not fail on the order of dependencies. Every round but the
	/// last deploys a file, so a group of N files takes at most N rounds.
	/// </summary>
	internal async Task DeployGroupAsync(IReadOnlyCollection<CodeItem> codeItems)
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		bool inAmbientTransaction = Transaction.Current is not null;
		if (inAmbientTransaction && projectSettings.NumberOfThreadsForCodeDeploy > 1)
		{
			_logger.Information(
				"Parallel code deploy disabled because deployment runs inside a transaction."
			);
		}

		int maxWorkers = inAmbientTransaction ? 1 : Math.Max(1, projectSettings.NumberOfThreadsForCodeDeploy);

		List<CodeItem> round = [.. codeItems];

		for (int roundNumber = 1; ; roundNumber++)
		{
			List<(CodeItem CodeItem, Exception Error)> failed =
				await DeployRoundAsync(round, Math.Min(maxWorkers, round.Count)).ConfigureAwait(false);

			if (failed.Count == 0)
			{
				return;
			}

			if (failed.Count == round.Count)
			{
				foreach ((CodeItem codeItem, Exception error) in failed)
				{
					// a file of round N failed in every round before it.
					_logger.Error(
						error,
						"Deployment failed for {FilePath} after {Attempts} attempts",
						codeItem.FileData.RelativePath, roundNumber
					);
				}

				throw new CodeDeploymentAggregateException("Code deployment failed.", failed.Select(f => f.Error));
			}

			_logger.Information("Retrying {FailedCount} code files that failed in round {Round}.", failed.Count, roundNumber);

			round = [.. failed.Select(f => f.CodeItem)];
		}
	}

	/// <summary>
	/// Deploys the files of one round and returns the ones that failed, in the order of the round.
	/// </summary>
	private async Task<List<(CodeItem CodeItem, Exception Error)>> DeployRoundAsync(List<CodeItem> codeItems, int workersCount)
	{
		ConcurrentQueue<int> queue = new(Enumerable.Range(0, codeItems.Count));
		CodeItemDeployResult?[] results = new CodeItemDeployResult?[codeItems.Count];

		using CancellationTokenSource cts = new();

		Task<Exception?>[] workers = new Task<Exception?>[workersCount];
		for (int workerId = 0; workerId < workersCount; workerId++)
		{
			workers[workerId] = Task.Run(() => RunWorkerAsync(codeItems, queue, results, cts));
		}

		Exception?[] stopErrors = await Task.WhenAll(workers).ConfigureAwait(false);

		List<Exception> notRetried = [.. stopErrors.OfType<Exception>()];

		if (notRetried.Count > 0)
		{
			// not "after N attempts": a file whose error left the transaction unusable is not retried.
			throw new CodeDeploymentAggregateException("Code deployment failed.", notRetried);
		}

		List<(CodeItem CodeItem, Exception Error)> failed = [];

		for (int i = 0; i < codeItems.Count; i++)
		{
			if (results[i] is { IsSuccess: false } result)
			{
				failed.Add((codeItems[i], result.Exception!));
			}
		}

		return failed;
	}

	/// <summary>
	/// Deploys files of the round until the queue is empty. Returns the error of a file that cannot be retried, which stops the
	/// other workers too.
	/// </summary>
	private async Task<Exception?> RunWorkerAsync(
		List<CodeItem> codeItems,
		ConcurrentQueue<int> queue,
		CodeItemDeployResult?[] results,
		CancellationTokenSource cts
	)
	{
		while (!cts.IsCancellationRequested && queue.TryDequeue(out int index))
		{
			CodeItem codeItem = codeItems[index];
			string relativePath = codeItem.FileData.RelativePath;

			_logger.Debug("Deploying {FilePath}", relativePath);

			CodeItemDeployResult result = await _codeItemDeployer.DeployAsync(codeItem).ConfigureAwait(false);

			results[index] = result;

			if (result.IsSuccess)
			{
				_logger.Debug("Successfully deployed {FilePath}", relativePath);
			}
			else if (!result.CanRetry)
			{
				_logger.Error(result.Exception, "Deployment failed for {FilePath}, not retried", relativePath);

				await cts.CancelAsync().ConfigureAwait(false);

				return result.Exception;
			}
			else
			{
				_logger.Debug(result.Exception, "Deployment failed for {FilePath}", relativePath);
			}
		}

		return null;
	}
}
