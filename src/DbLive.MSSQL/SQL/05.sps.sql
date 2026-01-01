
create or alter proc dblive.save_code_item
	@relative_path nvarchar(4000)
  , @status varchar(32)
  , @content_hash int
  , @applied_utc datetime2(7)
  , @execution_time_ms int
  , @created_utc datetime2(7)
  , @error_message nvarchar(max)
as

	set nocount on;

	merge into dblive.code as t
	using ( select 1 ) s(c) on t.relative_path = @relative_path
	when matched then update 
		set status = @status
		  , content_hash = @content_hash
		  , applied_utc = @applied_utc
		  , execution_time_ms = @execution_time_ms
		  , error_message = @error_message
	when not matched then 
		insert (
			relative_path
		  , status
		  , content_hash
		  , applied_utc
		  , execution_time_ms
		  , created_utc
		  , error_message
		)
		values (
			@relative_path
		  , @status
		  , @content_hash
		  , @applied_utc
		  , @execution_time_ms
		  , @created_utc
		  , @error_message
		);
	
go


create or alter proc dblive.save_folder_item
	@folder_type varchar(32)
  , @relative_path nvarchar(4000)
  , @started_utc datetime2(7)
  , @completed_utc datetime2(7)
  , @execution_time_ms int
as
	set nocount on;

	merge into dblive.folder_items as t
	using ( select 1 ) s(c) on t.folder_type = @folder_type and t.relative_path = @relative_path
	when matched then 
		update set 
			started_utc = @started_utc,
			completed_utc = @completed_utc,
			execution_time_ms = @execution_time_ms
	when not matched then 
		insert (
			  folder_type
			, relative_path
			, created_utc
			, started_utc
			, completed_utc
			, execution_time_ms
		)
		values (
			  @folder_type
			, @relative_path
			, sysutcdatetime()
			, @started_utc
			, @completed_utc
			, @execution_time_ms
		);

go


create or alter proc dblive.save_migration
	@version int
  , @name nvarchar(512)
  , @item_type varchar(32)
  , @status varchar(32)
  , @content_hash int
  , @content nvarchar(max)
  , @created_utc datetime2(7)
  , @applied_utc datetime2(7)
  , @execution_time_ms int
as

	set nocount on;

	merge into dblive.migration as t
	using ( select 1 ) s(c) on t.version = @version and t.item_type = @item_type
	when matched then update 
		set status = @status
		  , content_hash = @content_hash
		  , content = @content
		  , applied_utc = @applied_utc
		  , execution_time_ms = @execution_time_ms
	when not matched then 
		insert (
			version
		  , name
		  , item_type
		  , status
		  , content_hash
		  , content
		  , created_utc
		  , applied_utc
		  , execution_time_ms
		)
		values (
			@version
		  , @name
		  , @item_type
		  , @status
		  , @content_hash
		  , @content
		  , @created_utc
		  , @applied_utc
		  , @execution_time_ms
		);

go


create or alter proc dblive.save_unit_test_result
	@relative_path nvarchar(4000)
  , @content_hash int
  , @run_utc datetime2(7)
  , @execution_time_ms int
  , @pass bit
  , @error nvarchar(4000)
as

	set nocount on;

	merge into dblive.unit_test_run as t
	using ( select 1 ) s(c) on t.relative_path = @relative_path
	when matched then update 
		set content_hash = @content_hash
		  , run_utc = @run_utc
		  , execution_time_ms = @execution_time_ms
		  , pass = @pass
		  , error = @error
	when not matched then 
		insert (
			relative_path
		  , content_hash
		  , run_utc
		  , execution_time_ms
		  , pass
		  , error
		)
		values (
			@relative_path
		  , @content_hash
		  , @run_utc
		  , @execution_time_ms
		  , @pass
		  , @error
		);

go


create or alter proc dblive.update_code_state
	@relative_path nvarchar(4000)
  , @verified_utc datetime2(7)
as

	set nocount on;

	update dblive.code
	set verified_utc = @verified_utc
	where relative_path = @relative_path

go

