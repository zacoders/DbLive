
create table easyflow.folder_items (
	folder_type varchar(32) not null
  , relative_path nvarchar(4000) not null
  
  , created_utc datetime2(7) not null
  , started_utc datetime2(7) not null
  , completed_utc datetime2(7) not null
  , execution_time_ms int not null

  , constraint pk_easyflow_folders primary key ( folder_type, relative_path )
)
go
