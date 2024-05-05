using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Testing;

public interface IUnitTestResultChecker
{
	CompareResult ValidateTestResult(MultipleResults multiResult);
}