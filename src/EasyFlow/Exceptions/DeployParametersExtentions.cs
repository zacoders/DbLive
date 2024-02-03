namespace EasyFlow.Exceptions;

[ExcludeFromCodeCoverage]
internal static class DeployParametersExtentions
{
	public static void Check(this DeployParameters parameters)
	{
		if (parameters.MaxVersionToDeploy is not null)
		{
			if (parameters.DeployCode)
			{
				throw new BadDeployParametersException(
					$"Code cannot be deployed if {nameof(parameters.MaxVersionToDeploy)} is set. There is no gurantee that Code is compatible with the deploying version.");
			}

			if (parameters.RunTests)
			{
				throw new BadDeployParametersException(
					$"Tests cannot be ran if {nameof(parameters.MaxVersionToDeploy)} is set. There is no gurantee that Tests are compatible with the deploying version.");
			}

		}
	}
}
