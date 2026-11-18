// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

import { execFileSync } from 'node:child_process';
import { writeFile } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';
import { parseArgs } from 'node:util';

const repositories = [
  'open-telemetry/opentelemetry-dotnet',
  'open-telemetry/opentelemetry-dotnet-contrib',
];
const componentLabels = ['comp:exporter.geneva', 'comp:exporter.onecollector'];
const interestLabels = ['good first issue', 'help wanted', ...componentLabels];
const actorFields = 'author { login __typename }';
const commentFields = `${actorFields} body createdAt updatedAt url`;
const reviewFields = `${actorFields} body state submittedAt url commit { oid }`;
const pageFields = 'pageInfo { hasNextPage endCursor }';
const requestFields = 'requestedReviewer { ... on User { login } }';
const threadFields = `isResolved comments(first: 100) {
  nodes { ${commentFields} } ${pageFields}
}`;

export class GitHub {
  constructor(token, fetchImpl = fetch) {
    this.token = token;
    this.fetch = fetchImpl;
  }

  async request(path, method = 'GET', body) {
    const response = await this.fetch(`https://api.github.com/${path}`, {
      method,
      headers: {
        Authorization: `Bearer ${this.token}`,
        Accept: 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2022-11-28',
        'User-Agent': 'opentelemetry-personal-triage',
      },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
      signal: AbortSignal.timeout(60000),
    });
    const data = await response.json();
    if (!response.ok) {
      throw new Error(`GitHub ${method} ${path}: ${response.status} ${data.message ?? response.statusText}`);
    }

    return data;
  }

  async graphql(query, variables) {
    const result = await this.request('graphql', 'POST', { query, variables });
    if (result.errors?.length) {
      throw new Error(`GitHub GraphQL: ${result.errors.map(error => error.message).join('; ')}`);
    }

    return result.data;
  }

  async list(path) {
    const items = [];
    for (let page = 1; ; page++) {
      const batch = await this.request(`${path}${path.includes('?') ? '&' : '?'}per_page=100&page=${page}`);
      if (!Array.isArray(batch)) {
        throw new Error(`Expected a GitHub list at ${path}`);
      }

      items.push(...batch);
      if (batch.length < 100) {
        return items;
      }
    }
  }
}

export async function connectionPages(loadPage) {
  const nodes = [];
  let cursor = null;
  do {
    const connection = await loadPage(cursor);
    if (!connection?.nodes || !connection.pageInfo) {
      throw new Error('Missing GitHub connection; refusing to publish incomplete results');
    }

    nodes.push(...connection.nodes);
    if (!connection.pageInfo.hasNextPage) {
      return nodes;
    }

    const next = connection.pageInfo.endCursor;
    if (!next || next === cursor) {
      throw new Error('GitHub pagination did not advance');
    }

    cursor = next;
  } while (true);
}

function pullConnection(api, repository, number, field, selection) {
  const [owner, name] = repository.split('/');
  return connectionPages(async cursor => {
    const data = await api.graphql(`query($owner: String!, $name: String!, $number: Int!, $cursor: String) {
      repository(owner: $owner, name: $name) {
        pullRequest(number: $number) {
          ${field}(first: 100, after: $cursor) { nodes { ${selection} } ${pageFields} }
        }
      }
    }`, { owner, name, number, cursor });
    return data.repository?.pullRequest?.[field];
  });
}

