namespace DbLive.Exceptions;

[ExcludeFromCodeCoverage]
public class FileContentChangedException(string relativePath, int fileHash, int dbHash)
	: Exception($"The content of the file has been modified since the previous deployment. File Path: {relativePath}. File hash: {fileHash}, DB hash: {dbHash}.")
{
	public string RelativePath { get; } = relativePath;
}