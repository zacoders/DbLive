
select s.name, t.name
from sys.tables t
	join sys.schemas s on s.schema_id = t.schema_id
where s.name != 'dblive'

select assert = 'row-count=0'

