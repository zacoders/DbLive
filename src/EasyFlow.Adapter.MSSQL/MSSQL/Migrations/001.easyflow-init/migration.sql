create schema easyflow;
go

create table easyflow.Migrations (
	Domain nvarchar(128) not null constraint DEF_EasyFlow_Migrations_Domain default ('default')
  , MigrationVersion int not null
  , MigrationName nvarchar(512) not null
  , MigrationStarted datetime2(7) not null
  , MigrationCompleted datetime2(7) null

  , constraint PK_EasyFlow_Migrations primary key ( Domain, MigrationVersion, MigrationName )
)
go
