create schema easyflow;
go

create table easyflow.migrations (
	version int not null
  , name nvarchar(512) not null
  , created_utc datetime2(7) not null
  , modified_utc datetime2(7) not null
  , execution_time_ms int not null

  , constraint pk_easyflow_migrations primary key ( version, name )
)
go


create table easyflow.migration_items (
	version int not null
  , name nvarchar(512) not null
  , item_type varchar(32) not null
  , status varchar(32) not null
  , content_md5_hash uniqueidentifier not null
  , created_utc datetime2(7) not null
  , applied_utc datetime2(7) null
  , execution_time_ms int null

  , constraint pk_easyflow_migration_items primary key ( version, name, item_type )
)
go


create table easyflow.version (
	version int not null
  , created_utc datetime2(7) not null
  , applied_utc datetime2(7) null
  , one_row_lock as 1 
  , constraint pk_easyflow_version primary key ( one_row_lock )
)
go

insert into easyflow.version ( version, created_utc ) values ( 0, sysutcdatetime() );
go
