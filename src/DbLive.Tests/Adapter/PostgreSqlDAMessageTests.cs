using DbLive.PostgreSQL;
using Npgsql;

namespace DbLive.Tests.Adapter;

public class PostgreSqlDAMessageTests
{
	[Fact]
	public void Error_of_the_server_keeps_its_message()
	{
		PostgresException ex = new("relation \"t1\" does not exist", "ERROR", "ERROR", "42P01");

		Assert.Equal(ex.Message, PostgreSqlDA.GetMessage(ex));
	}

	[Fact]
	public void Command_timeout_has_the_cause_from_the_inner_exception()
	{
		// what Npgsql throws on a command timeout.
		NpgsqlException ex = new("Exception while reading from stream", new TimeoutException("Timeout during reading attempt"));

		Assert.Equal(
			$"Exception while reading from stream{Environment.NewLine}Timeout during reading attempt",
			PostgreSqlDA.GetMessage(ex)
		);
	}

	[Fact]
	public void Repeated_messages_of_the_chain_are_written_once()
	{
		NpgsqlException ex = new(
			"Exception while reading from stream",
			new NpgsqlException("Exception while reading from stream", new IOException("Connection reset by peer"))
		);

		Assert.Equal(
			$"Exception while reading from stream{Environment.NewLine}Connection reset by peer",
			PostgreSqlDA.GetMessage(ex)
		);
	}
}
