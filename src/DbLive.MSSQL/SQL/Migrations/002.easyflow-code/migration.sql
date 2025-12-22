
create table dblive.code (
	relative_path nvarchar(4000) not null
  , content_hash int not null
  , applied_utc datetime2(7) not null
  , execution_time_ms int not null
  , verified_utc datetime2(7) null

  , constraint pk_dblive_code primary key ( relative_path )
)
go
