create schema easyflow;
go

--TODO: I think it will be good to keep MigrationTasks separatelly.
create table easyflow.Migrations (
	MigrationVersion int not null
  , MigrationName nvarchar(512) not null
  , MigrationStarted datetime2(7) not null
  , MigrationCompleted datetime2(7) null

  , constraint PK_EasyFlow_Migrations primary key ( MigrationVersion, MigrationName )
)
go