async function openPulls(api, repository) {
  const [owner, name] = repository.split('/');
  const pulls = await connectionPages(async cursor => {
    const data = await api.graphql(`query($owner: String!, $name: String!, $cursor: String) {
      repository(owner: $owner, name: $name) {
        pullRequests(states: OPEN, first: 100, after: $cursor, orderBy: {field: UPDATED_AT, direction: DESC}) {
          nodes {
            number title url createdAt updatedAt isDraft reviewDecision ${actorFields}
            commits(last: 1) { nodes { commit { oid } } }
            labels(first: 100) { nodes { name } ${pageFields} }
            reviewRequests(first: 100) { nodes { ${requestFields} } ${pageFields} }
          }
          ${pageFields}
        }
      }
    }`, { owner, name, cursor });
    return data.repository?.pullRequests;
  });

  for (const pr of pulls.filter(pr => !pr.isDraft)) {
    pr.repository = repository;
    pr.head = pr.commits.nodes[0]?.commit.oid;
    pr.labels = pr.labels.pageInfo.hasNextPage
      ? await pullConnection(api, repository, pr.number, 'labels', 'name')
      : pr.labels.nodes;
    pr.reviewRequests = pr.reviewRequests.pageInfo.hasNextPage
      ? await pullConnection(api, repository, pr.number, 'reviewRequests', requestFields)
      : pr.reviewRequests.nodes;
    pr.comments = await pullConnection(api, repository, pr.number, 'comments', commentFields);
    pr.reviews = await pullConnection(api, repository, pr.number, 'reviews', reviewFields);
  }

  return pulls.filter(pr => !pr.isDraft);
}

async function reviewThreads(api, pr) {
  const threads = await pullConnection(api, pr.repository, pr.number, 'reviewThreads', `id ${threadFields}`);
  for (const thread of threads) {
    const first = thread.comments;
    thread.comments = await connectionPages(async cursor => {
      if (!cursor) {
        return first;
      }

      const data = await api.graphql(`query($id: ID!, $cursor: String) {
        node(id: $id) {
          ... on PullRequestReviewThread {
            comments(first: 100, after: $cursor) { nodes { ${commentFields} } ${pageFields} }
          }
        }
      }`, { id: thread.id, cursor });
      return data.node?.comments;
    });
  }

  return threads;
}

function sameUser(actor, user) {
  return actor?.login?.toLowerCase() === user.toLowerCase();
}

function isHuman(actor) {
  return actor && actor.__typename !== 'Bot' && !actor.login.endsWith('[bot]');
}

function participated(pr, user) {
  return [...pr.comments, ...pr.reviews].some(item => sameUser(item.author, user));
}

function timestamp(item) {
  return item.submittedAt ?? item.createdAt ?? '';
}

function newest(items) {
  return [...items].sort((a, b) => timestamp(b).localeCompare(timestamp(a)));
}

function dashboard(pr) {
  const status = pr.comments
    .filter(comment => comment.author?.login === 'opentelemetry-pr-dashboard'
      && comment.body.includes('<!-- pull-request-dashboard-status -->'))
    .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt))[0];
  const text = status?.body.replace(/<!--[\s\S]*?-->/g, '').split('<details>')[0].trim() ?? '';
  return { text, url: status?.url, maintainers: /\*\*Waiting on maintainers\*\*/i.test(text) };
}

function requestsAction(body) {
  const unquoted = body.split('\n').filter(line => !line.trimStart().startsWith('>')).join('\n');
  return /\?|\b(?:please|ptal|thoughts)\b|\b(?:can|could|would|will)\s+you\b|\b(?:re-?review|review requested|ready for (?:your )?review|take (?:another|a) look)\b/i.test(unquoted);
}

function mentions(body, user) {
  const unquoted = body.split('\n').filter(line => !line.trimStart().startsWith('>')).join('\n');
  return new RegExp(`(^|[^\\w-])@${user}(?![\\w-])`, 'i').test(unquoted);
}

function excerpt(body) {
  return body.replace(/<!--[\s\S]*?-->/g, '').replace(/\s+/g, ' ').trim().slice(0, 200);
}

