
create or replace proc easyflow.get_code_item (
	@relative_path nvarchar(4000)
)
language plpgsql
as $$
begin

	select relative_path
         , content_hash
         , applied_utc
         , execution_time_ms
         , verified_utc
	from easyflow.code
	where relative_path = @relative_path

end 
$$