
using Xunit.Extensions.AssemblyFixture;

[assembly: CollectionBehavior(MaxParallelThreads = 0)]
[assembly: TestFramework(AssemblyFixtureFramework.TypeName, AssemblyFixtureFramework.AssemblyName)]
