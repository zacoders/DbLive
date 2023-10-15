
create or alter proc easyflow.IsCodeItemApplied
	@RelativePath nvarchar(512)
  , @ContentMD5Hash uniqueidentifier
as

	set nocount on;

	select cast( case when exists ( select *
								    from easyflow.Code
								    where RelativePath = @RelativePath
								      and ContentMD5Hash = @ContentMD5Hash )
						  then 1
					  else 0
				 end
			as bit) as IsApplied

go