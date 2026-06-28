namespace DbLive.Deployers;

[ExcludeFromCodeCoverage]
public class DeployLockFailedException(string message) : Exception(message);
