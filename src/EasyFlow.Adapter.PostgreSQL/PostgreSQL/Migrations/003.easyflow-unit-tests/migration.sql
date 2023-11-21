
create table easyflow.unit_test_run (
	relative_path varchar(4000) not null
  , content_hash int not null
  , run_utc timestamp not null
  , execution_time_ms int not null
  , pass boolean not null
  , error varchar(4000) null

  , constraint pk_unit_tests_results primary key ( relative_path )
)
go
