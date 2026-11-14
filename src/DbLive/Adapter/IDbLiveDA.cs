
namespace DbLive;

/// <summary>
/// Access to the database of the connection string, for tests that create their own database: drop it at the end.
/// Everything else DbLive does in the database is internal.
/// </summary>
public interface IDbLiveDA
{
	Task DropDbAsync(bool skipIfNotExists = true);
}
