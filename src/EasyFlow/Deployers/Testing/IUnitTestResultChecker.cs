using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Testing;

public interface IUnitTestResultChecker
{
	ValidationResult ValidateTestResult(List<SqlResult> multiResult);
}