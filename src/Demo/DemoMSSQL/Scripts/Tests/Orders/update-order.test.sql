-- Arrange
-- Note: Test data was prepared in setup.test.sql file.


-- Act

update dbo.Orders
set Status = 'Shipped'
	output inserted.OrderID
where OrderID = 1


-- Assert
select assert = 'row-count=1'

