/* 
	Breaking Migration: 
		* Removing PhoneNumber column from Person table
		* Dropping temporary data migration trigger
	
*/

drop trigger dbo.TMP_DataMigration_Person_To_PersonPhones
go

alter table dbo.Person
drop column if exists PhoneNumber
go

