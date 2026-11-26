
namespace DbLive.Tests.Schema;

public class SchemaDiffTests
{
	private static readonly SchemaObject Table = new(SchemaObjectType.Table, "dbo.Person", "", "dbo");
	private static readonly SchemaObject Id = new(SchemaObjectType.Column, "dbo.Person.Id", "int not null", "dbo.Person");
	private static readonly SchemaObject Name = new(SchemaObjectType.Column, "dbo.Person.Name", "nvarchar(50) not null", "dbo.Person");
	private static readonly SchemaObject PrimaryKey = new(SchemaObjectType.PrimaryKey, "dbo.Person", "clustered (Id asc)", "dbo.Person");

	[Fact]
	public void Same_objects_in_any_order_no_difference()
	{
		SchemaDiff diff = SchemaDiff.Compare(new([Table, Id, Name, PrimaryKey]), new([PrimaryKey, Name, Id, Table]));

		Assert.True(diff.IsEmpty);
		Assert.Equal("", diff.ToString());
	}

	[Fact]
	public void Missing_extra_and_changed_objects()
	{
		SchemaObject phone = new(SchemaObjectType.Column, "dbo.Person.Phone", "varchar(20) null", "dbo.Person");
		SchemaObject index = new(SchemaObjectType.Index, "dbo.Person.IX_Person_Name", "nonclustered (Name asc)", "dbo.Person");
		SchemaObject longerName = Name with { Definition = "nvarchar(100) not null" };

		SchemaDiff diff = SchemaDiff.Compare(new([Table, Id, Name, phone]), new([Table, Id, longerName, index]));

		Assert.Equal([phone], diff.Missing);
		Assert.Equal([index], diff.Extra);
		Assert.Equal([new SchemaObjectChange(Name, longerName)], diff.Changed);
		Assert.Equal(
			"""
			  missing column dbo.Person.Phone: varchar(20) null
			  extra index dbo.Person.IX_Person_Name: nonclustered (Name asc)
			  changed column dbo.Person.Name
			    was: nvarchar(50) not null
			    now: nvarchar(100) not null
			""".ReplaceLineEndings(),
			diff.ToString()
		);
	}

	[Fact]
	public void Same_name_of_another_type_is_another_object()
	{
		// a primary key is identified by its table.
		SchemaDiff diff = SchemaDiff.Compare(new([Table]), new([Table, PrimaryKey]));

		Assert.Empty(diff.Missing);
		Assert.Equal([PrimaryKey], diff.Extra);
	}

	[Fact]
	public void Names_compare_by_case()
	{
		SchemaObject lowerCase = Name with { Name = "dbo.Person.name" };

		SchemaDiff diff = SchemaDiff.Compare(new([Name]), new([lowerCase]));

		Assert.Equal([Name], diff.Missing);
		Assert.Equal([lowerCase], diff.Extra);
	}

	[Fact]
	public void Content_of_missing_table_is_counted_not_listed()
	{
		SchemaObject view = new(SchemaObjectType.View, "dbo.vPerson", "create view dbo.vPerson as select Id from dbo.Person", "dbo");

		SchemaDiff diff = SchemaDiff.Compare(new([Table, Id, Name, PrimaryKey, view]), new([view]));

		Assert.Equal(4, diff.Missing.Count);
		Assert.Equal("  missing table dbo.Person (with 2 columns, 1 primary key)", diff.ToString());
	}

	[Fact]
	public void Content_of_extra_schema_is_counted_with_the_content_of_its_tables()
	{
		SchemaObject schema = new(SchemaObjectType.Schema, "dbo", "");

		SchemaDiff diff = SchemaDiff.Compare(new([]), new([schema, Table, Id, Name]));

		Assert.Equal("  extra schema dbo (with 1 table, 2 columns)", diff.ToString());
	}

	[Fact]
	public void Objects_of_one_name_are_numbered()
	{
		SchemaObject check = new(SchemaObjectType.CheckConstraint, "dbo.Person check ([Id]>(0))", "check ([Id]>(0))", "dbo.Person");

		SchemaSnapshot two = new([check, check]);
		SchemaDiff diff = SchemaDiff.Compare(two, new([check]));

		Assert.Equal(["dbo.Person check ([Id]>(0))", "dbo.Person check ([Id]>(0)) #2"], two.Objects.Select(o => o.Name));
		Assert.Equal(["dbo.Person check ([Id]>(0)) #2"], diff.Missing.Select(o => o.Name));
	}

	[Fact]
	public void Long_definition_is_cut()
	{
		SchemaObject procedure = new(SchemaObjectType.Procedure, "dbo.GetPerson", "create procedure dbo.GetPerson as " + new string('x', 300), "dbo");

		string text = SchemaDiff.Compare(new([procedure]), new([])).ToString();

		Assert.StartsWith("  missing procedure dbo.GetPerson: create procedure dbo.GetPerson as xxx", text);
		Assert.EndsWith("...", text);
		Assert.True(text.Length < 200);
	}

	[Fact]
	public void Changed_long_definition_is_shown_around_the_first_difference()
	{
		string body = "create procedure dbo.GetPerson as " + new string('x', 200);
		SchemaObject before = new(SchemaObjectType.Procedure, "dbo.GetPerson", body + " select Id from dbo.Person;", "dbo");
		SchemaObject after = before with { Definition = body + " select Id, Name from dbo.Person;" };

		string text = SchemaDiff.Compare(new([before]), new([after])).ToString();

		Assert.Equal(
			$"""
			  changed procedure dbo.GetPerson
			    was: ...{new string('x', 30)} select Id from dbo.Person;
			    now: ...{new string('x', 30)} select Id, Name from dbo.Person;
			""".ReplaceLineEndings().TrimEnd(),
			text
		);
	}

	[Fact]
	public void Definitions_that_differ_in_whitespace_only()
	{
		string body = "create view dbo.vPerson as select " + new string('x', 200);
		SchemaObject before = new(SchemaObjectType.View, "dbo.vPerson", body + " from dbo.Person", "dbo");
		SchemaObject after = before with { Definition = body + "\n  from dbo.Person" };

		string text = SchemaDiff.Compare(new([before]), new([after])).ToString();

		Assert.EndsWith("the definitions differ in whitespace only", text);
	}
}
