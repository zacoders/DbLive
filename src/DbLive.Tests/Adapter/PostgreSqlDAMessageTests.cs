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
	public void Sql_error_has_the_hint_and_the_position_once()
	{
		PostgresException ex = new(
			"function f(integer) does not exist",
			"ERROR",
			"ERROR",
			"42883",
			hint: "No function matches the given name and argument types. You might need to add explicit type casts.",
			position: 8
		);

		string[] lines = PostgreSqlDA.GetSqlError(ex).ReplaceLineEndings("\n").Split('\n');

		Assert.Contains("Hint: No function matches the given name and argument types. You might need to add explicit type casts.", lines);

		// Npgsql writes the position into the message, on a line of its own.
		Assert.Contains("Message: 42883: function f(integer) does not exist", lines);
		Assert.Equal("POSITION: 8", Assert.Single(lines, line => line.Contains("position", StringComparison.OrdinalIgnoreCase)));
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
