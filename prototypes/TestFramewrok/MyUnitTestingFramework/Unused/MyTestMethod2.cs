using Xunit.Abstractions;

namespace MyUnitTestingFramework.Unused;

public class MyTestMethod2 : ITestMethod
{
    private readonly ITestMethod _testMethod;

    public IMethodInfo Method => new MyMethodInfo(_testMethod.Method, "MyNewMethod123");

    public ITestClass TestClass => _testMethod.TestClass;

    public MyTestMethod2(ITestMethod testMethod)
    {
        _testMethod = testMethod;
    }

    public void Deserialize(IXunitSerializationInfo info)
    {
        throw new NotImplementedException();
    }

    public void Serialize(IXunitSerializationInfo info)
    {
        throw new NotImplementedException();
    }
}
