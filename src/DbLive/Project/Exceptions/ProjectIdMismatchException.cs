namespace DbLive.Project.Exceptions;

[ExcludeFromCodeCoverage]
public class ProjectIdMismatchException(string message) : Exception(message);
