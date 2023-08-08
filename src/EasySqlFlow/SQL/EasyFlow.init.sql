
create schema easyflow;
go

create table easyflow.Migrations (
	MigrationId int not null
  
  , MigrationName nvarchar(512) not null
  , MigrationStarted datetime2(7) not null
  , MigrationCompleted datetime2(7) null
  , MigrationSQL nvarchar(max) not null
  , UndoSQL nvarchar(max) null

  , constraint PK_EasyFlow_Migrations primary key ( MigrationId )
)

exec sys.sp_tableoption
	@TableNamePattern = 'easyflow.Migrations'
  , @OptionName = 'large value types out of row'
  , @OptionValue = '1'
go

