namespace DbLive.Project;

public interface ISettingsAccessor
{
	Task<DbLiveSettings> GetProjectSettingsAsync();
}
