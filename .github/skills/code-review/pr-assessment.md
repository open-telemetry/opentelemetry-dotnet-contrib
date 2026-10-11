# Holistic PR Assessment

Review-only criteria: these apply when assessing a pull request as a whole, not
when authoring code. The line-item coding/review rules live in
[`REVIEW.md`](../../../REVIEW.md) (see "Where the Review Rules Live" in
`SKILL.md`) - this file does not repeat them.

Use the criteria below to write the Motivation, Approach, and Summary fields of
the review output.

**Reviewer mindset:** Be polite but thorough. This repository accepts community
contributions across many independently owned components, so weigh whether a
change is a net positive for *that specific component* and for the repository's
long-term maintenance burden, not just whether the diff is technically correct.

Before reviewing individual lines of code, evaluate the PR as a whole.

## Motivation & Justification

- **Every community PR must articulate what problem it solves and why.** Don't
  accept vague or absent motivation. If the PR links to a discussion or an issue
  that already establishes motivation, that's sufficient - don't demand the author
  restate what's already documented. For maintainers, trivial PRs may lack specific
  motivation as the context may have been discussed elsewhere, such as Slack, or
  be immediately apparent.
- **Challenge new abstractions, flags, and dependencies with "Do we need
  this?"** Each new package in a component is a maintenance obligation that
  outlives the PR (see `AGENTS.md` "NuGet References in Source Projects" -
  instrumentation packages should reference only the minimal OpenTelemetry
  packages needed).
- **A brand-new component needs prior discussion.** Per `REVIEW.md` "New
  Components", the design, naming, and necessity of a new component should have
  been discussed in a GitHub issue before the PR was opened - a new package is a
  new independently versioned, indefinitely maintained surface for the project.

## Evidence & Data

- **Require measurable performance data before accepting optimization claims.**
  Per `REVIEW.md` "Performance", any PR claiming a performance improvement
  should include BenchmarkDotNet results. Use the `performance-benchmark` skill
  to reproduce or validate these claims with `benchmark.ps1` rather than
  accepting the numbers in the PR description at face value.
- **Distinguish real wins from micro-benchmark noise.** A benchmark with an
  unrealistic input shape (e.g. a single fixed string, an empty collection) can
  overstate gains that don't hold for typical telemetry payloads. Prefer
  evidence from realistic attribute/tag shapes.
- **Semantic-convention claims need a citation.** Per `REVIEW.md`
  "Miscellaneous", feedback about OpenTelemetry semantic conventions should cite
  the [specification
  repository](https://github.com/open-telemetry/semantic-conventions), not rely
  on memory of what a convention says. Semantic Conventions should reference a
  specific version, not just "latest".

## Approach & Alternatives

- **Check whether the PR solves the right problem at the right layer.** An
  instrumentation package should only add tags to an activity it already
  started, not start new activities outside its stated purpose (`REVIEW.md`
  "Instrumentation Package Conventions"). A fix applied in the wrong layer (e.g.
  patching a symptom in one exporter instead of a shared helper) is a signal to
  redirect early.
- **When a PR takes a fundamentally wrong approach, redirect early.** Don't
  iterate on implementation details of a flawed design - push back on the
  direction first.
- **Prefer the simplest solution that fits existing patterns.** Check sibling
  components for how the same problem was solved elsewhere before endorsing a
  novel approach.

## Cost-Benefit & Complexity

- **Weigh whether the change is a net positive for the specific component**, not
  just whether it compiles and passes tests. A dependency added to satisfy one
  edge case may not be worth the added `PackageReference` surface for every
  consumer of that component.
- **Every addition creates a maintenance obligation borne by the component's
  owners** (see `.github/component_owners.yml`) - not just the contributor.
  Weigh this against the reported benefit, especially for niche or
  narrowly-scoped functionality.

## Scope & Focus

- **Require large or mixed PRs to be split into focused changes.** Per
  `REVIEW.md` "Pull Request Hygiene", thousands of changed lines in one PR make
  meaningful review impractical.
- **A PR touching multiple components should be justified.** It's fine when the
  components share a genuine cross-cutting change (e.g. `src/Shared`), but flag
  it when unrelated components are bundled together for convenience.
- **Defer tangential improvements to follow-up PRs.** Even good ideas should
  wait if they're not part of the PR's core purpose, unless they are trivial
  and small. If in doubt, defer to a follow-up PR.

## Risk & Compatibility

- **Flag breaking changes and require an explicit maintainer decision.** Per
  `REVIEW.md` "Public API Surface", breaking API changes (removals, signature
  changes, type moves) need an explicit maintainer decision and a
  `**Breaking Change**:`-prefixed CHANGELOG entry.
- **Sampling-sensitive attributes must be set before `Activity.Start()`.** A
  change that moves attribute-setting after `Start()` is a behavioral regression
  for custom samplers, even if tests still pass (`REVIEW.md` "OpenTelemetry
  Semantic Conventions").
- **Independent versioning limits blast radius but doesn't eliminate risk.** A
  breaking change in one component doesn't force a major bump elsewhere, but
  still needs the same rigor within that component's own version history.

## Codebase Fit & History

- **Ensure new code matches the conventions of its own component and of
  `src/Shared`.** Code shared across components must be linked via
  `<Compile Include=... Link=...>`, never copied or referenced as a project
  reference (`AGENTS.md` "Shared Source"). Copy-pasted logic that duplicates
  an existing Shared helper is a fit problem, not just a style nit. A bug in a
  Shared file can affect every component that links it, not just the one in
  the diff - check other linkers of the same file when reviewing a
  Shared-source change.
- **Check git history for prior attempts at the same fix.**
  `git log --oneline -- <path>` may reveal a revert or a previously rejected
  approach - require a clear explanation of what's different this time.
- **EasyCLA must be signed.** Per `REVIEW.md` "Pull Request Hygiene", flag any
  PR where the CLA check hasn't passed - maintainers will usually not review the
  change at all until it has.
