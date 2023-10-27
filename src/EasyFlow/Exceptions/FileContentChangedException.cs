namespace EasyFlow.Exceptions;

public class FileContentChangedException : Exception
{
	public string RelativePath { get; }

	public FileContentChangedException(string relativePath, int fileHash, int dbHash)
		: base($"The content of the file has been modified since the previous deployment. File Path: {relativePath}. File hash: {fileHash}, DB hash: {dbHash}.")
	{
		RelativePath = relativePath;
	}
}