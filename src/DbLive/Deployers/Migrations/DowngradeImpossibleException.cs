namespace DbLive.Deployers.Migrations;

[ExcludeFromCodeCoverage]
public class DowngradeImpossibleException(string errorMessage)
	: Exception(errorMessage)
{
}
