
namespace EasyFlow.Deployers.Testing;

public static class AssertTypeExtensions
{
	public static AssertType ToAssertType(this string assertType)
	{
		return assertType.ToLower() switch
		{
			"rows" => AssertType.Rows,
			"rows-with-schema" => AssertType.RowsWithSchema,
			"has-rows" => AssertType.HasRows,
			_ => throw new Exception($"Unknown assert type {assertType}. Supported values: '{SupportedAssertTypes()}'")
		};
	}

	private static string SupportedAssertTypes() =>
		string.Join(",", Enum.GetNames(typeof(AssertType)).Select(name => name.ToLower()));
}