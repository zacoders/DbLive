set identity_insert dbo.Orders on;

insert into dbo.Orders ( OrderID, CustomerID, OrderDate, TotalAmount, Status )
values
    (1, 101, '2023-08-08', 150.00, 'Pending'),
    (2, 102, '2023-08-09', 300.00, 'Shipped'),
    (3, 103, '2023-08-10', 75.50, 'Completed');

set identity_insert dbo.Orders off;
go

set identity_insert dbo.OrderItems on;

insert into dbo.OrderItems ( OrderItemID, OrderID, ProductID, Quantity, PricePerUnit, TotalPrice )
values
    (1, 1, 201, 2, 50.00, 100.00),
    (2, 1, 202, 3, 25.00, 75.00),
    (3, 2, 203, 1, 150.00, 150.00),
    (4, 2, 204, 2, 75.00, 150.00),
    (5, 3, 201, 1, 50.00, 50.00),
    (6, 3, 205, 4, 5.50, 22.00);

set identity_insert dbo.OrderItems off;
go
