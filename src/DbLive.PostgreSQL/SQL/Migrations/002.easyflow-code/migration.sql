
create table dblive.code (
	relative_path varchar(4000) not null
  , content_hash int not null
  , applied_utc timestamp not null
  , execution_time_ms int not null
  , verified_utc timestamp null

  , constraint pk_dblive_code primary key ( relative_path )
)
go
