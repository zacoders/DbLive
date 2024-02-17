using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Code;

public class CodeItemDeployer(
        ILogger logger,
        IEasyFlowDA _da,
        ITimeProvider _timeProvider
    ) : ICodeItemDeployer
{
    private readonly ILogger _logger = logger.ForContext(typeof(CodeItemDeployer));

    private readonly RetryPolicy _codeItemRetryPolicy =
        Policy.Handle<Exception>()
              .WaitAndRetry(
                    5,
                    retryAttempt => TimeSpan.FromSeconds(retryAttempt * retryAttempt)
              );

	/// <inheritdoc/>
	public bool DeployCodeItem(bool isSelfDeploy, CodeItem codeItem)
    {
        try
        {
            _logger.Information("Deploying code file: {filePath}", codeItem.FileData.FilePath.GetLastSegment());

            if (!isSelfDeploy)
            {
                CodeItemDto? codeItemDto = _da.FindCodeItem(codeItem.FileData.RelativePath);

                if (codeItemDto != null)
                {
                    if (codeItemDto.ContentHash == codeItem.FileData.Crc32Hash)
                    {
                        _da.MarkCodeAsVerified(codeItem.FileData.RelativePath, _timeProvider.UtcNow());
                        _logger.Information("Code file deploy skipped, (hash match): {filePath}", codeItem.FileData.FilePath.GetLastSegment());
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
            _logger.Error(ex, "Deploy code file error. File path: {filePath}", codeItem.FileData.FilePath);
            return false;
        }
    }
}
