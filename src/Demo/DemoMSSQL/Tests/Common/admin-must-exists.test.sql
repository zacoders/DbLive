
-- Arrange


-- Act
declare @cnt int = (
	select count(*) 
	from dbo.Users 
	where Name = 'Admin'
)

-- Assert
-- using throw to indicate test failure
if @cnt != 1 throw 50000, 'Expected one admin user.', 0
