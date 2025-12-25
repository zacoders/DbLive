
-- Arrange


-- Act
select dbo.GetFullName('first', 'last')


-- Assert
select assert = 'rows-with-schema'
select cast('first last' as nvarchar(255))