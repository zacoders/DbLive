create schema easyflow;
go

create table easyflow.Migrations (
	MigrationVersion int not null
  , MigrationName nvarchar(512) not null
  , MigrationStarted datetime2(7) not null
  , MigrationCompleted datetime2(7) null

  , constraint PK_EasyFlow_Migrations primary key ( MigrationVersion, MigrationName )
)
go

create table easyflow.Version (
	Version int not null
  , MigrationDatetime datetime2(7)
  , OneRowLock as 1 
  , constraint PK_EasyFlow_Version primary key ( OneRowLock )
)
go

insert into easyflow.Version ( Version ) values ( 0 );
go
