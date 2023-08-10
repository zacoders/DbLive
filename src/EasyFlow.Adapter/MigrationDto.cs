namespace EasyFlow.Adapter
{
	public class MigrationDto
	{
		public int MigrationVersion { get; set; }
		public required string MigrationName { get; set; }
		public DateTime MigrationStarted { get; set; }
		public DateTime MigrationCompleted { get; set; }
	}
}