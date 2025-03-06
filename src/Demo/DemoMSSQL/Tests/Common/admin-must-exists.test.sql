

declare @cnt int = (
	select count(*) 
	from dbo.Users 
	where Name = 'Admin'
)


if @cnt != 1 throw 50000, 'Expected one admin user.', 0
