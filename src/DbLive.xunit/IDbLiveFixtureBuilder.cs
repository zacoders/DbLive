using DbLive.Common;

namespace DbLive.xunit;

public interface IDbLiveFixtureBuilder
{
	Task<DbLiveBuilder> GetBuilderAsync();
}