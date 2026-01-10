create or replace function get_customer_total_spent(p_customer_id int)
returns numeric(12,2)
language plpgsql
as
$$
declare
    v_total numeric(12,2);
begin
    select coalesce(sum(i.total), 0)
    into v_total
    from invoice i
    where i.customer_id = p_customer_id;

    return v_total;
end;
$$;
