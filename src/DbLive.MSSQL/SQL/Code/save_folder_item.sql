
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
