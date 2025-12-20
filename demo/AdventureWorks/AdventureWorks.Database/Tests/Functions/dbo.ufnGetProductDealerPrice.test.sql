
select dbo.ufnGetProductDealerPrice(2, '2023-07-01') --as DealerPrice

select assert = 'rows-with-schema'

select cast(925.00 * 0.6 /* 60% hardcoded @DealerDiscount*/ as money)
