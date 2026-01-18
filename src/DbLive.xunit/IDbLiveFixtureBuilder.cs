using System.Reflection;

namespace DbLive.xunit;

public interface IDbLiveFixtureBuilder
{
	Task<DbLiveBuilder> GetBuilderAsync();
	Assembly GetProjectAssembly();
}