using System.Collections.Concurrent;

namespace DbLive.Deployers.Code;


public class CodeDeployer(
	ILogger logger,
	IDbLiveProject project,
	ICodeItemDeployer codeItemDeployer
) : ICodeDeployer
{
	private readonly ILogger _logger = logger.ForContext(typeof(CodeDeployer));
	private readonly IDbLiveProject _project = project;
	private readonly ICodeItemDeployer _codeItemDeployer = codeItemDeployer;

	public void DeployCode(bool isSelfDeploy, DeployParameters parameters)
	{
		if (!parameters.DeployCode)
		{
			return;
		}

		_logger.Information("Deploying code.");

		foreach (CodeGroup group in _project.GetCodeGroups())
		{
			DeployGroup(group.Path, group.CodeItems, isSelfDeploy, parameters);
		}

		_logger.Information("Code deploy successfully completed.");
	}

	internal void DeployGroup(string groupPath, IReadOnlyCollection<CodeItem> codeItems, bool isSelfDeploy, DeployParameters parameters)
	{
		var cts = new CancellationTokenSource();
		
		var queue = new ConcurrentQueue<CodeItem>();
		var retryCounters = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		foreach (var item in codeItems)
		{
			queue.Enqueue(item);
			retryCounters[item.FileData.FilePath] = 0;
		}

		Task<Exception?>[] workers = new Task<Exception?>[parameters.NumberOfThreadsForCodeDeploy];
		for (int workerId = 0; workerId < parameters.NumberOfThreadsForCodeDeploy; workerId++)
		{
			Task<Exception?> worker = Task.Run(()
				=> CreateWorker(isSelfDeploy, workerId, parameters.MaxCodeDeployRetries, queue, retryCounters, cts), cts.Token
			);
			workers[workerId] = worker;
		}
	
		Task.WaitAll(workers);

		List<Exception> exceptions = [.. workers.Where(w => w.Result is not null).Select(w => w.Result!)];

		if (exceptions.Count > 0)
		{
			throw new CodeDeploymentAggregateException(
				$"Code deployment failed for path '{groupPath}' after {parameters.MaxCodeDeployRetries} attempts.",
				exceptions
			);
		}
	}

	internal Exception? CreateWorker(
		bool isSelfDeploy, 
		int workerId, 
		int maxRetries, 
		ConcurrentQueue<CodeItem> queue, 
		ConcurrentDictionary<string, int> retryCounters, 
		CancellationTokenSource cts
	)
	{
		while (!cts.IsCancellationRequested)
		{
			if (!queue.TryDequeue(out var codeItem))
			{
				break;
			}

			string relativePath = codeItem.FileData.RelativePath;

			_logger.Debug("Deploying {FilePath}", relativePath);

			CodeItemDeployResult result = _codeItemDeployer.DeployCodeItem(isSelfDeploy, codeItem);

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

				cts.Cancel();

				return result.Exception;				
			}

			_logger.Debug(
				result.Exception,
				"Deployment failed for {FilePath}. Retry {Retry}/{MaxRetries}. Returning to queue.",
				relativePath, retry, maxRetries
			);

			queue.Enqueue(codeItem);
		}
		return null;
	}
}
