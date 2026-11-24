namespace DbLive.Deployers.Testing;

/// <summary>
/// The assert of a test file is not valid: an assert result set without rows, more than one, an unknown assert value, or a result set
/// that looks like a misnamed assert. The test fails with the message, and <see cref="TestRunResult.Exception"/> holds it.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class InvalidAssertException(string message) : Exception(message);
