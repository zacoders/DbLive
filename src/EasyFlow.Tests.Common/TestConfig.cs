using Microsoft.Extensions.Configuration;

namespace EasyFlow.Tests.Config;

public class TestConfig
{
	IConfigurationRoot config;

	public TestConfig()
	{
		config = new ConfigurationBuilder()
		   .AddEnvironmentVariables() // environment vars go first
		   .AddJsonFile("appsettings.json", true, true)
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
