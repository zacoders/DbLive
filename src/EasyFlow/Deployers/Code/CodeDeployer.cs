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

		ConcurrentBag<string> failedFiles = [];

		foreach (var codeGroup in codeGroups)
		{
			Parallel.ForEach(codeGroup.CodeItems, parallelOptions, codeItem =>
			{
				if (!_codeItemDeployer.DeployCodeItem(isSelfDeploy, codeItem))
				{
					failedFiles.Add(Path.Combine(codeItem.FileData.FilePath));
				}
			});
		}

		if (failedFiles.Count() > 0)
		{
			CodeDeploymentException ex = new ($"Code deploy failed. Deployment of {failedFiles.Count()} item(s) failed.");
			ex.Data["FailedFiles"] = failedFiles;
			throw ex;
		}

		_logger.Information("Code deploy successfully completed.");
	}
}
