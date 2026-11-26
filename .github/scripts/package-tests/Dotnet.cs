using System.Diagnostics;

static class Dotnet
{
	/// <summary>Runs dotnet: the exit code, and the output with the error output after it.</summary>
	public static (int ExitCode, string Output) Run(params string[] arguments)
	{
		var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
		foreach (string argument in arguments)
			info.ArgumentList.Add(argument);

		using var process = Process.Start(info)!;
		Task<string> error = process.StandardError.ReadToEndAsync();
		string output = process.StandardOutput.ReadToEnd();
		process.WaitForExit();
		return (process.ExitCode, output + error.Result);
	}
}
