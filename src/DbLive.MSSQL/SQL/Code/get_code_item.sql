
create or alter proc easyflow.get_code_item
	@relative_path nvarchar(4000)
as

	set nocount on;

	select relative_path
         , content_hash
         , applied_utc
         , execution_time_ms
         , verified_utc
	from easyflow.code
	where relative_path = @relative_path

go