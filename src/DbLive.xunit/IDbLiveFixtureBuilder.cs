namespace DbLive.xunit;

public interface IDbLiveFixtureBuilder
{
	Task<DbLiveBuilder> GetBuilderAsync();
}