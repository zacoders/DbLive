namespace DbLive.Deployers.Testing;

public interface IUnitTestsRunner
{
	Task RunAllTestsAsync(DeployParameters parameters);
}
