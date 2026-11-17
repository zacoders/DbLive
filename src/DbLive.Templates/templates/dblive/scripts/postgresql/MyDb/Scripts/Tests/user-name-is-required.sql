-- A data rule: a user must have a name. Without an assert, the test passes when the script raises no error.
do $$
begin
    insert into users ( name ) values ( '' );
    raise exception 'users accepted a user with an empty name';
exception
    when check_violation then null; -- the check constraint ck_users_name rejected the row, as expected
end $$;
