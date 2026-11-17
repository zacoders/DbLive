namespace DbLive.SelfDeployer;

/// <summary>
/// DbLive's own scripts (the <c>dblive</c> schema), embedded in the provider assembly.
/// A separate type keeps them apart from the project's <see cref="IScriptSource"/> in DI.
/// </summary>
internal interface IInternalScriptSource : IScriptSource
{
}
