using Xunit.Abstractions;

namespace MyUnitTestingFramework.Unused;

public class MyTestClass : ITestClass
{
    public ITypeInfo Class => new MyTypeInfo(typeof(object));

    public ITestCollection TestCollection => throw new NotImplementedException();

    public void Deserialize(IXunitSerializationInfo info)
    {
        throw new NotImplementedException();
    }

    public void Serialize(IXunitSerializationInfo info)
    {
        throw new NotImplementedException();
    }
}
