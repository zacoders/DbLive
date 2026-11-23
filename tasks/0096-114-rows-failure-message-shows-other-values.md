---
type: bug
priority: low
created: 2026-11-23
---
# The failure message of `rows` prints the values in another way than it compares them

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
- [ ] Fixed, with unit tests: `NULL`, a decimal under `ru-RU`, an array, `timestamptz` against `timestamp`
- [ ] Docs updated

## Notes
Related: the comparison itself takes `NULL` as `{}` (0096-108).
