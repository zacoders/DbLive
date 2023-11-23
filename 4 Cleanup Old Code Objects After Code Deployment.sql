
-- note: we can try to drop the type and warn instead of error in case type is used by some SP/FN
-- note: drop in multi threads, but considering priority.

-- drop order:
--  * sps should be dropped first
--  * functions should be dropped second
--  * note: types (tvps) should be dropped as last step

select
    c.full_name
  , o.type_desc
  , case o.type_desc 
        when 'SQL_STORED_PROCEDURE' then 1
        when 'SQL_INLINE_TABLE_VALUED_FUNCTION' then 2
        when 'SQL_TABLE_VALUED_FUNCTION' then 3
        when 'SQL_SCALAR_FUNCTION' then 4
        when 'TYPE_TABLE' then 10
        else 100
    end drop_priority 
  , case o.type_desc 
        when 'SQL_STORED_PROCEDURE' then concat('drop proc ', full_name)
        when 'SQL_INLINE_TABLE_VALUED_FUNCTION' then concat('drop function ', full_name)
        when 'SQL_TABLE_VALUED_FUNCTION' then concat('drop function ', full_name)
        when 'SQL_SCALAR_FUNCTION' then concat('drop function ', full_name)
        when 'TYPE_TABLE' then concat('drop type ', full_name)
    end drop_priority 
  , o.create_date
  , o.modify_date
from sys.objects o
    left join sys.table_types tt on tt.type_table_object_id = o.object_id
    left join sys.schemas s on s.schema_id = coalesce(tt.schema_id, o.schema_id)
    cross apply ( select coalesce(tt.name, o.name) as name ) true_obj
    cross apply ( select concat(quotename(s.name), '.', quotename(true_obj.name)) as full_name ) c
where s.name not in ( 'easyflow' ) 
  and o.type_desc in (
          'SQL_SCALAR_FUNCTION'
        , 'SQL_INLINE_TABLE_VALUED_FUNCTION'
        , 'SQL_TABLE_VALUED_FUNCTION'
        , 'SQL_STORED_PROCEDURE'
        , 'TYPE_TABLE'
    )
  and c.full_name not in (
    -- SSMS diagram related objects.
      'dbo.sp_helpdiagrams'
    , 'dbo.sp_helpdiagramdefinition'
    , 'dbo.sp_creatediagram'
    , 'dbo.sp_alterdiagram'
    , 'dbo.sp_upgraddiagrams'
    , 'dbo.sp_dropdiagram'
    , 'dbo.sp_renamediagram'
    , 'dbo.fn_diagramobjects'
  )

--union all

--select 
--    c.full_name
--  , 'TABLE_TYPE' as type_desc
--  , 10 as drop_priority
--  , 
--  , o.*
--from sys.table_types o
--    left join sys.schemas s on s.schema_id = o.schema_id
--    cross apply ( select concat(quotename(s.name), '.', quotename(o.name)) as full_name ) c

--select *
--from sys.objects
--where object_id in (138105370, 144458222, 160458279, 169949319, 170105484)