---
name: code-review
description: Review code changes in opentelemetry-dotnet-contrib for correctness, convention compliance, and consistency with project conventions. Use when reviewing PRs or code changes.
---

# opentelemetry-dotnet-contrib Code Review

Review code changes against the conventions documented for this repository. This
is a **monorepo of independently versioned, community-maintained** OpenTelemetry
components for .NET, so reviews must account for per-component scoping
(versioning, CHANGELOG, ownership, public API surface) in addition to general
code correctness.

**Reviewer mindset:** Be polite but thorough. Community contributors may not be
familiar with this repository's conventions, so explain *why* a rule applies and
point at the source (`AGENTS.md` / `REVIEW.md`) rather than just asserting it.
Treat the PR description and linked issues as claims to verify, not facts to
accept - but remain constructive; this repository depends on volunteer
contributions and reviews should help contributors succeed, not discourage them.

## When to Use This Skill

Use this skill when:

- Reviewing a PR or code change in opentelemetry-dotnet-contrib.
- Checking code for correctness, convention, or consistency issues before
  submitting a PR.
- Asked to review, critique, or provide feedback on code changes.
- Validating that a change follows this repository's conventions.

## Review Process

### Step 0: Load the Rules

Read `AGENTS.md` and `REVIEW.md` in full, and load `build/BannedSymbols.txt`
directly rather than relying on `REVIEW.md`'s summary of it - `REVIEW.md`
"String Comparisons and Culture-Sensitive Parsing" only calls out the
`string.Equals`/`TryParse` bans as examples, but the file itself is the
authoritative list and also bans the `ActivitySource`/`Meter` string
constructors and several `HttpClient`/`HttpContent` convenience methods.
Between `AGENTS.md` and `REVIEW.md`, the areas covered are: build/test
commands, package and versioning rules, `.publicApi/` tracking, CHANGELOG
format, and the "What NOT to Flag" list. Cite the specific rule a finding
violates rather than restating the rule set in your output. Also load
`.github/skills/code-review/pr-assessment.md` - the criteria for whether the
PR is justified, well-scoped, and a net positive. This is **not** covered in
`AGENTS.md` or `REVIEW.md`; always load it.

If any of these files cannot be loaded, say so explicitly and fall back to a
first-principles review, noting the gap in the output.

If the repository defines area-specific review agents under `.github/agents/`,
check for one matching the changed component and invoke it as a subtask,
integrating its findings. None exist in this repository today, so unless one
has since been added, continue the review yourself.

### Step 1: Gather Code Context Before Reading the PR Narrative

Do **not** read the PR description, linked issues, or existing review
comments yet - form an independent view first so the author's framing doesn't
anchor your judgment.

1. **Diff and file list**: Fetch the full diff and the list of changed files.
2. **Full source files**: For every changed file, read beyond the diff hunks -
   most components in this repository are small enough to read in full.
   Understand the surrounding code, not just the changed lines.
3. **Consumers and callers**: If the change modifies public API, grep for usages
   in the same component's tests and, for `src/Shared`/`test/Shared` files,
   every component that links them (via `<Compile Include=... Link=...>` - see
   `AGENTS.md` "Shared Source"). A change to shared source affects every
   component that links it, not just the one in the diff.
4. **Sibling components**: If the change fixes a bug or adds a pattern in one
   instrumentation, exporter, or resource detector, check whether sibling
   components have the same issue (e.g. the semantic-convention dual-emit
   pattern, or a Shared helper that several components copy instead of link).
5. **Git history**: Check recent commits to the changed files
   (`git log --oneline -20 -- <file>`). Look for related recent changes,
   reverts, or prior attempts at the same fix.
6. **Detect new/changed public API surface**: Diff
   `.publicApi/PublicAPI.Unshipped.txt` for each touched component and scan
   changed `src/**/*.cs` files for new `public`/`protected` members. Note
   whether every new or changed public member is declared in
   `PublicAPI.Unshipped.txt` - see `REVIEW.md` "Public API Surface". This is
   enforced by a build error, so an omission is a correctness bug in the PR, not
   a style nit.

### Step 2: Identify Affected Components

