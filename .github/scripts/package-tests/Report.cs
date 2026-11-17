/// <summary>
/// The output of the checks: a section for each project, a line for each check, and the failures again at the end.
/// </summary>
static class Report
{
	static readonly List<string> failures = [];
	static string section = "";

	public static bool Failed => failures.Count > 0;

	public static void Section(string name)
	{
		section = name;
		Console.WriteLine();
		Console.WriteLine($"== {name}");
	}

	public static void Ok(string message) => Console.WriteLine($"   ok: {message}");

	public static void Fail(string message)
	{
		failures.Add($"{section}: {message}");
		Console.WriteLine($"   FAIL: {message}");
	}

	public static void PrintFailures(string projectsFolder)
	{
		Console.Error.WriteLine();
		Console.Error.WriteLine($"{failures.Count} check(s) failed, projects kept in {projectsFolder}:");
		foreach (string failure in failures)
			Console.Error.WriteLine($"  {failure}");
	}
}
