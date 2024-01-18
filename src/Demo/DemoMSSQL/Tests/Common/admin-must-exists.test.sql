

if not exists ( select * from dbo.Users where Name = 'Admin' )
	throw 50001, 'Admin user must exists.', 0;

