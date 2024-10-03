using Xunit.Abstractions;

namespace MyUnitTestingFramework.Unused;

public class MyTestMethod : ITestMethod
{
    public IMethodInfo Method => new MyMethodInfo(null, "test_method_name12333");

    public ITestClass TestClass => new MyTestClass();

    public void Deserialize(IXunitSerializationInfo info)
    {
        throw new NotImplementedException();
    }

    public void Serialize(IXunitSerializationInfo info)
    {
        throw new NotImplementedException();
    }
}
