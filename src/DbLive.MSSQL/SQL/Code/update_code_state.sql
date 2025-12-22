
create or alter proc dblive.update_code_state
	@relative_path nvarchar(4000)
  , @verified_utc datetime2(7)
as

	set nocount on;

	update dblive.code
	set verified_utc = @verified_utc
	where relative_path = @relative_path

go