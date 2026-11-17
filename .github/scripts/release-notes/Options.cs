/// <summary>The command line of release-notes.cs, described at the top of that file.</summary>
record Options(string? Title, string? Version, string? Output, bool Check)
{
	public static Options Parse(string[] args)
	{
		string? title = null, version = null, output = null;
		bool check = false;
		for (int i = 0; i < args.Length; i++)
		{
			string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
			switch (args[i])
			{
				case "--title": title = Value(); break;
				case "--version": version = Value(); break;
				case "--output": output = Value(); break;
				case "--check": check = true; break;
				default: throw new ArgumentException($"Unknown argument '{args[i]}'.");
			}
		}
		return new Options(title, version, output, check);
	}
}
