namespace DbLive.Common;

internal interface ITransactionSettingsValidator
{
	Task ValidateAsync(DbLiveSettings settings);
}
