using DbLive.Adapter;
using DbLive.Schema;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SchemaSnapshotTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	private const string Schema = """
		create schema app;
		go
		create type app.Code from nvarchar(20) not null;
		create type app.IdList as table (Id int not null primary key, Name nvarchar(10) null);
		create sequence app.Seq as bigint start with 10 increment by 5;
		create table dbo.Dept (Id int identity(1, 1) primary key, Name nvarchar(50) not null unique);
		go
		create table app.Person (
			Id int identity(100, 2) not null constraint PK_Person primary key clustered,
			Name nvarchar(50) not null,
			Phone varchar(max) null default (''),
			Code app.Code,
			Price decimal(10, 2) not null constraint DF_Person_Price default ((0)),
			Total as (Price * 2) persisted,
			DeptId int null references dbo.Dept (Id) on delete cascade,
			Age int null check (Age > 0),
			Email nvarchar(100) collate Latin1_General_CS_AS null,
			constraint CK_Person_Name check (len(Name) > 0),
			constraint UQ_Person_Email unique (Email desc)
		);
		create index IX_Person_Name on app.Person (Name asc, Age desc) include (Phone, Code) where (Age is not null);
		go
		create view app.vPerson with schemabinding as select Id, Name from app.Person;
		go
		create unique clustered index IX_vPerson on app.vPerson (Id);
		go
		create or alter procedure app.GetPerson @Id int
		as
			select Id, Name from app.Person where Id = @Id;
		go
		create function app.Twice (@x int) returns int as begin return @x * 2 end;
		go
		create trigger app.TR_Person on app.Person after insert as set nocount on;
		go
		disable trigger app.TR_Person on app.Person;
		go
		create synonym app.P for app.Person;
		""";

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	private IDbLiveDA _da = null!;

	public async Task InitializeAsync()
	{
		_da = new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).CreateDbLiveDA();
		await _da.CreateDBAsync();
	}

	public Task DisposeAsync() => _da.DropDbAsync();

	[Fact]
	public async Task Snapshot_describes_each_kind_of_object()
	{
		await _da.ExecuteNonQueryAsync(Schema);

		SchemaSnapshot snapshot = await _da.GetSchemaSnapshotAsync();

		_output.WriteLine(string.Join(Environment.NewLine, snapshot.Objects.Select(o => $"{o.Type} {o.Name}: {o.Definition}")));

		Assert.Equal("", Definition(snapshot, SchemaObjectType.Schema, "app"));
		Assert.Equal("", Definition(snapshot, SchemaObjectType.Table, "app.Person"));
		Assert.Equal("int identity(100, 2) not null", Definition(snapshot, SchemaObjectType.Column, "app.Person.Id"));
		Assert.Equal("varchar(max) collate SQL_Latin1_General_CP1_CI_AS null default ('')", Definition(snapshot, SchemaObjectType.Column, "app.Person.Phone"));
		Assert.Equal("app.Code collate SQL_Latin1_General_CP1_CI_AS not null", Definition(snapshot, SchemaObjectType.Column, "app.Person.Code"));
		Assert.Equal("decimal(10, 2) not null constraint DF_Person_Price default ((0))", Definition(snapshot, SchemaObjectType.Column, "app.Person.Price"));
		Assert.Equal("as ([Price]*(2)) persisted null", Definition(snapshot, SchemaObjectType.Column, "app.Person.Total"));
		Assert.Equal("nvarchar(100) collate Latin1_General_CS_AS null", Definition(snapshot, SchemaObjectType.Column, "app.Person.Email"));
		Assert.Equal("constraint PK_Person clustered (Id asc)", Definition(snapshot, SchemaObjectType.PrimaryKey, "app.Person"));
		Assert.Equal("clustered (Id asc)", Definition(snapshot, SchemaObjectType.PrimaryKey, "dbo.Dept"));
		Assert.Equal("nonclustered (Email desc)", Definition(snapshot, SchemaObjectType.UniqueConstraint, "app.Person.UQ_Person_Email"));
		Assert.Equal("nonclustered (Name asc)", Definition(snapshot, SchemaObjectType.UniqueConstraint, "dbo.Dept (Name asc)"));
		Assert.Equal("(DeptId) references dbo.Dept (Id) on delete cascade", Definition(snapshot, SchemaObjectType.ForeignKey, "app.Person (DeptId) references dbo.Dept"));
		Assert.Equal("check ([Age]>(0))", Definition(snapshot, SchemaObjectType.CheckConstraint, "app.Person check ([Age]>(0))"));
		Assert.Equal("check (len([Name])>(0))", Definition(snapshot, SchemaObjectType.CheckConstraint, "app.Person.CK_Person_Name"));
		Assert.Equal("nonclustered (Name asc, Age desc) include (Code, Phone) where ([Age] IS NOT NULL)", Definition(snapshot, SchemaObjectType.Index, "app.Person.IX_Person_Name"));
		Assert.Equal("unique clustered (Id asc)", Definition(snapshot, SchemaObjectType.Index, "app.vPerson.IX_vPerson"));
		Assert.Equal("create view app.vPerson with schemabinding as select Id, Name from app.Person;", Definition(snapshot, SchemaObjectType.View, "app.vPerson"));
		Assert.Equal("create procedure app.GetPerson @Id int as select Id, Name from app.Person where Id = @Id;", Definition(snapshot, SchemaObjectType.Procedure, "app.GetPerson"));
		Assert.Equal("create function app.Twice (@x int) returns int as begin return @x * 2 end;", Definition(snapshot, SchemaObjectType.Function, "app.Twice"));
		Assert.Equal("create trigger app.TR_Person on app.Person after insert as set nocount on; disabled", Definition(snapshot, SchemaObjectType.Trigger, "app.TR_Person"));
		Assert.Equal("nvarchar(20) not null", Definition(snapshot, SchemaObjectType.Type, "app.Code"));
		Assert.Equal("table (Id int not null, Name nvarchar(10) collate SQL_Latin1_General_CP1_CI_AS null, primary key clustered (Id asc))", Definition(snapshot, SchemaObjectType.Type, "app.IdList"));
		Assert.Equal("bigint start with 10 increment by 5 minvalue -9223372036854775808 maxvalue 9223372036854775807 no cycle cache", Definition(snapshot, SchemaObjectType.Sequence, "app.Seq"));
		Assert.Equal("[app].[Person]", Definition(snapshot, SchemaObjectType.Synonym, "app.P"));

		Assert.Equal("app.Person", snapshot.Find(SchemaObjectType.Column, "app.Person.Id")!.Parent);
		Assert.Equal("app.Person", snapshot.Find(SchemaObjectType.Trigger, "app.TR_Person")!.Parent);
		Assert.Equal("app", snapshot.Find(SchemaObjectType.Table, "app.Person")!.Parent);
	}

	[Fact]
	public async Task Names_given_by_the_server_and_column_order_are_ignored()
	{
		await _da.ExecuteNonQueryAsync(Schema);
		SchemaSnapshot before = await _da.GetSchemaSnapshotAsync();

		// the column comes back at the end of the table; its default, the check and the foreign key get new names from the server.
		await _da.ExecuteNonQueryAsync("""
			declare @sql nvarchar(max);
			select @sql = concat('alter table app.Person drop constraint ', name) from sys.default_constraints where parent_object_id = object_id('app.Person') and is_system_named = 1;
			exec (@sql);
			drop index IX_Person_Name on app.Person;
			alter table app.Person drop column Phone;
			alter table app.Person add Phone varchar(max) null default ('');
			create index IX_Person_Name on app.Person (Name asc, Age desc) include (Phone, Code) where (Age is not null);

			select @sql = concat('alter table app.Person drop constraint ', name) from sys.check_constraints where parent_object_id = object_id('app.Person') and is_system_named = 1;
			exec (@sql);
			alter table app.Person add check (Age > 0);

			select @sql = concat('alter table app.Person drop constraint ', name) from sys.foreign_keys where parent_object_id = object_id('app.Person');
			exec (@sql);
			alter table app.Person add foreign key (DeptId) references dbo.Dept (Id) on delete cascade;
			""");

		AssertSame(before, await _da.GetSchemaSnapshotAsync());
	}

	[Fact]
	public async Task Module_text_compares_without_create_or_alter_and_whitespace()
	{
		await _da.ExecuteNonQueryAsync(Schema);
		SchemaSnapshot before = await _da.GetSchemaSnapshotAsync();

		// SQL Server stores "alter" as "CREATE", "create or alter" as "create".
		await _da.ExecuteNonQueryAsync("""
			alter procedure app.GetPerson @Id int as
			    select Id, Name
			    from app.Person where Id = @Id;
			go
			drop function app.Twice;
			go
			CREATE OR ALTER function app.Twice (@x int) returns int as begin return @x * 2 end;
			""");

		SchemaSnapshot after = await _da.GetSchemaSnapshotAsync();

		AssertSame(before, after);
	}

	[Fact]
	public async Task Current_identity_and_sequence_values_and_dblive_schema_are_ignored()
	{
		await _da.ExecuteNonQueryAsync(Schema);
		SchemaSnapshot before = await _da.GetSchemaSnapshotAsync();

		await _da.ExecuteNonQueryAsync("""
			insert into dbo.Dept (Name) values ('a'), ('b');
			select next value for app.Seq;
			create statistics ST_Person_Age on app.Person (Age);
			go
			create schema dblive;
			go
			create table dblive.t (id int);
			""");

		AssertSame(before, await _da.GetSchemaSnapshotAsync());
	}

	[Fact]
	public async Task Changed_objects_are_found()
	{
		await _da.ExecuteNonQueryAsync(Schema);
		SchemaSnapshot before = await _da.GetSchemaSnapshotAsync();

		await _da.ExecuteNonQueryAsync("""
			alter table app.Person drop constraint CK_Person_Name;
			alter table app.Person drop constraint UQ_Person_Email;
			alter table app.Person drop column Email;
			create index IX_Person_Age on app.Person (Age);
			alter sequence app.Seq increment by 10;
			""");

		SchemaDiff diff = SchemaDiff.Compare(before, await _da.GetSchemaSnapshotAsync());

		Assert.Equal(
			"""
			  missing check constraint app.Person.CK_Person_Name: check (len([Name])>(0))
			  missing column app.Person.Email: nvarchar(100) collate Latin1_General_CS_AS null
			  missing unique constraint app.Person.UQ_Person_Email: nonclustered (Email desc)
			  extra index app.Person.IX_Person_Age: nonclustered (Age asc)
			  changed sequence app.Seq
			    was: bigint start with 10 increment by 5 minvalue -9223372036854775808 maxvalue 9223372036854775807 no cycle cache
			    now: bigint start with 10 increment by 10 minvalue -9223372036854775808 maxvalue 9223372036854775807 no cycle cache
			""".ReplaceLineEndings().TrimEnd(),
			diff.ToString()
		);
	}

	private static string Definition(SchemaSnapshot snapshot, SchemaObjectType type, string name)
		=> (snapshot.Find(type, name) ?? throw new Exception($"{type} {name} is not in the snapshot.")).Definition;

	private static void AssertSame(SchemaSnapshot before, SchemaSnapshot after)
	{
		SchemaDiff diff = SchemaDiff.Compare(before, after);
		Assert.True(diff.IsEmpty, diff.ToString());
	}
}
