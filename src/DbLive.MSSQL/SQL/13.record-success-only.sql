-- the dblive tables keep only successes: a failure is in the exception and the log.
-- a code file or a breaking script of the Code folder whose last run failed outside the deployment transaction has a row with
-- status 'error': the row goes. For the next deployment it is the same: a code file without an applied row of its content runs.
-- dblive.unit_test_run kept the last result of each SQL test; DbLive never read it.
-- re-runnable: a database whose dblive.version was lowered by hand runs it again.

delete from dblive.code
where status = 'error';
go

if col_length('dblive.code', 'error') is not null
	alter table dblive.code drop column error;
go

if col_length('dblive.migration', 'error') is not null
	alter table dblive.migration drop column error;
go

drop table if exists dblive.unit_test_run;
go
