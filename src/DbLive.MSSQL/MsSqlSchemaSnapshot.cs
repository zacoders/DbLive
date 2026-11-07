using DbLive.Schema;
using System.Text.RegularExpressions;

namespace DbLive.MSSQL;

/// <summary>
/// The schema snapshot of SQL Server, read from the <c>sys.*</c> catalog views (SQL Server 2017 or later: <c>string_agg</c>, <c>concat_ws</c>).
/// Constraints named by the server (<c>is_system_named</c>) are identified by their table and definition, not by their name;
/// columns are identified by name, not by position.
/// </summary>
internal static partial class MsSqlSchemaSnapshot
{
	public const string Query = """
		with obj as (
			-- user objects outside DbLive's own schema.
			select o.object_id
			     , o.name
			     , o.type
			     , o.parent_object_id
			     , s.name as schema_name
			     , concat(s.name, '.', o.name) as full_name
			from sys.objects o
			join sys.schemas s on s.schema_id = o.schema_id
			where o.is_ms_shipped = 0
			  and s.name <> 'dblive'
		),
		col as (
			-- the columns of tables and table types.
			select c.object_id
			     , c.column_id
			     , c.name
			     , concat_ws(' '
			         , case
			               when cc.object_id is not null then concat('as ', cc.definition, iif(cc.is_persisted = 1, ' persisted', ''))
			               when t.is_user_defined = 1 then concat(schema_name(t.schema_id), '.', t.name)
			               when t.name in ('varchar', 'char', 'varbinary', 'binary') then concat(t.name, '(', iif(c.max_length = -1, 'max', cast(c.max_length as varchar(10))), ')')
			               when t.name in ('nvarchar', 'nchar') then concat(t.name, '(', iif(c.max_length = -1, 'max', cast(c.max_length / 2 as varchar(10))), ')')
			               when t.name in ('decimal', 'numeric') then concat(t.name, '(', c.precision, ', ', c.scale, ')')
			               when t.name in ('datetime2', 'time', 'datetimeoffset') then concat(t.name, '(', c.scale, ')')
			               else t.name
			           end
			         , iif(c.collation_name is not null, concat('collate ', c.collation_name), null)
			         , iif(ic.object_id is not null, concat('identity(', cast(ic.seed_value as varchar(40)), ', ', cast(ic.increment_value as varchar(40)), ')'), null)
			         , iif(c.is_rowguidcol = 1, 'rowguidcol', null)
			         , iif(c.is_sparse = 1, 'sparse', null)
			         , iif(c.is_filestream = 1, 'filestream', null)
			         , iif(c.generated_always_type > 0, concat('generated always ', lower(replace(c.generated_always_type_desc collate database_default, '_', ' '))), null)
			         , iif(c.is_hidden = 1, 'hidden', null)
			         , iif(mc.object_id is not null, concat('masked with (function = ''', mc.masking_function, ''')'), null)
			         , iif(c.is_nullable = 1, 'null', 'not null')
			         , iif(dc.object_id is not null, concat_ws(' ', iif(dc.is_system_named = 0, concat('constraint ', dc.name), null), concat('default ', dc.definition)), null)
			       ) as definition
			from sys.columns c
			join sys.types t on t.user_type_id = c.user_type_id
			left join sys.computed_columns cc on cc.object_id = c.object_id and cc.column_id = c.column_id
			left join sys.identity_columns ic on ic.object_id = c.object_id and ic.column_id = c.column_id
			left join sys.masked_columns mc on mc.object_id = c.object_id and mc.column_id = c.column_id
			left join sys.default_constraints dc on dc.parent_object_id = c.object_id and dc.parent_column_id = c.column_id
		),
		idx as (
			-- indexes of tables, views and table types, with their column lists.
			select i.object_id
			     , i.index_id
			     , i.name
			     , i.is_primary_key
			     , i.is_unique_constraint
			     , k.columns as key_columns
			     , concat_ws(' '
			         , iif(i.is_unique = 1 and i.is_primary_key = 0 and i.is_unique_constraint = 0, 'unique', null)
			         , lower(i.type_desc collate database_default)
			         , iif(k.columns is not null, concat('(', k.columns, ')'), null)
			         , iif(inc.columns is not null, concat('include (', inc.columns, ')'), null)
			         , iif(i.has_filter = 1, concat('where ', i.filter_definition), null)
			         , iif(i.ignore_dup_key = 1, 'ignore_dup_key', null)
			         , iif(i.is_disabled = 1, 'disabled', null)
			       ) as definition
			from sys.indexes i
			outer apply (
				select string_agg(concat(c.name, iif(ic.is_descending_key = 1, ' desc', ' asc')), ', ') within group (order by ic.key_ordinal) as columns
				from sys.index_columns ic
				join sys.columns c on c.object_id = ic.object_id and c.column_id = ic.column_id
				where ic.object_id = i.object_id
				  and ic.index_id = i.index_id
				  and ic.key_ordinal > 0
			) k
			outer apply (
				-- included columns, and the columns of a columnstore index; their order does not matter.
				select string_agg(c.name, ', ') within group (order by c.name) as columns
				from sys.index_columns ic
				join sys.columns c on c.object_id = ic.object_id and c.column_id = ic.column_id
				where ic.object_id = i.object_id
				  and ic.index_id = i.index_id
				  and ic.key_ordinal = 0
				  and (ic.is_included_column = 1 or i.type in (5, 6))
			) inc
			where i.type > 0
			  and i.is_hypothetical = 0
		),
		fk as (
			select f.object_id
			     , string_agg(pc.name, ', ') within group (order by fc.constraint_column_id) as columns
			     , string_agg(rc.name, ', ') within group (order by fc.constraint_column_id) as referenced_columns
			from sys.foreign_keys f
			join sys.foreign_key_columns fc on fc.constraint_object_id = f.object_id
			join sys.columns pc on pc.object_id = fc.parent_object_id and pc.column_id = fc.parent_column_id
			join sys.columns rc on rc.object_id = fc.referenced_object_id and rc.column_id = fc.referenced_column_id
			group by f.object_id
		)

		select 'Schema' as object_type
		     , s.name as object_name
		     , null as parent_name
		     , '' as definition
		from sys.schemas s
		where s.schema_id between 5 and 16383
		  and s.name <> 'dblive'

		union all

		select 'Table'
		     , t.full_name
		     , t.schema_name
		     , concat_ws(' '
		         , iif(st.is_memory_optimized = 1, 'memory optimized', null)
		         , case st.temporal_type
		               when 1 then 'history table'
		               when 2 then concat('system versioned, history table ', schema_name(h.schema_id), '.', h.name)
		           end
		       )
		from obj t
		join sys.tables st on st.object_id = t.object_id
		left join sys.tables h on h.object_id = st.history_table_id

		union all

		select 'Column'
		     , concat(t.full_name, '.', c.name)
		     , t.full_name
		     , c.definition
		from obj t
		join col c on c.object_id = t.object_id
		where t.type = 'U'

		union all

		select iif(i.is_primary_key = 1, 'PrimaryKey', 'UniqueConstraint')
		       -- a table has one primary key; a unique constraint named by the server is identified by its columns.
		     , case
		           when i.is_primary_key = 1 then t.full_name
		           when kc.is_system_named = 0 then concat(t.full_name, '.', kc.name)
		           else concat(t.full_name, ' (', i.key_columns, ')')
		       end
		     , t.full_name
		     , concat_ws(' ', iif(i.is_primary_key = 1 and kc.is_system_named = 0, concat('constraint ', kc.name), null), i.definition)
		from obj t
		join idx i on i.object_id = t.object_id
		join sys.key_constraints kc on kc.parent_object_id = i.object_id and kc.unique_index_id = i.index_id
		where t.type = 'U'

		union all

		select 'Index'
		     , concat(t.full_name, '.', i.name)
		     , t.full_name
		     , i.definition
		from obj t
		join idx i on i.object_id = t.object_id
		where t.type in ('U', 'V')
		  and i.is_primary_key = 0
		  and i.is_unique_constraint = 0

		union all

		select 'ForeignKey'
		       -- a foreign key named by the server is identified by its columns and the table it references.
		     , iif(f.is_system_named = 0
		         , concat(t.full_name, '.', f.name)
		         , concat(t.full_name, ' (', fk.columns, ') references ', schema_name(r.schema_id), '.', r.name))
		     , t.full_name
		     , concat_ws(' '
		         , concat('(', fk.columns, ') references ', schema_name(r.schema_id), '.', r.name, ' (', fk.referenced_columns, ')')
		         , iif(f.delete_referential_action > 0, concat('on delete ', lower(replace(f.delete_referential_action_desc collate database_default, '_', ' '))), null)
		         , iif(f.update_referential_action > 0, concat('on update ', lower(replace(f.update_referential_action_desc collate database_default, '_', ' '))), null)
		         , iif(f.is_not_for_replication = 1, 'not for replication', null)
		         , iif(f.is_disabled = 1, 'disabled', null)
		         , iif(f.is_disabled = 0 and f.is_not_trusted = 1, 'not trusted', null)
		       )
		from obj t
		join sys.foreign_keys f on f.parent_object_id = t.object_id
		join fk on fk.object_id = f.object_id
		join sys.objects r on r.object_id = f.referenced_object_id

		union all

		select 'CheckConstraint'
		       -- a check constraint named by the server is identified by its expression.
		     , iif(ck.is_system_named = 0, concat(t.full_name, '.', ck.name), concat(t.full_name, ' check ', ck.definition))
		     , t.full_name
		     , concat_ws(' '
		         , concat('check ', ck.definition)
		         , iif(ck.is_not_for_replication = 1, 'not for replication', null)
		         , iif(ck.is_disabled = 1, 'disabled', null)
		         , iif(ck.is_disabled = 0 and ck.is_not_trusted = 1, 'not trusted', null)
		       )
		from obj t
		join sys.check_constraints ck on ck.parent_object_id = t.object_id
		where t.type = 'U'

		union all

		select case m.type
		           when 'V' then 'View'
		           when 'P' then 'Procedure'
		           when 'TR' then 'Trigger'
		           else 'Function'
		       end
		     , m.full_name
		     , iif(m.type = 'TR', p.full_name, m.schema_name)
		     , concat(sm.definition, iif(tr.is_disabled = 1, char(10) + 'disabled', ''))
		from obj m
		join sys.sql_modules sm on sm.object_id = m.object_id
		left join obj p on p.object_id = m.parent_object_id
		left join sys.triggers tr on tr.object_id = m.object_id
		where m.type in ('V', 'P', 'FN', 'IF', 'TF', 'TR')

		union all

		-- database DDL triggers have no schema.
		select 'Trigger'
		     , concat('database.', tr.name)
		     , null
		     , concat(sm.definition, iif(tr.is_disabled = 1, char(10) + 'disabled', ''))
		from sys.triggers tr
		join sys.sql_modules sm on sm.object_id = tr.object_id
		where tr.parent_class = 0
		  and tr.is_ms_shipped = 0

		union all

		select 'Type'
		     , concat(schema_name(t.schema_id), '.', t.name)
		     , schema_name(t.schema_id)
		     , case
		           when t.is_table_type = 1 then concat(
		               'table ('
		               , (select string_agg(concat(c.name, ' ', c.definition), ', ') within group (order by c.column_id) from col c where c.object_id = tt.type_table_object_id)
		               , (select string_agg(concat(', ', iif(i.is_primary_key = 1, 'primary key ', iif(i.is_unique_constraint = 1, 'unique ', 'index ')), i.definition), '') within group (order by i.index_id) from idx i where i.object_id = tt.type_table_object_id)
		               , ')')
		           else concat_ws(' '
		               , case
		                     when b.name in ('varchar', 'char', 'varbinary', 'binary') then concat(b.name, '(', iif(t.max_length = -1, 'max', cast(t.max_length as varchar(10))), ')')
		                     when b.name in ('nvarchar', 'nchar') then concat(b.name, '(', iif(t.max_length = -1, 'max', cast(t.max_length / 2 as varchar(10))), ')')
		                     when b.name in ('decimal', 'numeric') then concat(b.name, '(', t.precision, ', ', t.scale, ')')
		                     when b.name in ('datetime2', 'time', 'datetimeoffset') then concat(b.name, '(', t.scale, ')')
		                     else b.name
		                 end
		               , iif(t.is_nullable = 1, 'null', 'not null'))
		       end
		from sys.types t
		left join sys.types b on b.user_type_id = t.system_type_id
		left join sys.table_types tt on tt.user_type_id = t.user_type_id
		where t.is_user_defined = 1
		  and schema_name(t.schema_id) <> 'dblive'

		union all

		select 'Sequence'
		     , o.full_name
		     , o.schema_name
		     , concat_ws(' '
		         , type_name(sq.user_type_id)
		         , concat('start with ', cast(sq.start_value as varchar(40)))
		         , concat('increment by ', cast(sq.increment as varchar(40)))
		         , concat('minvalue ', cast(sq.minimum_value as varchar(40)))
		         , concat('maxvalue ', cast(sq.maximum_value as varchar(40)))
		         , iif(sq.is_cycling = 1, 'cycle', 'no cycle')
		         , iif(sq.is_cached = 1, concat('cache', ' ' + cast(sq.cache_size as varchar(20))), 'no cache')
		       )
		from obj o
		join sys.sequences sq on sq.object_id = o.object_id

		union all

		select 'Synonym'
		     , o.full_name
		     , o.schema_name
		     , sn.base_object_name
		from obj o
		join sys.synonyms sn on sn.object_id = o.object_id

		-- objects with one name are numbered in this order (SchemaSnapshot).
		order by object_type, object_name, definition
		""";

	public static SchemaObject Normalize(SchemaObject obj)
		=> obj.Type is SchemaObjectType.View or SchemaObjectType.Procedure or SchemaObjectType.Function or SchemaObjectType.Trigger
			? obj with { Definition = NormalizeModule(obj.Definition) }
			: obj;

	/// <summary>
	/// SQL Server stores the source of a module as written, but "create or alter" as "create" and "alter" as "CREATE":
	/// the keyword is compared in lower case, the text without differences in whitespace.
	/// </summary>
	private static string NormalizeModule(string definition)
		=> SqlText.CollapseWhitespace(CreateKeywordRegex().Replace(definition, "${lead}create", 1));

	// the first "create", after the comments the source may start with.
	[GeneratedRegex(@"^(?<lead>(?:\s|--[^\n]*\n|/\*.*?\*/)*)create\b", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex CreateKeywordRegex();
}
