using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Code;

public class CodeItemDeployer(
        ILogger _logger,
        IEasyFlowDA _da,
        ITimeProvider _timeProvider
    )
{
    private readonly ILogger Logger = _logger.ForContext(typeof(CodeItemDeployer));

    private readonly RetryPolicy _codeItemRetryPolicy =
        Policy.Handle<Exception>()
              .WaitAndRetry(
                    10,
                    retryAttempt => TimeSpan.FromSeconds(retryAttempt * retryAttempt)
              );

	/// <summary>
	/// Deploys code item.
	/// </summary>
	/// <param name="isSelfDeploy"></param>
	/// <param name="codeItem"></param>
	/// <returns>Returns false if there was any error during deployment.</returns>
	internal bool DeployCodeItem(bool isSelfDeploy, CodeItem codeItem)
    {
        try
        {
            Logger.Information("Deploying code file: {filePath}", codeItem.FileData.FilePath.GetLastSegment());

            if (!isSelfDeploy)
            {
                CodeItemDto? codeItemDto = _da.FindCodeItem(codeItem.FileData.RelativePath);

                if (codeItemDto != null)
                {
                    if (codeItemDto.ContentHash == codeItem.FileData.Crc32Hash)
                    {
                        _da.MarkCodeAsVerified(codeItem.FileData.RelativePath, _timeProvider.UtcNow());
                        Logger.Information("Code file deploy skipped, (hash match): {filePath}", codeItem.FileData.FilePath.GetLastSegment());
                        return true;
                    }

                    throw new FileContentChangedException(
                        codeItem.FileData.RelativePath,
                        codeItem.FileData.Crc32Hash,
                        codeItemDto.ContentHash
                    );
                }
            }

            DateTime migrationStartedUtc = _timeProvider.UtcNow();
            _codeItemRetryPolicy.Execute(() =>
            {
                _da.ExecuteNonQuery(codeItem.FileData.Content);
            });
            DateTime migrationCompletedUtc = _timeProvider.UtcNow();

            if (!isSelfDeploy)
            {
                _da.MarkCodeAsApplied(codeItem.FileData.RelativePath, codeItem.FileData.Crc32Hash, migrationCompletedUtc, (int)(migrationCompletedUtc - migrationStartedUtc).TotalMilliseconds);
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Deploy code file error. File path: {filePath}", codeItem.FileData.FilePath);
            return false;
        }
    }
}
