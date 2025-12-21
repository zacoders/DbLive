using System.Collections.Concurrent;

namespace EasyFlow.Deployers.Code;

public class CodeDeployer(
		ILogger _logger,
		IEasyFlowProject _project,
		ICodeItemDeployer _codeItemDeployer
	) : ICodeDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(CodeDeployer));

	public void DeployCode(bool isSelfDeploy, DeployParameters parameters)
	{
		if (!parameters.DeployCode)
		{
			return;
		}

		_logger.Information("Deploying Code.");

		var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parameters.NumberOfThreadsForCodeDeploy };

		var codeGroups = _project.GetCodeGroups();

		ConcurrentBag<(string FilePath, Exception Error)> failedFiles = new();

		foreach (var codeGroup in codeGroups)
		{
			Parallel.ForEach(codeGroup.CodeItems, parallelOptions, codeItem =>
			{
				CodeItemDeployResult result = _codeItemDeployer.DeployCodeItem(isSelfDeploy, codeItem);
				if (!result.IsSuccess)
				{
					// Use named tuple literal to ensure element names are set
					failedFiles.Add((codeItem.FileData.FilePath, Error: result.Exception!));
				}
			});
		}

		if (failedFiles.Count > 0)
		{
			CodeDeploymentAggregateException ex = new (
				$"Code deploy failed. Deployment of {failedFiles.Count} item(s) failed.",
				failedFiles.Select(ff => ff.Error)
			);
			throw ex;
		}

		_logger.Information("Code deploy successfully completed.");
	}
}
