namespace DbLive.Deployers.Migrations;

[ExcludeFromCodeCoverage]
public class DowngradeNotAllowedException(string errorMessage)
	: Exception(errorMessage)
{
}