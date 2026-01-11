
namespace DbLive.xunit;

[Serializable]
internal class WrongDbLiveTestingFixtureTypeException(string? message) 
	: Exception(message)
{
}