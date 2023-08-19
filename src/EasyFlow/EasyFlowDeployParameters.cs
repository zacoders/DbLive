namespace EasyFlow;

public class EasyFlowDeployParameters
{
	/// <summary>
	/// The migration version to deploy. If version is specified code (SPs/Function) cannot be deployed.
	/// NULL value -> latest version.
	/// </summary>
	public int? MaxVersionToDeploy { get; set; }

	public static EasyFlowDeployParameters Default => new();
}