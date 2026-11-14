
namespace DbLive.Deployers.Testing;

internal record AssertInfo
{
	public AssertType AssertType { get; set; }
	public int? RowCount { get; set; }
}