export function personalActions(pr, user) {
  if (pr.isDraft || !participated(pr, user)) {
    return [];
  }

  const actions = [];
  const mine = newest([...pr.comments, ...pr.reviews].filter(item => sameUser(item.author, user)))[0];
  const lastActivity = timestamp(mine);
  const myReview = newest(pr.reviews.filter(review => sameUser(review.author, user) && review.state !== 'PENDING'))[0];
  const status = dashboard(pr);
  const add = (kind, url, reason) => {
    if (!actions.some(action => action.url === url)) {
      actions.push({ kind, url, reason });
    }
  };

  if (pr.reviewRequests.some(request => sameUser(request.requestedReviewer, user))) {
    add('Review requested', `${pr.url}/files`, 'GitHub currently requests your review.');
  }

  for (const comment of pr.comments) {
    if (isHuman(comment.author) && !sameUser(comment.author, user)
      && comment.createdAt > lastActivity && mentions(comment.body, user) && requestsAction(comment.body)) {
      add('Reply or review requested', comment.url, excerpt(comment.body));
    }
  }

  for (const review of pr.reviews) {
    if (isHuman(review.author) && !sameUser(review.author, user) && review.submittedAt > lastActivity
      && mentions(review.body, user) && requestsAction(review.body)) {
      add('Reply or review requested', review.url, excerpt(review.body));
    }
  }

  for (const thread of pr.threads ?? []) {
    if (thread.isResolved) {
      continue;
    }

    const comments = newest(thread.comments.filter(comment => isHuman(comment.author)));
    const last = comments[0];
    if (!last || sameUser(last.author, user)) {
      continue;
    }

    const ownComment = comments.find(comment => sameUser(comment.author, user));
    if (ownComment && last.createdAt > ownComment.createdAt
      && (!myReview || myReview.state !== 'APPROVED' || last.createdAt > myReview.submittedAt)) {
      add(requestsAction(last.body) ? 'Reply needed' : 'Possibly needs you', last.url,
        `New response to your unresolved review thread: ${excerpt(last.body)}`);
    } else if (last.createdAt > lastActivity && mentions(last.body, user) && requestsAction(last.body)) {
      add('Reply needed', last.url, excerpt(last.body));
    }
  }

  if (!actions.length && !status.maintainers && /\*\*Waiting on reviewers\*\*/i.test(status.text)
    && myReview && myReview.state !== 'APPROVED' && myReview.commit?.oid
    && pr.head && myReview.commit.oid !== pr.head) {
    add('Possibly needs you', status.url,
      'The dashboard requests review, and the head commit differs from your latest review. Verify whether your feedback is addressed.');
  }

  return actions;
}

function activeApprovals(pr) {
  const decisions = new Map();
  for (const review of newest(pr.reviews)) {
    const login = review.author?.login.toLowerCase();
    if (login && !decisions.has(login) && ['APPROVED', 'CHANGES_REQUESTED', 'DISMISSED'].includes(review.state)) {
      decisions.set(login, review.state);
    }
  }

  return [...decisions.values()].filter(state => state === 'APPROVED').length;
}

export function needsComponentApproval(pr) {
  if (pr.isDraft || pr.repository !== repositories[1]
    || !pr.labels.some(label => componentLabels.includes(label.name))
    || pr.reviewDecision === 'APPROVED' || dashboard(pr).maintainers) {
    return false;
  }

  return ['REVIEW_REQUIRED', 'CHANGES_REQUESTED'].includes(pr.reviewDecision) || activeApprovals(pr) === 0;
}

export function selectDigest(issues, pulls, user, now) {
  const cutoff = new Date(now.getTime() - 14 * 24 * 60 * 60 * 1000).toISOString();
  const assigned = issues.filter(issue => issue.assignees.some(assignee => sameUser(assignee, user)));
  const assignedUrls = new Set(assigned.map(issue => issue.html_url));
  const personal = pulls.map(pr => ({ ...pr, actions: personalActions(pr, user) })).filter(pr => pr.actions.length);
  const personalUrls = new Set(personal.map(pr => pr.url));
  const components = pulls.filter(pr => !personalUrls.has(pr.url) && needsComponentApproval(pr));
  const recent = issues.filter(issue => !assignedUrls.has(issue.html_url) && issue.updated_at >= cutoff
    && issue.labels.some(label => interestLabels.includes(label.name)));
  const sortIssues = items => items.sort((a, b) => b.updated_at.localeCompare(a.updated_at));
  const sortPulls = items => items.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
  return {
    assigned: sortIssues(assigned),
    personal: sortPulls(personal),
    components: sortPulls(components),
    recent: sortIssues(recent),
  };
}

