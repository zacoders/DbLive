namespace DbLive.MSSQL.Tests;

/// <summary>
/// The column <c>error</c> of the dblive tables is <c>nvarchar(4000)</c>: MsSqlDA cuts a longer text to fit.
/// </summary>
public class MsSqlDAFitErrorColumnTests
{
	[Fact]
	public void No_text_stays_none()
	{
		Assert.Null(MsSqlDA.FitErrorColumn(null));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(MsSqlDA.ErrorColumnLength)]
	public void Text_that_fits_is_kept(int length)
	{
		string error = new('x', length);

		Assert.Same(error, MsSqlDA.FitErrorColumn(error));
	}

	[Fact]
	public void Longer_text_is_cut_to_the_column_length_and_ends_with_the_mark()
	{
		string error = new('x', MsSqlDA.ErrorColumnLength + 1);

		string fitted = MsSqlDA.FitErrorColumn(error)!;

		Assert.Equal(MsSqlDA.ErrorColumnLength, fitted.Length);
		Assert.Equal(new string('x', MsSqlDA.ErrorColumnLength - MsSqlDA.TruncatedMark.Length) + MsSqlDA.TruncatedMark, fitted);
	}

	[Fact]
	public void Surrogate_pair_at_the_cut_is_not_split()
	{
		// the cut keeps 3985 characters: the last one would be the high surrogate of the pair.
		int kept = MsSqlDA.ErrorColumnLength - MsSqlDA.TruncatedMark.Length;
		string error = new string('x', kept - 1) + "😀" + new string('y', 100);

		string fitted = MsSqlDA.FitErrorColumn(error)!;

		Assert.Equal(new string('x', kept - 1) + MsSqlDA.TruncatedMark, fitted);
	}
}
