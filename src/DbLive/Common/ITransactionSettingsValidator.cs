namespace DbLive.Common;

public interface ITransactionSettingsValidator
{
	Task ValidateAsync(DbLiveSettings settings);
}