export async function collectDigest(api, user, now) {
  const issues = [];
  const pulls = [];
  for (const repository of repositories) {
    const allIssues = await api.list(`repos/${repository}/issues?state=open&sort=updated&direction=desc`);
    issues.push(...allIssues.filter(issue => !issue.pull_request).map(issue => ({ ...issue, repository })));
    const open = await openPulls(api, repository);
    for (const pr of open) {
      if (participated(pr, user)) {
        pr.threads = await reviewThreads(api, pr);
      }
    }

    pulls.push(...open);
  }

  return selectDigest(issues, pulls, user, now);
}

function escapeMarkdown(value) {
  return String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/@/g, '&#64;').replace(/\\/g, '\\\\').replace(/([|`*_[\]])/g, '\\$1').replace(/\s+/g, ' ').trim();
}

function issueLink(issue) {
  return `[${issue.repository}#${issue.number}](${issue.html_url}) ${escapeMarkdown(issue.title)}`;
}

function pullLink(pr) {
  return `[${pr.repository}#${pr.number}](${pr.url}) ${escapeMarkdown(pr.title)}`;
}

function notes(pr) {
  const status = dashboard(pr);
  const decision = {
    APPROVED: 'Approved',
    REVIEW_REQUIRED: 'Approval required',
    CHANGES_REQUESTED: 'Changes requested',
  }[pr.reviewDecision] ?? (activeApprovals(pr) ? 'Has approval' : 'No active approval');
  return status.url
    ? `${decision}. [Dashboard / blockers](${status.url}): ${escapeMarkdown(status.text.replace(/^## Pull request dashboard status\s*/, ''))}`
    : decision;
}

function table(rows, headings) {
  if (!rows.length) {
    return 'None.\n';
  }

  return `| ${headings.join(' | ')} |\n| ${headings.map(() => '---').join(' | ')} |\n`
    + rows.map(row => `| ${row.join(' | ')} |`).join('\n') + '\n';
}

export function localDate(now, timezone) {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: timezone, year: 'numeric', month: '2-digit', day: '2-digit',
  }).formatToParts(now);
  const part = type => parts.find(item => item.type === type).value;
  return `${part('year')}-${part('month')}-${part('day')}`;
}

export function renderDigest(digest, user, now, timezone) {
  const date = localDate(now, timezone);
  return `<!-- opentelemetry-daily-triage:${user}:${date} -->
# OpenTelemetry daily digest - ${date}

Digest for ${escapeMarkdown(user)} - generated ${now.toISOString()} (${timezone}).

## 1. Issues assigned to you

${table(digest.assigned.map(issue => [
    issueLink(issue), issue.updated_at.slice(0, 10), escapeMarkdown(issue.labels.map(label => label.name).join(', ')),
  ]), ['Issue', 'Updated (UTC)', 'Labels'])}
## 2. PRs you are already working on

Explicit requests appear alongside conservative **Possibly needs you** follow-ups.
These are suggestions to inspect, never automatic approvals.

${table(digest.personal.map(pr => [
    pullLink(pr),
    pr.actions.map(action => `**${action.kind}**: [Evidence](${action.url}) - ${escapeMarkdown(action.reason)}`).join('<br>'),
    pr.updatedAt.slice(0, 10), notes(pr),
  ]), ['PR', 'Your next action / evidence', 'Updated (UTC)', 'Status / blockers'])}
## 3. Geneva / OneCollector PRs needing approval

Open, non-draft component PRs without sufficient approval; bot recommendations are not approvals.
PRs already listed above are not repeated. Author or CI blockers remain visible in the dashboard links.

${table(digest.components.map(pr => [
    pullLink(pr), escapeMarkdown(pr.labels.filter(label => componentLabels.includes(label.name)).map(label => label.name).join(', ')),
    pr.updatedAt.slice(0, 10), notes(pr),
  ]), ['PR', 'Component', 'Updated (UTC)', 'Status / blockers'])}
## 4. Pull request dashboards

- [open-telemetry/opentelemetry-dotnet#7539](https://github.com/open-telemetry/opentelemetry-dotnet/issues/7539)
- [open-telemetry/opentelemetry-dotnet-contrib#4846](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/4846)

## 5. Recently active issues of interest

Open issues updated in the last 14 days with any of: ${interestLabels.map(label => `\`${label}\``).join(', ')}.
Assigned issues already listed above are not repeated. All lists are newest activity first.

