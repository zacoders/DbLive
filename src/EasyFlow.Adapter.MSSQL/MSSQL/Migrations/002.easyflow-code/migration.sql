
create table easyflow.Code (
	RelativePath nvarchar(512) not null
  , ContentMD5Hash uniqueidentifier not null
  , FirstTimeCreated datetime2(7) not null
  , MigrationStarted datetime2(7) not null
  , MigrationCompleted datetime2(7) null

  , constraint PK_EasyFlow_Code primary key ( RelativePath )
)
go
