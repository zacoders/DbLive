
create table easyflow.code (
	relative_path nvarchar(4000) not null
  , content_md5_hash uniqueidentifier not null
  , applied_utc datetime2(7) not null
  , execution_time_ms int not null
  , verified_utc datetime2(7) null

  , constraint pk_easyflow_code primary key ( relative_path )
)
go
