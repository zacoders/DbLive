/* 
	Rollback should support both states of the database
	  1. Breaking changes script was applied 
	  2. Breaking changes script was not applied yet
	So we should use if object exists checks
*/

set xact_abort on
begin tran

	if not exists ( select *
					from sys.columns                       
					where object_id = object_id('dbo.Person')
					  and name = 'PhoneNumber' )
	begin
		-- reverting column back if it was deleted by breaking changes script
		alter table dbo.Person
		add PhoneNumber varchar(32) null 

		-- reverting data back
		-- dynamics sql used to avoid 'Invalid column name PhoneNumber' error.
		exec('
			update p
			set PhoneNumber = pp.PhoneNumber
			  , ModifiedUtc = sysutcdatetime()
			from dbo.Person p 
				join dbo.PersonPhones pp on pp.PersonId = p.PersonId
										and pp.IsPrimary = 1
		')
	end

	-- dropping new objects
	drop table if exists dbo.PersonPhones
	drop trigger if exists dbo.TMP_DataMigration_Person_To_PersonPhones

commit
go