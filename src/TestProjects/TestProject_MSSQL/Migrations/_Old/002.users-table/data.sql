
set identity_insert dbo.Users on;
go

insert into dbo.Users ( UserId, Name )
values ( 1, 'Admin' )
go

set identity_insert dbo.Users off;
go

