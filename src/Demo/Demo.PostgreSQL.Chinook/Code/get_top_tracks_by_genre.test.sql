
select * from get_top_tracks_by_genre(1, 3);

select 'rows' as assert;

with cte_result ( track_id, track_name, total_quantity, total_amount ) 
as (
		  select 1, 'For Those About To Rock (We Salute You)', 1, 0.99
union all select 2, 'Balls to the Wall', 2, 1.98
union all select 3, 'Fast As a Shark', 1, 0.99
)
select *
from cte_result
order by track_id;


