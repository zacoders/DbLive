using DbLive.Common;

namespace DbLive.Deployers;

internal class ProjectIdValidator(
	ILogger logger,
	ISettingsAccessor settingsAccessor,
	IDbLiveInternalDA da
) : IProjectIdValidator
{
	public const int MaxProjectIdLength = 128;

	private const string DeploymentBlocked = "Deployment blocked to prevent deploying to the wrong database.";
	private const string TestsBlocked = "Tests blocked to prevent running them against the wrong database.";

	private readonly ILogger _logger = logger.ForContext(typeof(ProjectIdValidator));

	public async Task ValidateAsync()
	{
		string? settingsProjectId = await GetSettingsProjectIdAsync().ConfigureAwait(false);

		if (settingsProjectId is null)
		{
			_logger.Warning(
				"ProjectId is not set in settings.json, so nothing stops this project from being deployed to the database of another project. Set ProjectId in settings.json, for example \"ProjectId\": \"billing-api-db\"."
			);
			return;
		}

		// a new database has nothing to compare yet.
		if (!await da.DbLiveInstalledAsync().ConfigureAwait(false))
		{
			return;
		}

		string? dbProjectId = await da.GetProjectIdAsync().ConfigureAwait(false);

		if (dbProjectId is not null)
		{
			ThrowIfMismatch(settingsProjectId, dbProjectId, DeploymentBlocked);
		}
	}

	public async Task BindAsync()
	{
		string? settingsProjectId = await GetSettingsProjectIdAsync().ConfigureAwait(false);

		if (settingsProjectId is null)
		{
			return;
		}

		string? dbProjectId = await da.GetProjectIdAsync().ConfigureAwait(false);

		if (dbProjectId is not null)
		{
			ThrowIfMismatch(settingsProjectId, dbProjectId, DeploymentBlocked);
			return;
		}

		string normalizedProjectId = ProjectIdTools.Normalize(settingsProjectId);
		await da.SetProjectIdAsync(normalizedProjectId).ConfigureAwait(false);
		_logger.Information("ProjectId bound to database: {ProjectId}.", normalizedProjectId);
	}

	public async Task ValidateBeforeTestsAsync()
	{
		string? settingsProjectId = await GetSettingsProjectIdAsync().ConfigureAwait(false);

		// the deployment warns; the tests run against a database the project was deployed to.
		if (settingsProjectId is null)
		{
			return;
		}

		string? dbProjectId = await da.GetProjectIdAsync().ConfigureAwait(false);

		// a deployment of this project binds its ProjectId to the database.
		if (dbProjectId is null)
		{
			throw new ProjectIdMismatchException(
				$"The database has no ProjectId, settings ProjectId: {settingsProjectId}. The database was not deployed by this project. {TestsBlocked}");
		}

		ThrowIfMismatch(settingsProjectId, dbProjectId, TestsBlocked);
	}

	/// <returns>The ProjectId of settings.json, null when it is not set.</returns>
	private async Task<string?> GetSettingsProjectIdAsync()
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);
		string? settingsProjectId = projectSettings.ProjectId;

		if (string.IsNullOrWhiteSpace(settingsProjectId))
		{
			return null;
		}

		if (settingsProjectId.Length > MaxProjectIdLength)
		{
			throw new ProjectIdMismatchException(
				$"ProjectId exceeds maximum length of {MaxProjectIdLength} characters.");
		}

		return settingsProjectId;
	}

	private void ThrowIfMismatch(string settingsProjectId, string dbProjectId, string blocked)
	{
		if (ProjectIdTools.Equals(dbProjectId, settingsProjectId))
		{
			return;
		}

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
