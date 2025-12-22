
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