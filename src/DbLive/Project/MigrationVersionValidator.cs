using System.Globalization;

namespace DbLive.Project;

internal class MigrationVersionValidator(ITimeProvider timeProvider) : IMigrationVersionValidator
{
	// Validates the timestamp string and returns the parsed value
	public void Validate(long versionPrefixLong, string fileName)
	{
		string versionPrefix = versionPrefixLong.ToString(CultureInfo.InvariantCulture);

		// 1. Length check for exactly 14 characters
		if (string.IsNullOrWhiteSpace(versionPrefix) || versionPrefix.Length != 14)
		{
			string errorMessage = $"Bad version {versionPrefix}. Version prefix must be exactly 14 characters long.";
			throw new InvalidMigrationVersionException(fileName, errorMessage);
		}

		// 2. Calendar and format check
		string format = "yyyyMMddHHmmss";
		bool isValidDate = DateTime.TryParseExact(
			versionPrefix,
			format,
			CultureInfo.InvariantCulture,
			DateTimeStyles.None,
			out DateTime parsedDate);

		if (!isValidDate)
		{
			string errorMessage = "Version prefix must match 'yyyyMMddHHmmss' format.";
			throw new InvalidMigrationVersionException(fileName, errorMessage);
		}

		// 3. Logical boundary check (protects against fat-finger year typos)
		int maxYear = timeProvider.UtcNow().AddMonths(1).Year;
		int minYear = 2000; // Assuming we don't expect migrations before the year 2000
		if (parsedDate.Year < minYear || parsedDate.Year > maxYear)
		{
			string errorMessage = $"Year {parsedDate.Year} is out of bounds ({minYear} to {maxYear}).";
			throw new InvalidMigrationVersionException(fileName, errorMessage);
		}
	}
}