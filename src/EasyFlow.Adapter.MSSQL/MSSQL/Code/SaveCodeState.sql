
create or alter proc easyflow.SaveCodeState
	@RelativePath nvarchar(512)
  , @ContentMD5Hash uniqueidentifier
  , @MigrationStartedUtc datetime2(7)
  , @MigrationCompletedUtc datetime2(7)
as

	set nocount on;

	merge into easyflow.Code as t
	using ( select 1 c ) s on @RelativePath = t.RelativePath
	when not matched then 
		insert (
			RelativePath
		  , ContentMD5Hash
		  , MigrationStarted
		  , MigrationCompleted
		  , FirstTimeCreated
		)
		values (
			@RelativePath
		  , @ContentMD5Hash
		  , @MigrationStartedUtc
		  , @MigrationCompletedUtc
		  , @MigrationCompletedUtc
		)
	when matched then 
	update 
		set ContentMD5Hash = @ContentMD5Hash
		  , MigrationStarted = @MigrationStartedUtc
		  , MigrationCompleted = @MigrationCompletedUtc;

go