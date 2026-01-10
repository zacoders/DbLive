drop function if exists get_top_tracks_by_genre(int, int);

create function get_top_tracks_by_genre(
    p_genre_id int,
    p_limit int default 10
)
returns table
(
    track_id int,
    track_name varchar(200),
    total_quantity bigint,
    total_amount numeric(12,2)
)
language plpgsql
as
$$
begin
    return query
    select
        t.track_id,
        t.name as track_name,
        sum(il.quantity) as total_quantity,
        sum(il.quantity * il.unit_price) as total_amount
    from track t
        join invoice_line il on il.track_id = t.track_id
    where t.genre_id = p_genre_id
    group by t.track_id, t.name
    order by t.track_id, t.name
    limit p_limit;
end;
$$;
