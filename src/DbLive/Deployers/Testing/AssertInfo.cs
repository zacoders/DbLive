
namespace DbLive.Deployers.Testing;

public record AssertInfo
{
	public AssertType AssertType { get; set; }
	public int? RowCount { get; set; }
}
