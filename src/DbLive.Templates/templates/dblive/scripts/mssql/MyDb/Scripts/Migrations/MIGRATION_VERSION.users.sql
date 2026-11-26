-- A migration runs once per database. Do not change it after it was deployed: add a new migration instead.
create table dbo.Users
(
    UserId int not null identity(1, 1),
    Name nvarchar(100) not null,
    constraint PK_Users primary key ( UserId ),
    constraint CK_Users_Name check ( Name <> N'' )
);
