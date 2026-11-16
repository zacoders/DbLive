namespace DbLive.MSSQL.Tests;

/// <summary>
/// DbLive runs the <c>GO</c> batches of a script itself, and SMO splits the script in its capture mode, as its <c>ExecuteNonQuery</c>
/// did before: a file that deployed must be split into the same batches.
/// </summary>
public class MsSqlDABatchesTests
{
	[Theory]
	[InlineData("select 1", new[] { "select 1" })]
	[InlineData("select 1\nGO\nselect 2", new[] { "select 1\n", "select 2" })]
	[InlineData("select 1\nGO\nselect 2\n", new[] { "select 1\n", "select 2\n" })]
	[InlineData("select 1\r\nGO\r\nselect 2\r\n", new[] { "select 1\r\n", "select 2\r\n" })]
	// any case, spaces and tabs around it, a comment after it: the line of GO belongs to no batch.
	[InlineData("select 1\n  go\t -- comment\nselect 2", new[] { "select 1\n", "select 2" })]
	// GO <count>: the batch before it, count times.
	[InlineData("select 1\nGO 3\nselect 2", new[] { "select 1\n", "select 1\n", "select 1\n", "select 2" })]
	// GO on the first line and two GO in a row give empty batches, which are not run; a GO at the end gives none.
	[InlineData("GO\nselect 1\nGO\nGO\nselect 2\nGO\n", new[] { "", "select 1\n", "", "select 2\n" })]
	[InlineData("select 1\nGO\n\n", new[] { "select 1\n", "\n" })]
	[InlineData("", new string[0])]
	public void Script_is_split_at_GO_lines(string script, string[] batches)
	{
		Assert.Equal(batches, MsSqlDA.SplitIntoBatches(script).Select(b => b.Text));
	}

	// a batch starts at the line after the GO line of the batch before it; the error of a batch names that line.
	[Theory]
	[InlineData("select 1", new[] { 1 })]
	[InlineData("select 1\nGO\nselect 2", new[] { 1, 3 })]
	[InlineData("select 1\r\nGO\r\nselect 2\r\n", new[] { 1, 3 })]
	[InlineData("a\nb\nGO\n\nc\nd\nGO\ne", new[] { 1, 4, 8 })]
	[InlineData("a\r\nb\r\nGO\r\n\r\nc\r\nd\r\nGO\r\ne", new[] { 1, 4, 8 })]
	[InlineData("select 1\n  go\t -- comment\nselect 2", new[] { 1, 3 })]
	// CR alone ends a line for SMO too.
	[InlineData("select 1\rGO\rselect 2\r\nGO\nselect 3", new[] { 1, 3, 5 })]
	// GO on the first line and two GO in a row: the empty batches count as batches of no lines.
	[InlineData("GO\nselect 1\nGO\nGO\nselect 2\nGO\n", new[] { 1, 2, 4, 5 })]
	// GO inside a comment does not split, and its lines count.
	[InlineData("/*\nGO\n*/ select 1\nGO\nselect 2", new[] { 1, 5 })]
	[InlineData("select 1\nGO3\nGO\nselect 2", new[] { 1, 4 })]
	public void Batch_starts_after_the_GO_line_of_the_batch_before(string script, int[] startLines)
	{
		Assert.Equal(startLines, MsSqlDA.SplitIntoBatches(script).Select(b => b.StartLine));
	}

	[Theory]
	[InlineData("select 1\nGO;\nselect 2")]
	[InlineData("select 1 GO\nselect 2")]
	[InlineData("select 1\nGO3\nselect 2")]
	[InlineData("/*\nGO\n*/ select 1")]
	[InlineData("/* /* */\nGO\n*/ select 1")]
	[InlineData("select '\nGO\n'")]
	[InlineData("select [\nGO\n]")]
	[InlineData("select \"\nGO\n\"")]
	// sqlcmd commands are not processed: the server gets them as they are.
	[InlineData("select $(x)\n:setvar x 1\n:r file.sql")]
	public void GO_that_is_not_alone_on_its_line_or_is_inside_a_comment_string_or_brackets_does_not_split(string script)
	{
		Assert.Equal([script], MsSqlDA.SplitIntoBatches(script).Select(b => b.Text));
	}

	[Theory]
	[InlineData("/* never closed\nselect 1", "Missing end comment mark '*/'.")]
	[InlineData("select 1\nGO -1\nselect 2", "Incorrect syntax was encountered while parsing '-1'.")]
	[InlineData("select 1\nGO /* c */\nselect 2", "Incorrect syntax was encountered while parsing '/*'.")]
	public void Script_that_SMO_cannot_split_fails_before_any_batch_runs(string script, string message)
	{
		Exception ex = Assert.ThrowsAny<Exception>(() => MsSqlDA.SplitIntoBatches(script));

		Assert.Equal("BatchParserException", ex.GetType().Name);
		Assert.Equal(message, ex.Message);
	}
}
