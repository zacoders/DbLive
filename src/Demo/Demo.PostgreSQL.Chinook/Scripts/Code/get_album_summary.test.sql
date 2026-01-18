

select * from get_album_summary(1);

select 'rows-with-schema' as assert;

select 1 as album_id
	 , 'For Those About To Rock We Salute You' as album_title
	 , 'AC/DC' as artist_name
	 , cast(10 as bigint) as tracks_count
	 , cast(10 as bigint) as sold_quantity
	 , cast(9.90 as numeric) as total_amount

