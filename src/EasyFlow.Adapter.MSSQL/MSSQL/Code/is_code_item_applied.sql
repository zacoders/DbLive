
create or alter proc easyflow.is_code_item_applied
	@relative_path nvarchar(4000)
  , @content_md5_hash uniqueidentifier
as

	set nocount on;

	select cast( case when exists ( select *
								    from easyflow.code
								    where relative_path = @relative_path
								      and content_md5_hash = @content_md5_hash )
						  then 1
					  else 0
				 end
			as bit) as is_applied

go