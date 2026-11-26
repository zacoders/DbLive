-- A code object: deployed after the migrations, and again whenever this file changes.
create or replace function get_user(p_user_id int)
returns table ( user_id int, name varchar )
language sql
as $$
    select u.user_id, u.name
    from users u
    where u.user_id = p_user_id;
$$;
