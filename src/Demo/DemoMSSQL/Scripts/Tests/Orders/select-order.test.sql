-- Arrange
-- Note: Test data was prepared in init.sql file.


-- Act
select count(*) from dbo.Orders


-- Assert
select asstert = 'row-count=3'



