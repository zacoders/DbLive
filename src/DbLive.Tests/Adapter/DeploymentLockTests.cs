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
	public async Task Error_of_the_release_is_not_thrown_and_the_connection_is_closed()
	{
		// a broken connection: the error would replace the error of the deployment.
		DbConnection connection = Substitute.For<DbConnection>();

		await new DeploymentLock(connection, _ => Task.FromException(new InvalidOperationException("connection is broken"))).DisposeAsync();

		await connection.Received(1).DisposeAsync();
	}
}
