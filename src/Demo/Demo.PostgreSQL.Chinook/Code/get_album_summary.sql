
drop function if exists get_album_summary(int);

create function get_album_summary(p_album_id int)
returns table
(
    album_id int,
    album_title varchar(160),
    artist_name varchar(120),
    tracks_count bigint,
    sold_quantity bigint,
    total_amount numeric(12,2)
)
language plpgsql
as
$$
begin
    return query
    select
        a.album_id,
        a.title as album_title,
        ar.name as artist_name,
        count(distinct t.track_id) as tracks_count,
        coalesce(sum(il.quantity), 0) as sold_quantity,
        coalesce(sum(il.quantity * il.unit_price), 0) as total_amount
    from album a
        join artist ar on ar.artist_id = a.artist_id
        left join track t on t.album_id = a.album_id
        left join invoice_line il on il.track_id = t.track_id
    where a.album_id = p_album_id
    group by a.album_id, a.title, ar.name;
end;
$$;
