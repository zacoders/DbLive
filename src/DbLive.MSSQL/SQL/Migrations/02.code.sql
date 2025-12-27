
create table dblive.code (
	relative_path nvarchar(4000) not null
  , status varchar(32) not null
  , execution_time_ms int not null
  , applied_utc datetime2(7) null
  , created_utc datetime2(7) not null  
  , verified_utc datetime2(7) null
  , error_message nvarchar(max) null
  , content_hash int not null

  , constraint pk_dblive_code primary key ( relative_path )
)
go

exec sys.sp_tableoption
	@TableNamePattern = 'dblive.code'
  , @OptionName = 'large value types out of row'
  , @OptionValue = '1'
go
