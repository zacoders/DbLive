
create table dbo.Orders
(
    OrderId int identity
  , CustomerId int
  , OrderDate date
  , TotalAmount decimal(10, 2)
  , Status varchar(50)
  , constraint PK_Orders primary key clustered ( OrderId )
)
go

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
