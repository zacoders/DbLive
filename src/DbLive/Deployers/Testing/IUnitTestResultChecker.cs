using DbLive.Adapter;

namespace DbLive.Deployers.Testing;

public interface IUnitTestResultChecker
{
	ValidationResult ValidateTestResult(List<SqlResult> multiResult);
}