Because each `src/OpenTelemetry.{Type}.{Name}/` directory is an independently
versioned package, determine every component touched by the diff and, for each:

1. **CHANGELOG**: Confirm a `CHANGELOG.md` entry exists under `## Unreleased`
   for any behavioural change, per `REVIEW.md` "CHANGELOG". A PR touching
   multiple components needs an entry in each affected component's
   `CHANGELOG.md`. Performance improvements do not necessarily require a
   CHANGELOG entry as they can be considered internal refactoring, but any
   behavioral change does.
2. **Ownership context**: Check `.github/component_owners.yml` for the
   component's listed owners. This is context for whose domain expertise might
   be relevant - it does not change the review itself.
3. **New component check**: If the PR introduces a brand-new
   `src/OpenTelemetry.{Type}.{Name}/` directory, verify against `REVIEW.md` "New
   Components" (prior GitHub issue discussion, two-PR introduction pattern,
   `MinVerTagPrefix`/`PackageValidationBaselineVersion` set,
   `.github/component_owners.yml` entry, CI path filter, `.slnx` entry).

### Step 3: Form an Independent Assessment

Based only on the code gathered above, answer:

1. What does this change actually do - old behavior vs. new behavior?
2. Why might it be needed? Infer the motivation from the code itself.
3. Is this the right approach - is there a simpler alternative consistent with
   the existing architecture, or existing functionality that already
   solves it?
4. What problems do you see - bugs, edge cases, missing validation,
   missed TFMs, thread-safety, allocation/performance regressions in hot
   paths, semantic-convention mismatches, missing tests, missing CHANGELOG entries?

Write this down before reading the PR narrative. Use `pr-assessment.md` to
structure it.

### Step 4: Incorporate the PR Narrative and Reconcile

Now read the PR description, labels, linked issues, and existing review
comments. Treat these as **claims to verify**, not facts to accept.

1. Fetch the PR description, labels, linked issues, and author.
2. Check existing review comments to avoid duplicating feedback.
3. If the PR claims a bug fix or a performance improvement, verify it against
   the code and any benchmark evidence (see the `performance-benchmark` skill
   for what that evidence should look like).
4. Where your independent read disagrees with the author's framing, investigate
   rather than deferring - a problem your independent assessment found is not
   invalidated just because the description doesn't mention it.
5. Check the EasyCLA status; per `REVIEW.md`, maintainers generally won't
   review a PR before it passes.

### Step 5: Detailed Analysis

Apply every rule in `REVIEW.md` that is relevant to the diff's paths and
content; it is the authoritative, maintained rule set for this repository and
is not repeated here. Pay particular attention to the sections most often
missed by contributors: CHANGELOG, Public API Surface, Semantic Conventions,
and Instrumentation Package Conventions.

1. **Focus on what matters.** Prioritize correctness bugs, semantic-convention
   violations, missing/incorrect CHANGELOG or public API entries, thread-safety,
   allocation regressions in code that runs on every request and resource lifetime
   (`IDisposable`). Do not comment on trivial style issues unless they violate
   an explicit `REVIEW.md` rule.
2. **Consider collateral damage.** For every changed code path, investigate
   what other callers, signals (traces/metrics/logs), or semantic-convention
   opt-in modes (`OTEL_SEMCONV_STABILITY_OPT_IN`) flow through it, and whether
   the surface differs across TFMs. Only report a collateral risk once you
   have confirmed it against the actual code - per item 3 below, an
   unconfirmed guess is not an actionable finding.
