---
created: 2026-11-21
---
# Simulated users: agents that build DbLive projects from the documentation

## Idea
Agents play the users DbLive does not have yet. Each one reads only the published documentation (`llms-full.txt` and the pages under `docs/`) and takes the published packages. It builds a DbLive project for a scenario, deploys it, runs its tests, changes it over several releases, and checks the result. When something fails, or does not do what the documentation says, the agent writes a finding. A finding confirmed as a bug or as a gap in the docs becomes a task.

Came up 2026-11-21, from the maintainer: while DbLive has few users, bugs are reported rarely, and agents can stand in for users.

## Why
- Every check today is written by someone who knows the code: unit tests, integration tests, `package-tests.cs`, the demos. A user knows only the docs. Only someone who has not seen the code finds the gap between the two: a behavior the docs do not state, or a sentence the code does not keep.
- `docs/ai-agents.md` says that assistants increasingly write the migrations, procedures and tests in a project, and `llms-full.txt` is written for them. So an agent is more than a stand-in for a user: it is one of the real kinds of user.
- Real projects break on the second deployment, the tenth release, or an upgrade of DbLive, not on the first `dotnet new`. The demos and the template cover the first deployment.

## Two roles
| Role | Sees | Does |
|---|---|---|
| user | the published docs and packages; not `src/`, not `tasks/` | builds and runs the project; writes findings with the files, the commands and the output |
| triage | the repository | reproduces the finding and decides what it is: a bug, a gap in the docs, unclear docs, or a user mistake that the docs could prevent. Looks for the same task in `tasks/`, `tasks/done/` and `tasks/rejected/`, then writes the task |

Without triage, every guess of the user agent becomes a task, and the maintainer reads noise.

## Scenarios
A scenario is a combination of axes, chosen by a seed so that the runs cover the matrix instead of what the model likes to write. The runs record which combinations they covered.

- provider: SQL Server or PostgreSQL; server versions; a case-sensitive collation;
- layout (§3.2); greenfield, or brownfield (§18.2);
- the life of the project over several releases: add a table, change the contract of a procedure, rename a column with expand/contract, a long backfill, roll back a release (§18.8), upgrade DbLive from the previous release;
- settings (§9), item settings (§9.3), `TransactionWrapLevel`, the number of threads of the code deployment;
- SQL tests, `[SqlFact]`, `[SqlDeployFact]`, the CI pipeline of §18.10;
- environment: Windows or Linux, a path with spaces or non-ASCII letters, each supported .NET SDK.

The persona is an axis too. A careful user follows the recipes. A careless user breaks the hard rules of §0: edits an applied migration, writes a code file that cannot run twice, puts `GO` in a test file. For the careless user, the error is what gets checked: does it name the file and point to the fix (§17)?

Real schemas as input: the agent ports a public schema into DbLive (WideWorldImporters, Northwind, Sakila or Pagila, the schema of an open-source application) instead of inventing `Users` and `Orders`. Left alone, agents write the same simple SQL every time.

## How a run knows it is ok
- Predict, then run. Before each step, the agent writes down what the docs say will happen: which migrations are applied, which error comes, which rows `dblive.migration` and `dblive.code` get. A difference between the prediction and the result is a finding, whichever of the two is wrong.
- Check the database itself, not only the exit code: the objects exist, `dblive.migration` holds the versions, the test results are the expected ones.
- Record friction even when everything passed: what the agent had to guess, which section it could not find, how many attempts it needed before the first green deployment. This goes to the docs.

## What a finding becomes
- A confirmed bug keeps its reproduction. The project of the run, cut down to the minimum, becomes a test (an integration test, or a check in `package-tests.cs`), so the work of the run is kept.
- A gap in the docs becomes a `docs` task.

## Relation to other ideas
`large-generated-project-for-performance.md` has a program generate a project of a given size, for measurement. That generator is deterministic and cheap. It could also give the agents variety: file names from the grammar of §5.1, combinations of settings. The agents are for realism and for the docs, the generator for size and for covering the matrix.

## Open questions
- [ ] How is the user agent kept away from the source: a separate repository with the packages and a copy of the docs, a sandbox without the DbLive repository, or only the published docs site?
- [ ] Which packages: the latest release from NuGet (what users get), or the packages of the current build (finds bugs before the release)? Both, at different stages?
- [ ] Where do findings wait for triage, and which number do their tasks take: an umbrella GitHub issue for simulated users, with the findings numbered under it (`NNNN-01-...`)?
- [ ] What runs it, and how often: Claude Code by hand, a scheduled cloud agent, or a workflow started by hand? What budget per run?
- [ ] Several models and tools as different users (Claude Code, Copilot, Cursor, a small model)? A weaker model may find the docs that are ambiguous.
- [ ] One generator for this idea and for the large project, or two?
- [ ] Start with one scenario by hand, and write down what it found, before building the matrix?

## Sources
- `docs/llms-full.txt`, `docs/ai-agents.md`
- `.github/scripts/package-tests.cs` (builds projects from the packages, the way a user does)
- `tasks/ideas/large-generated-project-for-performance.md`
