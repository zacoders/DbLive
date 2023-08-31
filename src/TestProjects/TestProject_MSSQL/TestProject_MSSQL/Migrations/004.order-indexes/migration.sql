
create index IDX_Orders_OrderDate 
on dbo.Orders ( OrderDate )
with ( online = on, data_compression = row )
go


create index IDX_OrderItmes_ProductId
on dbo.OrderItems ( ProductId )
include ( Quantity, PricePerUnit, TotalPrice )
with ( online = on, data_compression = row )
go
