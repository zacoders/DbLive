
create table dbo.Orders
(
    OrderId int identity
  , CustomerId int
  , OrderDate date
  , TotalAmount decimal(10, 2)
  , Status varchar(50)
  , constraint PK_Orders primary key clustered ( OrderId )
)
