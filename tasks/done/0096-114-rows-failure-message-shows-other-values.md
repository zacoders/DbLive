---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# The failure message of `rows` prints the values in another way than it compares them

## Release note
**The failure message of `rows` prints the values as it compares them.** `Expected values:` and `Actual values:` show the JSON text of each value, the text that `rows` compares: a string in double quotes (`"test-user"`, was `'test-user'`), `NULL` as `null` (was empty), an array as `[1,2]` and `bytea` / `varbinary` in Base64 (were `System.Int32[]` and `System.Byte[]`), a date in ISO format, with `Z` for a PostgreSQL `timestamptz` (`"2026-01-01T00:00:00Z"`), and numbers with a dot in every culture. Before, two different values could print the same, and the reason of the failure was not visible. Nothing to change on upgrade.

## Problem
`rows` compares the JSON text of two values (`UnitTestResultChecker.cs:320-349`) but prints them with `ToString()` (`ListToSring`, `:351-354`):
- `NULL` prints as an empty string.
- Decimals and dates use the current culture (`150,00` on a `ru-RU` machine).
- Arrays and `byte[]` print as `System.Int32[]` and `System.Byte[]` on both sides.
- On PostgreSQL `timestamptz` and `timestamp` print the same, because `DateTime.ToString()` drops `Kind`, while their JSON differs (`...Z`). §8.3 warns about exactly this case.

So "Expected values" and "Actual values" can look the same on a failed test, and the reason is not visible.

## Where
- `src/DbLive/Deployers/Testing/UnitTestResultChecker.cs:351-354`

## Fix
Print the JSON text that was compared, or format with the invariant culture and print `NULL` for `NULL`.

## Docs to update
- `docs/unit-testing/stored-proc-and-unit-test.md:76-82` (sample failure output), if the format changes
- `docs/llms-full.txt` §8.3, if it shows the format

## Done when
- [x] Fixed, with unit tests: `NULL`, a decimal under `ru-RU`, an array, `timestamptz` against `timestamp`
- [x] Docs updated

## Notes
Related: the comparison itself takes `NULL` as `{}` (0096-108). Done together with it: both use the text of `RowValueText`.

## Result
- The message prints the text that `rows` compares (`RowValueText.Write`, see 0096-108), so the two cannot differ. `ListToSring` is gone.
- The JSON is written with `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, so the message shows an apostrophe and non-ASCII letters as they are (`"O'Brien Ж"`), not as `'` and `Ж`. The comparison does not change: the encoder maps equal strings to equal texts and different strings to different texts.
- Tests: `UnitTestResultCheckerRowsTests.Rows_failure_message_prints_the_compared_text` (under `ru-RU`: `NULL`, a decimal, an array, `timestamptz` against `timestamp`, a string with an apostrophe and a Cyrillic letter, `macaddr`); `Rows_DoesNotMatch_Values_Fail` and the PostgreSQL `Rows_test_compares_NaN_and_infinity` assert the whole new message.
- Docs: the sample failure in `docs/unit-testing/stored-proc-and-unit-test.md`; the format in `docs/llms-full.txt` §8.3 and `docs/unit-testing/index.md`.
