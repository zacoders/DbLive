using System.Globalization;

namespace DbLive.Project;

internal class MigrationVersionValidator(ITimeProvider timeProvider) : IMigrationVersionValidator
{
	public void Validate(string relativePath)
	{
		// the version as written, not the parsed number: the number loses leading zeros, and 020260808104925 would pass as 14 digits.
		string versionPrefix = MigrationFileNameParser.GetVersionPart(relativePath);

		// 1. Length check for exactly 14 characters
		if (versionPrefix.Length != 14)
		{
			string errorMessage = $"Bad version {versionPrefix}. Version prefix must be exactly 14 characters long.";
			throw new InvalidMigrationVersionException(relativePath, errorMessage);
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
			throw new InvalidMigrationVersionException(relativePath, errorMessage);
		}

		// 3. Logical boundary check (protects against fat-finger year typos)
		int maxYear = timeProvider.UtcNow().AddMonths(1).Year;
		int minYear = 2000; // Assuming we don't expect migrations before the year 2000
		if (parsedDate.Year < minYear || parsedDate.Year > maxYear)
		{
			string errorMessage = $"Year {parsedDate.Year} is out of bounds ({minYear} to {maxYear}).";
			throw new InvalidMigrationVersionException(relativePath, errorMessage);
		}
	}
}