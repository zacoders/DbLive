create schema if not exists easyflow;

create table easyflow.Migrations (
	MigrationVersion int not null
  , MigrationName varchar(512) not null
  , MigrationStarted timestamp not null
  , MigrationCompleted timestamp null

  , constraint PK_EasyFlow_Migrations primary key ( MigrationVersion, MigrationName )
);
