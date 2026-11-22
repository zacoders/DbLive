namespace DbLive;

/// <summary>
/// The <c>ProjectId</c> of <c>settings.json</c> differs from the one stored in the database: the connection string points to the
/// database of another project. Before the SQL tests also a database with no stored <c>ProjectId</c>. And every deployment and test
/// run with a <c>ProjectId</c> longer than 128 characters. Nothing ran, also not the upgrade of the <c>dblive</c> tables.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ProjectIdMismatchException : Exception
{
	internal ProjectIdMismatchException(string message)
		: base(message)
	{
	}
}
