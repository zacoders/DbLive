create schema easyflow;
go

create table easyflow.migration (
	version int not null
  , name nvarchar(512) not null
  , created_utc datetime2(7) not null
  , modified_utc datetime2(7) not null

  , constraint pk_easyflow_migration primary key ( version, name )
)
go


create table easyflow.migration_item (
	version int not null
  , name nvarchar(512) not null
  , item_type varchar(32) not null
  , status varchar(32) not null
  , content_hash int not null
  , content nvarchar(max) null
  , created_utc datetime2(7) not null
  , applied_utc datetime2(7) null
  , execution_time_ms int null

  , constraint pk_easyflow_migration_item primary key ( version, name, item_type )
)
go

exec sys.sp_tableoption
	@TableNamePattern = 'easyflow.migration_item'
  , @OptionName = 'large value types out of row'
  , @OptionValue = '1'
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