3. **Be specific and actionable.** Reference the specific
   `REVIEW.md`/`AGENTS.md` rule by file and line, and show how you verified
   the issue (e.g. "checked the other opt-in branch and it doesn't set this
   attribute either").
4. **Flag severity clearly:**
   - :x: **error** - Must fix before merge. Bugs, missing CHANGELOG/public API
     entries, semantic-convention violations, banned-API usage.
   - :warning: **warning** - Should fix. Performance issues, missing test coverage,
     inconsistency with established patterns.
   - :bulb: **suggestion** - Consider changing. Style improvements, optional
     optimizations.
5. **Don't pile on.** If the same issue recurs across files, flag it once with a
   list of affected files.
6. **Don't flag what CI catches** - `dotnet format`, StyleCop, `markdownlint`,
   `yamllint`, and `build/BannedSymbols.txt` run automatically; see `REVIEW.md`
   "What NOT to Flag" for the full list (also covers Renovate/otelbot-generated
   PRs).
7. **Ensure code suggestions are valid** and match the surrounding file's
   existing style (`REVIEW.md` "Line Length and Readability" applies to
   suggested code too).

## Review Output Format

> :memo: **AI-generated content disclosure:** when posting review content to
> GitHub under a user's own credentials (not a dedicated bot/app account),
> include a visible `> [!NOTE]` disclosure that the content is AI-generated,
> unless the user explicitly asks to omit it.

```markdown
## PR Review

**Motivation**: <1-2 sentences on whether the problem is real and the PR is justified>

**Approach**: <1-2 sentences on whether the approach fits this component's existing conventions>

**Summary**: <:white_check_mark: LGTM / :warning: Needs Human Review / :warning: Needs Changes / :x: Reject>. <2-3 sentence summary>

---

### Detailed Findings

#### :white_check_mark: / :warning: / :x: <Category> - <Brief description>

<Explanation, citing file/line and the specific AGENTS.md/REVIEW.md rule.>

<!-- AI disclosure note goes below this line when posting under a personal account. -->
```

### Guidelines

- Begin the review body with `## PR Review`, followed by
  `**Motivation**:`, `**Approach**:`, and `**Summary**:` in that order.
- **Detailed Findings** headers use: :white_check_mark: (verified as correct/good),
  :warning: (should fix or follow-up), :x: (must fix before merge), :bulb:
  (minor/nice-to-have).
- **Test quality** should be its own finding when tests are part of the
  PR - see `REVIEW.md` "Testing".
- **Summary** verdict rules (mirrors the severity levels above):
  1. Any :warning: or :x: finding rules out "LGTM" - use "Needs Human Review"
     or "Needs Changes" instead.
  2. When unsure whether a concern is valid, escalate to "Needs Human Review"
     rather than LGTM.
  3. A change can be correct code with an insufficient approach (e.g. fixes one
     component but not the sibling components with the same issue) - reflect
     that gap in the verdict rather than collapsing it to LGTM because the diff
     itself compiles and passes tests.

## Where the Review Rules Live

- **[`AGENTS.md`](../../../AGENTS.md)** - repository structure, build/test
  commands, and conventions for adding components, `.csproj` patterns,
  versioning, and package management.
- **[`REVIEW.md`](../../../REVIEW.md)** - the line-item review rule set:
  CHANGELOG, NuGet references, public API surface, XML docs, banned
  string/culture APIs, code correctness, semantic conventions, instrumentation
  conventions, performance, testing, line length, API design patterns, new
  components, PR hygiene, PowerShell scripts, and what *not* to flag. **Always
  load this and treat it as the rule set for Step 5.**
- **`.github/skills/code-review/pr-assessment.md`** - the PR assessment
  criteria: reviewer mindset, Motivation, Evidence, Approach, Cost-Benefit,
  Scope, Risk, and Codebase Fit. Use it to write the Motivation, Approach, and
  Summary fields of the output.
- **`.github/skills/performance-benchmark/SKILL.md`** - use when a PR makes a
  performance claim that needs validating, or when `REVIEW.md` "Performance"
  requires benchmark evidence.

If a rule in `REVIEW.md` conflicts with something in this file, `REVIEW.md`
wins; it is the single source of truth for review rules and is expected to
change independently of this skill.

## Multi-Model Review (Optional)

If the environment supports launching sub-agents on different model families,
run the review in parallel across 2-3 models for a substantial or risky
change, then synthesize: deduplicate findings, elevate ones flagged by
multiple models, and include unique high-confidence findings. Skip this for
small or low-risk changes - it multiplies review cost by the number of models
used.

---

Build/test commands, banned APIs, the CHANGELOG format, the public API
process, testing conventions, and the full "what not to flag" list live in
[`AGENTS.md`](../../../AGENTS.md) and [`REVIEW.md`](../../../REVIEW.md).
Reload them if this skill's guidance seems to conflict; they take precedence.
