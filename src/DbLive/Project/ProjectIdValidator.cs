using DbLive.Common.Settings;
using DbLive.Project.Exceptions;

namespace DbLive.Project;

internal class ProjectIdValidator(
	ILogger logger,
	ISettingsAccessor settingsAccessor,
	IDbLiveDA da
) : IProjectIdValidator
{
	public const int MaxProjectIdLength = 128;

	private readonly ILogger _logger = logger.ForContext(typeof(ProjectIdValidator));

	public async Task ValidateAsync()
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);
		string? settingsProjectId = projectSettings.ProjectId;

		if (string.IsNullOrWhiteSpace(settingsProjectId))
		{
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
			await da.SetProjectIdAsync(settingsProjectId).ConfigureAwait(false);
			_logger.Information("ProjectId bound to database: {ProjectId}.", settingsProjectId);
			return;
		}

		if (dbProjectId != settingsProjectId)
		{
			_logger.Error(
				"ProjectId mismatch. Settings ProjectId: {SettingsProjectId}, database ProjectId: {DatabaseProjectId}. Deployment blocked to prevent deploying to the wrong database.",
				settingsProjectId,
				dbProjectId
			);
			throw new ProjectIdMismatchException(
				$"ProjectId mismatch. Settings ProjectId: {settingsProjectId}, database ProjectId: {dbProjectId}. Deployment blocked to prevent deploying to the wrong database.");
		}
	}
}
