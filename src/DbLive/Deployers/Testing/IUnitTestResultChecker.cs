namespace DbLive.Deployers.Testing;

internal interface IUnitTestResultChecker
{
	ValidationResult ValidateTestResult(List<SqlResult> multiResult);
}