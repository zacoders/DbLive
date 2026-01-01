
create table dbo.Person
(
	PersonId int identity not null 
	, FirstName nvarchar(128) not null
	, LastName nvarchar(128) not null
	, PhoneNumber varchar(32) null  
	, CreatedUtc datetime2(2) not null
	, ModifiedUtc datetime2(2) not null
	, constraint PK_Person primary key clustered ( PersonId )
)
go

insert into dbo.Person (FirstName, LastName, PhoneNumber, CreatedUtc, ModifiedUtc)
values
	  ( 'John', 'Doe', '+1-202-555-0143', '2026-01-01 10:15:00.00', '2026-01-01 10:15:00.00' )	
	, ( 'Anna', 'Smith', null, '2026-01-01 11:30:45.50', '2026-01-01 11:30:45.50' )
	, ( 'Michael', 'Brown', '+44 7700 900123', '2026-01-01 12:05:10.25', '2026-01-01 12:20:00.00' );