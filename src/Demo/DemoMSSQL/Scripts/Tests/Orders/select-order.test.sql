-- Arrange
-- Note: Test data was prepared in init.sql file.


-- Act
select OrderId from dbo.Orders


-- Assert
select assert = 'row-count=3'



