using DbLive.Common;

namespace DbLive.xunit;

public interface IDbLiveFixtureBuilder
{
	string GetProjectPath();
	Task<DbLiveBuilder> GetBuilderAsync();
}