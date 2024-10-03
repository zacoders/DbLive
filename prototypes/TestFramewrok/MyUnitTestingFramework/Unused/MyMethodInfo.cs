using Xunit.Abstractions;

namespace MyUnitTestingFramework.Unused;

public class MyMethodInfo : IMethodInfo
{
    private readonly IMethodInfo _methodInfo;
    private readonly string _methodName;

    public bool IsAbstract => _methodInfo.IsAbstract;

    public bool IsGenericMethodDefinition => _methodInfo.IsGenericMethodDefinition;

    public bool IsPublic => _methodInfo.IsPublic;

    public bool IsStatic => _methodInfo.IsStatic;

    public string Name => _methodName;

    public ITypeInfo ReturnType => _methodInfo.ReturnType;

    public ITypeInfo Type => _methodInfo.Type;

    public MyMethodInfo(IMethodInfo methodInfo, string methodName)
    {
        _methodInfo = methodInfo;
        _methodName = methodName;
    }

    public IEnumerable<IAttributeInfo> GetCustomAttributes(string assemblyQualifiedAttributeTypeName)
    {
        return _methodInfo.GetCustomAttributes(assemblyQualifiedAttributeTypeName);
    }

    public IEnumerable<ITypeInfo> GetGenericArguments()
    {
        return _methodInfo.GetGenericArguments();
    }

    public IEnumerable<IParameterInfo> GetParameters()
    {
        return _methodInfo.GetParameters();
    }

    public IMethodInfo MakeGenericMethod(params ITypeInfo[] typeArguments)
    {
        return _methodInfo.MakeGenericMethod(typeArguments);
    }
}
