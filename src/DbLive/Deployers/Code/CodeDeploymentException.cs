namespace DbLive;

/// <summary>
/// A file of the Code folder failed; the message names it. Thrown for a breaking script of the Code folder, after which the breaking
/// scripts that follow do not run; a code file's is an inner exception of <see cref="CodeDeploymentAggregateException"/>.
/// The inner exception is the error of the file, usually a <see cref="DbLiveSqlException"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class CodeDeploymentException : Exception
{
	internal CodeDeploymentException(string errorMessage, Exception innerException)
		: base(errorMessage, innerException)
	{
	}
}
