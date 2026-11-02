using DbLive.Common;

namespace DbLive.Deployers;

internal class ProjectIdValidator(
	ILogger logger,
	ISettingsAccessor settingsAccessor,
	IDbLiveDA da
) : IProjectIdValidator
{
	public const int MaxProjectIdLength = 128;

	private readonly ILogger _logger = logger.ForContext(typeof(ProjectIdValidator));

	public Task ValidateAsync() => ValidateAsync(beforeTests: false);

	public Task ValidateBeforeTestsAsync() => ValidateAsync(beforeTests: true);

	private async Task ValidateAsync(bool beforeTests)
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);
		string? settingsProjectId = projectSettings.ProjectId;

		if (string.IsNullOrWhiteSpace(settingsProjectId))
		{
			if (!beforeTests)
			{
				_logger.Warning(
					"ProjectId is not set in settings.json, so nothing stops this project from being deployed to the database of another project. Set ProjectId in settings.json, for example \"ProjectId\": \"billing-api-db\"."
				);
			}
			return;
		}

		if (settingsProjectId.Length > MaxProjectIdLength)
		{
			throw new ProjectIdMismatchException(
				$"ProjectId exceeds maximum length of {MaxProjectIdLength} characters.");
		}

		string? dbProjectId = await da.GetProjectIdAsync().ConfigureAwait(false);

		if (dbProjectId is null)
		{
			if (beforeTests)
			{
				// a deployment of this project binds its ProjectId to the database.
				throw new ProjectIdMismatchException(
					$"The database has no ProjectId, settings ProjectId: {settingsProjectId}. The database was not deployed by this project. Tests blocked to prevent running them against the wrong database.");
			}

			string normalizedProjectId = ProjectIdTools.Normalize(settingsProjectId);
			await da.SetProjectIdAsync(normalizedProjectId).ConfigureAwait(false);
			_logger.Information("ProjectId bound to database: {ProjectId}.", normalizedProjectId);
			return;
		}

		if (!ProjectIdTools.Equals(dbProjectId, settingsProjectId))
		{
			string blocked = beforeTests
				? "Tests blocked to prevent running them against the wrong database."
				: "Deployment blocked to prevent deploying to the wrong database.";

			_logger.Error(
				"ProjectId mismatch. Settings ProjectId: {SettingsProjectId}, database ProjectId: {DatabaseProjectId}. {Blocked}",
				settingsProjectId,
				dbProjectId,
				blocked
			);
			throw new ProjectIdMismatchException(
				$"ProjectId mismatch. Settings ProjectId: {settingsProjectId}, database ProjectId: {dbProjectId}. {blocked}");
		}
	}
}
