using Microsoft.Extensions.Configuration;

namespace EasyFlow.Tests.Config;

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
			throw new Exception($"Setting {settingName} was not found.");
		}

		return setting;
	}

	public string GetSqlServerConnectionString()
	{
		return GetSetting("SqlServer");
	}
}
