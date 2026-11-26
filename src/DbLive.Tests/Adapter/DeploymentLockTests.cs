using System.Data.Common;

namespace DbLive.Tests.Adapter;

public class DeploymentLockTests
{
	[Fact]
	public async Task Lock_is_released_before_the_connection_is_closed()
	{
		DbConnection connection = Substitute.For<DbConnection>();
		Func<DbConnection, Task> release = Substitute.For<Func<DbConnection, Task>>();
		release(Arg.Any<DbConnection>()).Returns(Task.CompletedTask);

		await new DeploymentLock(connection, release).DisposeAsync();

		Received.InOrder(() =>
		{
			release(connection);
			connection.DisposeAsync();
		});
	}

	[Fact]
	public async Task Error_of_the_release_is_thrown_after_the_connection_is_closed()
	{
		// a broken connection: the deployment logs the error as a warning.
		DbConnection connection = Substitute.For<DbConnection>();

		InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
			async () => await new DeploymentLock(connection, _ => Task.FromException(new InvalidOperationException("connection is broken"))).DisposeAsync()
		);

		Assert.Equal("connection is broken", ex.Message);
		await connection.Received(1).DisposeAsync();
	}
}
