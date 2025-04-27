using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace MyUnitTestingFramework.Unused;

public class MyTypeInfo : LongLivedMarshalByRefObject, ITypeInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReflectionTypeInfo"/> class.
    /// </summary>
    /// <param name="type">The type to wrap.</param>
    public MyTypeInfo(Type type)
    {
        Type = type;
    }

    /// <inheritdoc/>
    public IAssemblyInfo Assembly
    {
        get { return Reflector.Wrap(Type.GetTypeInfo().Assembly); }
    }

    /// <inheritdoc/>
    public ITypeInfo BaseType
    {
        get { return Reflector.Wrap(Type.GetTypeInfo().BaseType); }
    }

    /// <inheritdoc/>
    public IEnumerable<ITypeInfo> Interfaces
    {
        get { return Type.GetTypeInfo().ImplementedInterfaces.Select(i => Reflector.Wrap(i)).ToList(); }
    }

    /// <inheritdoc/>
    public bool IsAbstract
    {
        get { return Type.GetTypeInfo().IsAbstract; }
    }

    /// <inheritdoc/>
    public bool IsGenericParameter
    {
        get { return Type.IsGenericParameter; }
    }

    /// <inheritdoc/>
    public bool IsGenericType
    {
        get { return Type.GetTypeInfo().IsGenericType; }
    }

    /// <inheritdoc/>
    public bool IsSealed
    {
        get { return Type.GetTypeInfo().IsSealed; }
    }

    /// <inheritdoc/>
    public bool IsValueType
    {
        get { return Type.GetTypeInfo().IsValueType; }
    }

    ///// <inheritdoc/>
    //public string Name
    //{
    //	get { return Type.FullName ?? Type.Name; }
    //}

    /// <inheritdoc/>
    public string Name => "MyTestName";

    /// <inheritdoc/>
    public Type Type { get; private set; }

    ///// <inheritdoc/>
    //public IEnumerable<IAttributeInfo> GetCustomAttributes(string assemblyQualifiedAttributeTypeName)
    //{
    //	return ReflectionAttributeInfo.GetCustomAttributes(Type, assemblyQualifiedAttributeTypeName).CastOrToList();
    //}

    /// <inheritdoc/>
    public IEnumerable<ITypeInfo> GetGenericArguments()
    {
        return Type.GetTypeInfo().GenericTypeArguments
                   .Select(t => Reflector.Wrap(t))
                   .ToList();
    }

    /// <inheritdoc/>
    public IMethodInfo GetMethod(string methodName, bool includePrivateMethod)
    {
        var method = GetRuntimeMethods()
                    .FirstOrDefault(m => (includePrivateMethod || m.IsPublic && m.DeclaringType != typeof(object)) && m.Name == methodName);

        if (method == null)
            return null;

        return Reflector.Wrap(method);
    }

    /// <inheritdoc/>
    public IEnumerable<IMethodInfo> GetMethods(bool includePrivateMethods)
    {
        var methodInfos = GetRuntimeMethods();

        if (!includePrivateMethods)
            methodInfos = methodInfos.Where(m => m.IsPublic);

        return methodInfos.Select(m => Reflector.Wrap(m)).ToList();
    }

    IEnumerable<MethodInfo> GetRuntimeMethods()
    {
        List<MethodInfo> results = new(Type.GetRuntimeMethods());

        for (var baseType = Type.GetTypeInfo().BaseType; baseType != null; baseType = baseType.GetTypeInfo().BaseType)
            results.AddRange(baseType.GetRuntimeMethods().Where(m => m.IsStatic));

        return results;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return Type.ToString();
    }

    public IEnumerable<IAttributeInfo> GetCustomAttributes(string assemblyQualifiedAttributeTypeName)
    {
        throw new NotImplementedException();
    }
}