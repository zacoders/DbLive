namespace EasyFlow.Deployers.Code;

public class CodeDeployer(
		ILogger _logger,
		IEasyFlowProject _project,
		ICodeItemDeployer _codeItemDeployer
	) : ICodeDeployer
{
	private readonly ILogger Logger = _logger.ForContext(typeof(CodeDeployer));

	public void DeployCode(bool isSelfDeploy, DeployParameters parameters)
	{
		if (!parameters.DeployCode)
		{
			return;
		}

		Logger.Information("Deploying Code.");

		var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parameters.NumberOfThreadsForCodeDeploy };

		var codeGroups = _project.GetCodeGroups();

		int failedCodeItemsCount = 0;

		foreach (var codeGroup in codeGroups)
		{
			Parallel.ForEach(codeGroup.CodeItems, parallelOptions, codeItem =>
			{
				if (!_codeItemDeployer.DeployCodeItem(isSelfDeploy, codeItem))
				{
					Interlocked.Increment(ref failedCodeItemsCount);
				}
			});
		}

		if (failedCodeItemsCount > 0)
		{
			throw new CodeDeploymentException($"Code deploy failed. Deployment of {failedCodeItemsCount} item(s) failed. See logs for details.");
		}

		Logger.Information("Code deploy successfully completed.");
	}
}
