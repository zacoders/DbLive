/* 
	Transaction was disabled for this migration, so there is no guarantee that 
    both indexes will be created. Added 'if exists' to deploy it without errors.
*/

drop index if exists IDX_Orders_OrderDate on dbo.Orders
go

drop index if exists IDX_OrderItmes_ProductId on dbo.OrderItems
go