${table(digest.recent.map(issue => [
    issueLink(issue), issue.updated_at.slice(0, 10), escapeMarkdown(issue.labels.map(label => label.name).join(', ')),
  ]), ['Issue', 'Updated (UTC)', 'Labels'])}
---

Generated by a GitHub Actions script implemented with GitHub Copilot.
Personal-action detection uses explicit requests and thread history, not an LLM; ambiguous cases are labeled.
`;
}

export async function publishDigest(api, destination, user, now, timezone, body) {
  if (destination.toLowerCase() !== `${user}/opentelemetry-dotnet-contrib`.toLowerCase()) {
    throw new Error('Publishing is restricted to the digest user\'s opentelemetry-dotnet-contrib fork');
  }

  const repository = await api.request(`repos/${destination}`);
  if (!repository.fork || repository.parent?.full_name !== repositories[1] || !repository.has_issues) {
    throw new Error('The destination must be an issues-enabled fork of open-telemetry/opentelemetry-dotnet-contrib');
  }

  if (body.length > 65000) {
    throw new Error('Digest exceeds the GitHub issue body limit; refusing to truncate results');
  }

  const date = localDate(now, timezone);
  const marker = `<!-- opentelemetry-daily-triage:${user}:${date} -->`;
  const since = new Date(now.getTime() - 48 * 60 * 60 * 1000).toISOString();
  const issues = await api.list(`repos/${destination}/issues?state=all&since=${encodeURIComponent(since)}`);
  const existing = issues.filter(issue => !issue.pull_request && issue.body?.startsWith(marker)
    && ['github-actions[bot]', user.toLowerCase()].includes(issue.user.login.toLowerCase()));
  if (existing.length > 1) {
    throw new Error('More than one digest exists for today; resolve duplicates before publishing');
  }

  const path = `repos/${destination}/issues`;
  const result = existing.length
    ? await api.request(`${path}/${existing[0].number}`, 'PATCH', { body })
    : await api.request(path, 'POST', {
      title: `OpenTelemetry daily digest - ${date}`, body,
    });
  return result.html_url;
}

async function main() {
  const { values } = parseArgs({
    options: {
      user: { type: 'string', default: process.env.TRIAGE_USER ?? 'harsimar' },
      destination: { type: 'string', default: process.env.TRIAGE_REPOSITORY ?? 'harsimar/opentelemetry-dotnet-contrib' },
      timezone: { type: 'string', default: process.env.TRIAGE_TIMEZONE ?? 'America/Los_Angeles' },
      output: { type: 'string' },
      publish: { type: 'boolean', default: false },
      help: { type: 'boolean', default: false },
    },
  });
  if (values.help) {
    console.log('node daily-triage.mjs [--user LOGIN] [--destination OWNER/REPO] [--timezone IANA_ZONE] [--output FILE] [--publish]');
    console.log('Default: read-only preview. Uses GH_TOKEN, GITHUB_TOKEN, or gh auth token. --publish creates/updates a daily issue.');
    return;
  }

  if (!/^[a-z\d](?:[a-z\d-]{0,37}[a-z\d])?$/i.test(values.user)) {
    throw new Error('Invalid GitHub username');
  }

  const now = new Date();
  localDate(now, values.timezone);
  const token = process.env.GH_TOKEN || process.env.GITHUB_TOKEN
    || execFileSync('gh', ['auth', 'token'], { encoding: 'utf8' }).trim();
  const api = new GitHub(token);
  const digest = await collectDigest(api, values.user, now);
  const body = renderDigest(digest, values.user, now, values.timezone);
  if (values.output) {
    await writeFile(values.output, body, 'utf8');
  } else {
    console.log(body);
  }

  if (values.publish) {
    const url = await publishDigest(api, values.destination, values.user, now, values.timezone, body);
    console.log(`Published digest: ${url}`);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch(error => {
    console.error(`Daily triage failed; no complete digest was produced: ${error.message}`);
    process.exitCode = 1;
  });
}
