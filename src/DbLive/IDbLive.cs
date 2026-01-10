namespace DbLive;

public interface IDbLive
{
	Task DeployAsync(DeployParameters parameters);
}