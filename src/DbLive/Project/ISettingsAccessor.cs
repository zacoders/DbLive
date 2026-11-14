namespace DbLive.Project;

internal interface ISettingsAccessor
{
	Task<DbLiveSettings> GetProjectSettingsAsync();
}
