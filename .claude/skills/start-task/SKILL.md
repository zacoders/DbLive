---
name: start-task
description: Start work on a task from tasks/ - read it with its context, summarize it, check with an experiment that the problem is real, and propose ways to solve it, without changing the code. Use when the maintainer starts a task or asks to look at one, and no approach is chosen yet.
argument-hint: <task number or file, e.g. 0096-165>
---

# Start a task

The task: $ARGUMENTS. Without an argument, take the task file open in the IDE; when there is none, ask which one.

This is the analysis before the work. Do not change `src/`, `docs/` or other tasks until the maintainer has chosen an approach. The task file is the exception: findings go to its `## Notes` as you go.

## 1. Read

- The task file, its parent and the tasks found with it (`tasks/<number>-*`, also in `done/` and `rejected/`).
- `tasks/rejected/`: was the same proposal rejected before, and did something change since?
- The code under `## Where` and the sections of `docs/llms-full.txt` the task touches. Check that the names and line numbers of the task still match the code.

## 2. Summarize

Five sentences at most, in your own words: what is wrong or missing, who meets it and when, what it costs them. Say where the task is unclear or contradicts the code or the docs.

## 3. Check that the problem is real

- Reproduce a bug or a claim about behavior on a real server, not from reading the code, unless the task already marks it verified and the code did not change since. For SQL behavior: a temporary test class in `DbLive.MSSQL.Tests` or `DbLive.PostgreSQL.Tests` (Docker), removed after. For anything else: a scratch script or project outside the repo.
- Write the result to `## Notes`: what you ran, on which versions (server, Npgsql or SqlClient), the outcome, marked verified with the date. Also what you ruled out.
- When it does not reproduce, say so and ask whether to close or reject the task: only the maintainer rejects.
- Say how often a user meets it and how bad it is. When it needs an operator mistake, say that first.

## 4. Propose

- Two or three approaches. For each: what changes, effort, risk, whether an upgrade needs a change on the user's side (`breaking`), whether an older DbLive misreads the `dblive` tables after it (`MinCompatibleVersion`), the docs to update, and how it is tested. "Docs only" or "do nothing" is an approach when it is a real option.
- Your recommendation and why.
- Answer the open `- [ ]` questions of the task that the code or an experiment can answer; write the answers to the task.

End with the questions only the maintainer can answer, and wait for the choice.
