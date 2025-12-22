
create or alter proc dblive.save_migration
	@version int
  , @name nvarchar(512)  
  , @created_utc datetime2(7)
  , @modified_utc datetime2(7)
as

	set nocount on;

	merge into dblive.migration as t
	using ( select 1 ) s(c) on t.version = @version and t.name = @name
	when matched then 
		update set modified_utc = @modified_utc
	when not matched then 
		insert (
			  version
			, name
			, created_utc
			, modified_utc
		)
		values (
			  @version
			, @name
			, @created_utc
			, @modified_utc
		);

go