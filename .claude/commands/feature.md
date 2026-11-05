---
description: Run a feature through planner, implementer, test-writer and reviewer
argument-hint: <feature description>
disable-model-invocation: true
allowed-tools: Agent, Read, Bash(git status *), Bash(git branch --show-current), Bash(git switch -c *)
---

Build this feature for Kanshilike using the project's subagents: $ARGUMENTS

If no feature was given, ask for one and stop.

Run the steps in order, one subagent at a time. Each subagent starts with no knowledge of this conversation, so pass it the feature request, the plan path and any results it needs. Do not do a subagent's work yourself.

## 0. Branch

Pick a short kebab-case `<feature>` name. Check the current branch:
- On `main`: run `git switch -c feature/<feature>`.
- On another branch: stop and ask whether to continue there, unless it is clearly for this feature.

Ignore changes under `.claude/skills/`; they are regenerated noise.

## 1. Plan: `planner`

Ask it to write `docs/plans/<feature>.md`. Read the result. Stop and ask the user if there are blocking open questions, or if the feature conflicts with the demo scope or the architecture order.

## 2. Implement: `implementer`

Pass the plan path. Stop and report if it could not finish or the code does not compile. If compile status is `NOT VERIFIED`, continue and carry that into the summary.

## 3. Test: `test-writer`

Pass the plan path and the implementer's report. Failed, `NOT RUN` or `BLOCKED` tests do not stop the workflow. Pass the results to the reviewer. Do not ask anyone to fix them yet.

## 4. Review: `reviewer`

Pass the plan path, the implementer's report and the test results.

## 5. Report

Do not commit, push or open a PR. Do not start a fix loop. Report:
- Branch and plan path
- What changed
- Compile status and test results, stating clearly anything not run
- The review verdict and blocking findings
- The manual play-test checklist from the plan

If the verdict is `CHANGES REQUESTED`, ask whether to send the findings back to the implementer.
