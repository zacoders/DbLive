
create or alter proc easyflow.insert_code_state
	@relative_path nvarchar(4000)
  , @content_hash int
  , @applied_utc datetime2(7)
  , @execution_time_ms int
as

	set nocount on;

	insert into easyflow.code (
		relative_path
	  , content_hash
	  , applied_utc
	  , execution_time_ms
	  , verified_utc
	)
	values (
		@relative_path
	  , @content_hash
	  , @applied_utc
	  , @execution_time_ms
	  , @applied_utc
	)

go