
public class DeploySQL
{
    public void Deploy(string path)
    {
        string migrations = Path.Combine(path, "Migrations");
		Console.WriteLine(migrations);

		foreach (string file in Directory.EnumerateDirectories(migrations, "*.*", SearchOption.AllDirectories))
		{
			Console.WriteLine(file);
		}
	}
}