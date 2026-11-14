/*
	Runs without a transaction (004.migration.settings.json), so if the second index fails,
	the first one stays and the next deployment runs the whole script again.
	Each index is created only if it does not exist yet, so the script is re-runnable.
*/

if not exists ( select *
				from sys.indexes
				where object_id = object_id('dbo.Orders')
				  and name = 'IDX_Orders_OrderDate' )
begin
	create index IDX_Orders_OrderDate
	on dbo.Orders ( OrderDate )
	with ( online = on, data_compression = row )
end
go


if not exists ( select *
				from sys.indexes
				where object_id = object_id('dbo.OrderItems')
				  and name = 'IDX_OrderItmes_ProductId' )
begin
	create index IDX_OrderItmes_ProductId
	on dbo.OrderItems ( ProductId )
	include ( Quantity, PricePerUnit, TotalPrice )
	with ( online = on, data_compression = row )
end
go
