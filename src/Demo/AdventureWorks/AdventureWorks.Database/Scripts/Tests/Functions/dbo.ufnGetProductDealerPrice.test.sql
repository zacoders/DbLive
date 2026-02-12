-- Arrange
declare @DealerDiscount decimal(5, 2) = 0.6 -- 60% hardcoded in the function

-- Act
select dbo.ufnGetProductDealerPrice(2, '2023-07-01') --as DealerPrice

-- Assert
select assert = 'rows-with-schema'

select cast(925.00 * @DealerDiscount as money)
