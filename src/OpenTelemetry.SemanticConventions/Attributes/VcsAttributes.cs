// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#nullable enable

#pragma warning disable CS1570 // XML comment has badly formed XML

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class VcsAttributes
{
    /// <summary>
    /// The ID of the change (pull request/merge request/changelist) if applicable. This is usually a unique (within repository) identifier generated by the VCS system.
    /// </summary>
    public const string AttributeVcsChangeId = "vcs.change.id";

    /// <summary>
    /// The state of the change (pull request/merge request/changelist).
    /// </summary>
    public const string AttributeVcsChangeState = "vcs.change.state";

    /// <summary>
    /// The human readable title of the change (pull request/merge request/changelist). This title is often a brief summary of the change and may get merged in to a ref as the commit summary.
    /// </summary>
    public const string AttributeVcsChangeTitle = "vcs.change.title";

    /// <summary>
    /// The type of line change being measured on a branch or change.
    /// </summary>
    public const string AttributeVcsLineChangeType = "vcs.line_change.type";

    /// <summary>
    /// The group owner within the version control system.
    /// </summary>
    public const string AttributeVcsOwnerName = "vcs.owner.name";

    /// <summary>
    /// The name of the version control system provider.
    /// </summary>
    public const string AttributeVcsProviderName = "vcs.provider.name";

    /// <summary>
    /// The name of the <a href="https://git-scm.com/docs/gitglossary#def_ref">reference</a> such as <strong>branch</strong> or <strong>tag</strong> in the repository.
    /// </summary>
    /// <remarks>
    /// <c>base</c> refers to the starting point of a change. For example, <c>main</c>
    /// would be the base reference of type branch if you've created a new
    /// reference of type branch from it and created new commits.
    /// </remarks>
    public const string AttributeVcsRefBaseName = "vcs.ref.base.name";

    /// <summary>
    /// The revision, literally <a href="https://www.merriam-webster.com/dictionary/revision">revised version</a>, The revision most often refers to a commit object in Git, or a revision number in SVN.
    /// </summary>
    /// <remarks>
    /// <c>base</c> refers to the starting point of a change. For example, <c>main</c>
    /// would be the base reference of type branch if you've created a new
    /// reference of type branch from it and created new commits. The
    /// revision can be a full <a href="https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.186-5.pdf">hash value (see
    /// glossary)</a>,
    /// of the recorded change to a ref within a repository pointing to a
    /// commit <a href="https://git-scm.com/docs/git-commit">commit</a> object. It does
    /// not necessarily have to be a hash; it can simply define a <a href="https://svnbook.red-bean.com/en/1.7/svn.tour.revs.specifiers.html">revision
    /// number</a>
    /// which is an integer that is monotonically increasing. In cases where
    /// it is identical to the <c>ref.base.name</c>, it SHOULD still be included.
    /// It is up to the implementer to decide which value to set as the
    /// revision based on the VCS system and situational context.
    /// </remarks>
    public const string AttributeVcsRefBaseRevision = "vcs.ref.base.revision";

    /// <summary>
    /// The type of the <a href="https://git-scm.com/docs/gitglossary#def_ref">reference</a> in the repository.
    /// </summary>
    /// <remarks>
    /// <c>base</c> refers to the starting point of a change. For example, <c>main</c>
    /// would be the base reference of type branch if you've created a new
    /// reference of type branch from it and created new commits.
    /// </remarks>
    public const string AttributeVcsRefBaseType = "vcs.ref.base.type";

    /// <summary>
    /// The name of the <a href="https://git-scm.com/docs/gitglossary#def_ref">reference</a> such as <strong>branch</strong> or <strong>tag</strong> in the repository.
    /// </summary>
    /// <remarks>
    /// <c>head</c> refers to where you are right now; the current reference at a
    /// given time.
    /// </remarks>
    public const string AttributeVcsRefHeadName = "vcs.ref.head.name";

    /// <summary>
    /// The revision, literally <a href="https://www.merriam-webster.com/dictionary/revision">revised version</a>, The revision most often refers to a commit object in Git, or a revision number in SVN.
    /// </summary>
    /// <remarks>
    /// <c>head</c> refers to where you are right now; the current reference at a
    /// given time.The revision can be a full <a href="https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.186-5.pdf">hash value (see
    /// glossary)</a>,
    /// of the recorded change to a ref within a repository pointing to a
    /// commit <a href="https://git-scm.com/docs/git-commit">commit</a> object. It does
    /// not necessarily have to be a hash; it can simply define a <a href="https://svnbook.red-bean.com/en/1.7/svn.tour.revs.specifiers.html">revision
    /// number</a>
    /// which is an integer that is monotonically increasing. In cases where
    /// it is identical to the <c>ref.head.name</c>, it SHOULD still be included.
    /// It is up to the implementer to decide which value to set as the
    /// revision based on the VCS system and situational context.
    /// </remarks>
    public const string AttributeVcsRefHeadRevision = "vcs.ref.head.revision";

    /// <summary>
    /// The type of the <a href="https://git-scm.com/docs/gitglossary#def_ref">reference</a> in the repository.
    /// </summary>
    /// <remarks>
    /// <c>head</c> refers to where you are right now; the current reference at a
    /// given time.
    /// </remarks>
    public const string AttributeVcsRefHeadType = "vcs.ref.head.type";

    /// <summary>
    /// The type of the <a href="https://git-scm.com/docs/gitglossary#def_ref">reference</a> in the repository.
    /// </summary>
    public const string AttributeVcsRefType = "vcs.ref.type";

    /// <summary>
    /// Deprecated, use <c>vcs.change.id</c> instead.
    /// </summary>
    [Obsolete("Deprecated, use <c>vcs.change.id</c> instead.")]
    public const string AttributeVcsRepositoryChangeId = "vcs.repository.change.id";

    /// <summary>
    /// Deprecated, use <c>vcs.change.title</c> instead.
    /// </summary>
    [Obsolete("Deprecated, use <c>vcs.change.title</c> instead.")]
    public const string AttributeVcsRepositoryChangeTitle = "vcs.repository.change.title";

    /// <summary>
    /// The human readable name of the repository. It SHOULD NOT include any additional identifier like Group/SubGroup in GitLab or organization in GitHub.
    /// </summary>
    /// <remarks>
    /// Due to it only being the name, it can clash with forks of the same
    /// repository if collecting telemetry across multiple orgs or groups in
    /// the same backends.
    /// </remarks>
    public const string AttributeVcsRepositoryName = "vcs.repository.name";

    /// <summary>
    /// Deprecated, use <c>vcs.ref.head.name</c> instead.
    /// </summary>
    [Obsolete("Deprecated, use <c>vcs.ref.head.name</c> instead.")]
    public const string AttributeVcsRepositoryRefName = "vcs.repository.ref.name";

    /// <summary>
    /// Deprecated, use <c>vcs.ref.head.revision</c> instead.
    /// </summary>
    [Obsolete("Deprecated, use <c>vcs.ref.head.revision</c> instead.")]
    public const string AttributeVcsRepositoryRefRevision = "vcs.repository.ref.revision";

    /// <summary>
    /// Deprecated, use <c>vcs.ref.head.type</c> instead.
    /// </summary>
    [Obsolete("Deprecated, use <c>vcs.ref.head.type</c> instead.")]
    public const string AttributeVcsRepositoryRefType = "vcs.repository.ref.type";

    /// <summary>
    /// The <a href="https://support.google.com/webmasters/answer/10347851?hl=en#:~:text=A%20canonical%20URL%20is%20the,Google%20chooses%20one%20as%20canonical.">canonical URL</a> of the repository providing the complete HTTP(S) address in order to locate and identify the repository through a browser.
    /// </summary>
    /// <remarks>
    /// In Git Version Control Systems, the canonical URL SHOULD NOT include
    /// the <c>.git</c> extension.
    /// </remarks>
    public const string AttributeVcsRepositoryUrlFull = "vcs.repository.url.full";

    /// <summary>
    /// The type of revision comparison.
    /// </summary>
    public const string AttributeVcsRevisionDeltaDirection = "vcs.revision_delta.direction";

    /// <summary>
    /// The state of the change (pull request/merge request/changelist).
    /// </summary>
    public static class VcsChangeStateValues
    {
        /// <summary>
        /// Open means the change is currently active and under review. It hasn't been merged into the target branch yet, and it's still possible to make changes or add comments.
        /// </summary>
        public const string Open = "open";

        /// <summary>
        /// WIP (work-in-progress, draft) means the change is still in progress and not yet ready for a full review. It might still undergo significant changes.
        /// </summary>
        public const string Wip = "wip";

        /// <summary>
        /// Closed means the merge request has been closed without merging. This can happen for various reasons, such as the changes being deemed unnecessary, the issue being resolved in another way, or the author deciding to withdraw the request.
        /// </summary>
        public const string Closed = "closed";

        /// <summary>
        /// Merged indicates that the change has been successfully integrated into the target codebase.
        /// </summary>
        public const string Merged = "merged";
    }

    /// <summary>
    /// The type of line change being measured on a branch or change.
    /// </summary>
    public static class VcsLineChangeTypeValues
    {
        /// <summary>
        /// How many lines were added.
        /// </summary>
        public const string Added = "added";

        /// <summary>
        /// How many lines were removed.
        /// </summary>
        public const string Removed = "removed";
    }

    /// <summary>
    /// The name of the version control system provider.
    /// </summary>
    public static class VcsProviderNameValues
    {
        /// <summary>
        /// <a href="https://github.com">GitHub</a>.
        /// </summary>
        public const string Github = "github";

        /// <summary>
        /// <a href="https://gitlab.com">GitLab</a>.
        /// </summary>
        public const string Gitlab = "gitlab";

        /// <summary>
        /// Deprecated, use <c>gitea</c> instead.
        /// </summary>
        public const string Gittea = "gittea";

        /// <summary>
        /// <a href="https://gitea.io">Gitea</a>.
        /// </summary>
        public const string Gitea = "gitea";

        /// <summary>
        /// <a href="https://bitbucket.org">Bitbucket</a>.
        /// </summary>
        public const string Bitbucket = "bitbucket";
    }

    /// <summary>
    /// The type of the <a href="https://git-scm.com/docs/gitglossary#def_ref">reference</a> in the repository.
    /// </summary>
    public static class VcsRefBaseTypeValues
    {
        /// <summary>
        /// <a href="https://git-scm.com/docs/gitglossary#Documentation/gitglossary.txt-aiddefbranchabranch">branch</a>.
        /// </summary>
        public const string Branch = "branch";

        /// <summary>
        /// <a href="https://git-scm.com/docs/gitglossary#Documentation/gitglossary.txt-aiddeftagatag">tag</a>.
        /// </summary>
        public const string Tag = "tag";
    }

    /// <summary>
    /// The type of the <a href="https://git-scm.com/docs/gitglossary#def_ref">reference</a> in the repository.
    /// </summary>
    public static class VcsRefHeadTypeValues
    {
        /// <summary>
        /// <a href="https://git-scm.com/docs/gitglossary#Documentation/gitglossary.txt-aiddefbranchabranch">branch</a>.
        /// </summary>
        public const string Branch = "branch";

        /// <summary>
        /// <a href="https://git-scm.com/docs/gitglossary#Documentation/gitglossary.txt-aiddeftagatag">tag</a>.
        /// </summary>
        public const string Tag = "tag";
    }

    /// <summary>
    /// The type of the <a href="https://git-scm.com/docs/gitglossary#def_ref">reference</a> in the repository.
    /// </summary>
    public static class VcsRefTypeValues
    {
        /// <summary>
        /// <a href="https://git-scm.com/docs/gitglossary#Documentation/gitglossary.txt-aiddefbranchabranch">branch</a>.
        /// </summary>
        public const string Branch = "branch";

        /// <summary>
        /// <a href="https://git-scm.com/docs/gitglossary#Documentation/gitglossary.txt-aiddeftagatag">tag</a>.
        /// </summary>
        public const string Tag = "tag";
    }

    /// <summary>
    /// Deprecated, use <c>vcs.ref.head.type</c> instead.
    /// </summary>
    public static class VcsRepositoryRefTypeValues
    {
        /// <summary>
        /// <a href="https://git-scm.com/docs/gitglossary#Documentation/gitglossary.txt-aiddefbranchabranch">branch</a>.
        /// </summary>
        [Obsolete("Deprecated, use <c>vcs.ref.head.type</c> instead.")]
        public const string Branch = "branch";

        /// <summary>
        /// <a href="https://git-scm.com/docs/gitglossary#Documentation/gitglossary.txt-aiddeftagatag">tag</a>.
        /// </summary>
        [Obsolete("Deprecated, use <c>vcs.ref.head.type</c> instead.")]
        public const string Tag = "tag";
    }

    /// <summary>
    /// The type of revision comparison.
    /// </summary>
    public static class VcsRevisionDeltaDirectionValues
    {
        /// <summary>
        /// How many revisions the change is behind the target ref.
        /// </summary>
        public const string Behind = "behind";

        /// <summary>
        /// How many revisions the change is ahead of the target ref.
        /// </summary>
        public const string Ahead = "ahead";
    }
}
