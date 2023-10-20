
create or alter proc easyflow.is_code_item_applied
	@relative_path nvarchar(4000)
  , @content_hash int
as

	set nocount on;

	select cast( case when exists ( select *
								    from easyflow.code
								    where relative_path = @relative_path
								      and content_hash = @content_hash )
						  then 1
					  else 0
				 end
			as bit) as is_applied

go