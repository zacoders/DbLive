using System.Text;

namespace DbLive.Tests.Common;

public static class ScriptPaths
{
	/// <summary>
	/// A relative path of <paramref name="length"/> characters: <paramref name="fileName"/> in <paramref name="folder"/>, in subfolders
	/// of at most 101 characters (a file system holds at most 255 in one name) that make up the length.
	/// </summary>
	public static string OfLength(string folder, string fileName, int length)
	{
		int subFolders = length - folder.Length - fileName.Length - 2;
		ArgumentOutOfRangeException.ThrowIfLessThan(subFolders, 1, nameof(length));

		StringBuilder path = new($"{folder}/");

		for (; subFolders > 101; subFolders -= 101)
		{
			_ = path.Append('a', 100).Append('/');
		}

		return path.Append('a', subFolders).Append('/').Append(fileName).ToString();
	}
}
