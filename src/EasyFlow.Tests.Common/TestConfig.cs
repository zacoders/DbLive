using Microsoft.Extensions.Configuration;

namespace EasyFlow.Tests.Common;

public class TestConfig
{
	IConfigurationRoot config;

	public TestConfig()
	{
		config = new ConfigurationBuilder()
		   .AddJsonFile("appsettings.json", true, true)
		   .AddEnvironmentVariables() // environment ovveride appsettings.json config.
		   .Build();
	}

	private string GetSetting(string settingName)
	{
		var setting = config[settingName];

		if (setting == null)
		{
			throw new Exception($"Setting '{settingName}' was not found.");
		}

		return setting;
	}

	private string? GetConnectionString(string settingName)
	{
		var cnnString = config.GetConnectionString(settingName);

		//if (cnnString == null)
		//{
		//	throw new Exception($"Connection string '{settingName}' was not found.");
		//}

		return cnnString;
	}

	public string? GetSqlServerConnectionString() => GetConnectionString("SQLSERVER");

	public string? GetPostgreSqlConnectionString() => GetConnectionString("POSTGRESQL");
}
