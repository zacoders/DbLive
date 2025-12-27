
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