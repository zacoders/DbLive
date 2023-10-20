create schema if not exists easyflow;

create table easyflow.Migration (
	MigrationVersion int not null
  , MigrationName varchar(512) not null
  , MigrationStarted timestamp not null
  , MigrationCompleted timestamp null

  , constraint PK_EasyFlow_migration primary key ( MigrationVersion, MigrationName )
);
