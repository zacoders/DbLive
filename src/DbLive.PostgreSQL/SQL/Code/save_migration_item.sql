
create or alter proc easyflow.save_migration_item
	@version int
  , @name nvarchar(512)
  , @item_type varchar(32)
  , @status varchar(32)
  , @content_hash int
  , @created_utc datetime2(7)
  , @applied_utc datetime2(7)
  , @execution_time_ms int
as

	set nocount on;

	merge into easyflow.migration_item as t
	using ( select 1 ) s(c) on t.version = @version and t.name = @name and t.item_type = @item_type
	when matched then update 
		set status = @status
		  , content_hash = @content_hash
		  , applied_utc = @applied_utc
		  , execution_time_ms = @execution_time_ms
	when not matched then 
		insert (
			version
		  , name
		  , item_type
		  , status
		  , content_hash
		  , created_utc
		  , applied_utc
		  , execution_time_ms
		)
		values (
			@version
		  , @name
		  , @item_type
		  , @status
		  , @content_hash
		  , @created_utc
		  , @applied_utc
		  , @execution_time_ms
		);

go