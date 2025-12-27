
using MoreLinq.Extensions;

namespace DbLive.Deployers.Testing;

public static class AssertTypeExtensions
{
	static Dictionary<AssertType, string> assertTypesMap = new()
	{
		{AssertType.Rows, "rows" },
		{AssertType.RowsWithSchema, "rows-with-schema" },
		{AssertType.HasRows, "has-rows" },
		{AssertType.RowCount, "row-count" },
		{AssertType.Empty, "empty" },
		{AssertType.SingleRow, "single-row" }
	};

	static Dictionary<string, AssertType> reverseAssertTypesMap =
		assertTypesMap.ToDictionary(i => i.Value, i => i.Key);

	public static AssertInfo ToAssertInfo(this string assertDefinition)
	{
		if (reverseAssertTypesMap.TryGetValue(assertDefinition.ToLower(), out AssertType value))
		{
			if (value == AssertType.Empty)
			{
				return new AssertInfo
				{
					AssertType = AssertType.RowCount,
					RowCount = 0
				};
			}

			if (value == AssertType.SingleRow)
			{
				return new AssertInfo
				{
					AssertType = AssertType.RowCount,
					RowCount = 1
				};
			}

			return new AssertInfo { AssertType = value };
		}

		if (assertDefinition.Contains("row-count"))
		{
			int equalIndex = assertDefinition.IndexOf("=");
			if (equalIndex != -1)
			{
				return new AssertInfo
				{
					AssertType = AssertType.RowCount,
					RowCount = Convert.ToInt32(assertDefinition.Substring(equalIndex + 1))
				};
			}
		}

		throw new Exception($"Unknown assert type {assertDefinition}. Supported values: {SupportedAssertTypes()}");
	}

	private static string SupportedAssertTypes() =>
		string.Join(", ", reverseAssertTypesMap.Keys);
}