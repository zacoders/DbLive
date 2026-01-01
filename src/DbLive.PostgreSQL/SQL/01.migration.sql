create schema if not exists dblive;


create table dblive.migration (
	version int not null
  , name varchar(512) not null
  , created_utc timestamp not null
  , modified_utc timestamp not null

  , constraint pk_dblive_migration primary key ( version, name )
)


create table dblive.migration (
	version int not null
  , name varchar(512) not null
  , item_type varchar(32) not null
  , status varchar(32) not null
  , content_hash int not null
  , created_utc timestamp not null
  , applied_utc timestamp null
  , execution_time_ms int null

  , constraint pk_dblive_migration primary key ( version, name, item_type )
)


create table dblive.version (
	version int not null
  , created_utc timestamp not null
  , applied_utc timestamp null
  , one_row_lock as 1 
  , constraint pk_dblive_version primary key ( one_row_lock )
)


insert into dblive.version ( version, created_utc ) values ( 0, sysutcdatetime() );
go
