using EasyFlow.Adapter;
namespace EasyFlow;

public class EasyFlow(
		IEasyFlowDA _da,
		ILogger logger,
		IEasyFlowInternal _easyFlowInternal
	) : IEasyFlow
{
	private readonly ILogger _logger = logger.ForContext(typeof(EasyFlow));

	public void Deploy(DeployParameters parameters)
	{
		_logger.Information("Starting deployment.");

		parameters.Check();

		if (parameters.CreateDbIfNotExists)
			_da.CreateDB(true);

		// Self deploy. Deploying EasyFlow to the database
		_easyFlowInternal.SelfDeployProjectInternal();

		// Deploy actual project
		_easyFlowInternal.DeployProjectInternal(false, parameters);
	}

	public void DropDatabase(bool skipIfNotExists = true)
	{
		_da.DropDB(skipIfNotExists);
	}
}
