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
			join Inserted i on i.PersonId = pp.PersonId
	)
	merge into cteTarget as t
	using Inserted s on s.PersonId = t.PersonId and t.IsPrimary = 1
	when matched then 
		update set 
			PhoneNumber = s.PhoneNumber
		  , ModifiedUtc = sysutcdatetime()
	when not matched then 
		insert ( PersonId, PhoneNumber, IsPrimary, CreatedUtc, ModifiedUtc )
		values ( PersonId, PhoneNumber, 1, sysutcdatetime(), sysutcdatetime() )
	when not matched by source then
		delete ;
	
go

-- initial data migration, emulating update to force trigger run
update dbo.Person
set ModifiedUtc = ModifiedUtc
go