
create table dblive.unit_test_run (
	relative_path nvarchar(4000) not null
  , content_hash int not null
  , run_utc datetime2(7) not null
  , execution_time_ms int not null
  , pass bit not null
  , error nvarchar(4000) null

  , constraint pk_unit_tests_results primary key ( relative_path )
)
go
