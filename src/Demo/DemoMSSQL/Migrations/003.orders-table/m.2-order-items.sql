
create table dbo.OrderItems
(
    OrderItemId int identity
  , OrderId int
  , ProductId int
  , Quantity int
  , PricePerUnit decimal(10, 2)
  , TotalPrice decimal(10, 2)

  , constraint PK_OrderItems primary key nonclustered ( OrderItemId ) with ( data_compression = row )

  , constraint FK_OrderItems 
        foreign key ( OrderId )
        references dbo.Orders ( OrderId )
  
  , index CI_OrderItems unique clustered ( OrderId, OrderItemId ) with ( data_compression = row )
)
