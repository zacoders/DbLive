-- Arrange
-- Note: Test data was prepared in init.sql file.


-- Act

update dbo.Orders
set OrderStatus = 'Shipped'
	output inserted.OrderID
where OrderID = 1


-- Assert
select asstert = 'row-count=1'

