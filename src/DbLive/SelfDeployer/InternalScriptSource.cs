using System.Reflection;

namespace DbLive.SelfDeployer;

/// <summary>
/// DbLive's own scripts embedded in a provider assembly as <c>DbLive.Internal/&lt;path relative to SQL&gt;</c>.
/// </summary>
/// <remarks>
/// Not <c>DbLive/</c>, so that <c>SetProject</c> with a provider assembly throws <see cref="ScriptsNotEmbeddedException"/>
/// instead of deploying DbLive's own scripts as the project's migrations.
/// </remarks>
internal sealed class InternalScriptSource(Assembly providerAssembly)
	: AssemblyScriptSource(providerAssembly, Prefix), IInternalScriptSource
{
	internal const string Prefix = "DbLive.Internal/";
}
