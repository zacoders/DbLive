


insert into Production.UnitMeasure ( UnitMeasureCode, Name, ModifiedDate)
values ( 'CM', 'CM', sysutcdatetime() )
     , ( 'KG', 'KG', sysutcdatetime() )
go


set identity_insert Production.ProductModel on

insert into Production.ProductModel ( ProductModelID, Name, CatalogDescription, Instructions, rowguid, ModifiedDate )
values ( 1, 'Bike', null, null, newid(), sysutcdatetime() )

set identity_insert Production.ProductModel off
go

set identity_insert Production.ProductCategory on
go
insert into Production.ProductCategory ( ProductCategoryID, Name, rowguid, ModifiedDate )
values ( 2, 'Category 2', newid(), sysutcdatetime() ) 

set identity_insert Production.ProductCategory off
go

set identity_insert Production.ProductSubcategory on 

insert into Production.ProductSubcategory ( ProductSubcategoryID, ProductCategoryID, Name, rowguid, ModifiedDate )
values ( 11, 2, 'Sub category 2-11', newid(), sysutcdatetime() )

set identity_insert Production.ProductSubcategory off

go


set identity_insert Production.Product on

insert into Production.Product
(
	ProductID
  , Name
  , ProductNumber
  , MakeFlag
  , FinishedGoodsFlag
  , Color
  , SafetyStockLevel
  , ReorderPoint
  , StandardCost
  , ListPrice
  , Size
  , SizeUnitMeasureCode
  , WeightUnitMeasureCode
  , Weight
  , DaysToManufacture
  , ProductLine
  , Class
  , Style
  , ProductSubcategoryID
  , ProductModelID
  , SellStartDate
)
values
  ( 1, 'Road Bike Pro', 'RB-PRO-001', 1, 1, 'Red', 100, 50, 800.00, 1200.00, 'L', 'CM', 'KG', 9.50, 2, 'R', 'H', 'U', 11, 1, '2022-01-01' )
, ( 2, 'Mountain Bike', 'MB-STD-002', 1, 1, 'Black', 150, 75, 600.00, 950.00, 'M', 'CM', 'KG', 11.20, 3, 'M', 'M', 'U', 11, 1, '2022-06-01' )
, ( 3, 'Touring Bike', 'TB-CLS-003', 1, 1, 'Blue', 80, 40, 700.00, 1100.00, 'L', 'CM', 'KG', 10.80, 4, 'T', 'L', 'U', 11, 1, '2023-01-15' );

set identity_insert Production.Product off

insert into production.productlistpricehistory (
      productid
    , startdate
    , enddate
    , listprice
)
values
-- Road Bike Pro history
(1, '2022-01-01', '2022-12-31 23:59:59.997', 1100.00),
(1, '2023-01-01', '2023-12-31 23:59:59.997', 1150.00),
(1, '2024-01-01', null,          1200.00),

-- Mountain Bike history
(2, '2022-06-01', '2023-05-31 23:59:59.997', 900.00),
(2, '2023-06-01', '2024-05-31 23:59:59.997', 925.00),
(2, '2024-06-01', null,         950.00),

-- Touring Bike history
(3, '2023-01-15', '2023-12-31 23:59:59.997', 1050.00),
(3, '2024-01-01', null,         1100.00);
