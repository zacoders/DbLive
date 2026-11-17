/* 
	* new table dbo.PersonPhones
	* and data migration trigger
	* data migration to a new table 
*/

create table dbo.PersonPhones
(
	PersonPhoneId int identity not null
  , PersonId int not null
  , PhoneNumber varchar(32) null 
  , IsPrimary bit not null constraint DEF_PersonPhones_IsPrimary default 0
  , CreatedUtc datetime2(2) not null
  , ModifiedUtc datetime2(2) not null
  , constraint PK_PersonPhones primary key clustered ( PersonPhoneId )
)
go

create trigger dbo.TMP_DataMigration_Person_To_PersonPhones 
on dbo.Person
after insert, update
as 
	set nocount on;

	-- only the primary phone is synced: Person.PhoneNumber knows nothing about
	-- other phones, they are added by the new application and must not be touched.
	with cteSource
	as (
		select *
		from Inserted
		where PhoneNumber is not null
	),
	cteTarget
	as (
		select pp.*
		from dbo.PersonPhones pp
		where pp.IsPrimary = 1
		  and pp.PersonId in ( select PersonId from Inserted )
	)
	merge into cteTarget as t
	using cteSource s on s.PersonId = t.PersonId
	when matched then
		update set
			PhoneNumber = s.PhoneNumber
		  , ModifiedUtc = sysutcdatetime()
	when not matched then
		insert ( PersonId, PhoneNumber, IsPrimary, CreatedUtc, ModifiedUtc )
		values ( s.PersonId, s.PhoneNumber, 1, sysutcdatetime(), sysutcdatetime() )
	when not matched by source then
		-- the phone number was removed from dbo.Person
		delete ;
	
go

-- initial data migration, emulating update to force trigger run
update dbo.Person
set ModifiedUtc = ModifiedUtc
go