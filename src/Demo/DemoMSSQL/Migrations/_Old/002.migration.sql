
create table dbo.Users (
    UserId int identity
  , Name nvarchar(128) not null
  , constraint PK_Users primary key clustered ( UserId )
)
go

set identity_insert dbo.Users on;
go

insert into dbo.Users ( UserId, Name )
values ( 1, 'Admin' )
go

set identity_insert dbo.Users off;
go

