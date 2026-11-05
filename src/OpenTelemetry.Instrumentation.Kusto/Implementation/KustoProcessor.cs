// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
#if NET
using System.Collections.Frozen;
#endif
using System.Runtime.CompilerServices;
using Kusto.Language;
using Kusto.Language.Editor;
using Kusto.Language.Syntax;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Use the Kusto query language services to process Kusto queries for summarization and sanitization.
/// </summary>
internal static class KustoProcessor
{
    // Because we're not doing semantic analysis for queries, we can reuse the default global state (which includes all built-in functions and types)
    private static readonly GlobalState KustoParserGlobalState = GlobalState.Default.WithCache();

    private enum ReplacementKind
    {
        Placeholder,
        Remove,
    }

    /// <summary>
    /// Processes the specified Kusto query and optionally generates a summary and/or a sanitized version based on the
    /// provided options.
    /// </summary>
    /// <remarks>
    /// If both summarization and sanitization are requested, the query is parsed only once for
    /// efficiency. The returned <see cref="KustoStatementInfo"/> will have null values for summary or sanitized output
    /// if the corresponding option is not enabled.
    /// </remarks>
    /// <param name="shouldSummarize">
    /// Indicates whether to generate a summary of the query. If <see langword="true"/>, the returned object will
    /// include a summarized representation.
    /// </param>
    /// <param name="shouldSanitize">
    /// Indicates whether to generate a sanitized version of the query. If <see langword="true"/>, the returned object
    /// will include a sanitized representation.
    /// </param>
    /// <param name="query">
    /// The Kusto query to process.
    /// </param>
    /// <returns>
    /// A <see cref="KustoStatementInfo"/> containing the summary and/or sanitized version of the query, depending on
    /// the options specified.
    /// </returns>
    public static KustoStatementInfo Process(bool shouldSummarize, bool shouldSanitize, string query)
    {
        string? summarized = null;
        string? sanitized = null;

        // Both summarization and sanitization work on the syntax tree alone, so the query is parsed once.
        // Deliberately not ParseAndAnalyze: binding computes the result schema of every operator, and the
        // size of that schema (hence the time and memory spent) can grow exponentially with the length of
        // the query (e.g. a chain of self-joins), which is not acceptable on the client's request path.
        var code = KustoCode.Parse(query, KustoParserGlobalState);

        if (shouldSummarize)
        {
            // A query with syntax errors can't be confidently classified, and the summarizer would otherwise
            // echo unrecognized tokens verbatim, producing a high-cardinality summary of arbitrary (possibly
            // sensitive) input. Omit the summary in that case.
            if (code.GetSyntaxDiagnostics().Count == 0)
            {
                summarized = Summarize(code);
            }
        }

        if (shouldSanitize)
        {
            if (TrySanitize(code, out var sanitizedText))
            {
                sanitized = sanitizedText;
            }
        }

        return new KustoStatementInfo(summarized, sanitized);
    }

    private static bool TrySanitize(KustoCode code, [NotNullWhen(true)] out string? sanitized)
    {
        // Collect nodes that need replacements
        var collector = new SanitizerVisitor();
        try
        {
            code.Syntax.Accept(collector);
        }
        catch (InsufficientExecutionStackException)
        {
            // The syntax tree is nested too deeply to walk without risking a stack overflow. We cannot be
            // sure every literal was found, so omit the query text rather than emit a partially-redacted value.
            sanitized = null;
            return false;
        }

        if (!collector.ShouldSanitize)
        {
            sanitized = code.Text;
            return true;
        }

        var edits = collector.Edits;
        if (edits.Count == 0)
        {
            // No literals were found, so the original text is already free of literal values.
            sanitized = code.Text;
            return true;
        }

        // Literals were found; omit the query text if the redactions cannot all be applied rather than
        // emitting it unsanitized.
        var text = new EditString(code.Text);
        if (!text.CanApplyAll(edits))
        {
            sanitized = null;
            return false;
        }

        sanitized = text.ApplyAll(edits);
        return true;
    }

    private static string Summarize(KustoCode code)
    {
        using var walker = new SummarizerVisitor();
        try
        {
            code.Syntax.Accept(walker);
        }
        catch (InsufficientExecutionStackException)
        {
            // The syntax tree is nested too deeply to walk without risking a stack overflow. Return the
            // best-effort summary collected so far rather than crashing the process.
        }

        return walker.GetSummary();
    }

    private static TextEdit CreatePlaceholder(SyntaxElement node) => TextEdit.Replacement(node.TextStart, node.Width, "?");

    private static TextEdit CreateRemoval(SyntaxElement node) => TextEdit.Deletion(node.TextStart, node.Width);

    /// <summary>
    /// Visitor that traverses the KQL looking for literal values to replace with the PLACEHOLDER value.
    /// </summary>
    private sealed class SanitizerVisitor : DefaultSyntaxVisitor
    {
        // Literal token kinds (StringLiteralToken, LongLiteralToken, ...) discovered once by name so any future
        // literal kind is covered automatically. Used (together with identifier tokens) to redact value-bearing
        // tokens the parser left loose inside a skipped (unparsable) region of a malformed query.
#if NET
        private static readonly FrozenSet<SyntaxKind> LiteralTokenKinds = BuildLiteralTokenKinds();
#else
        private static readonly HashSet<SyntaxKind> LiteralTokenKinds = BuildLiteralTokenKinds();
#endif

        private readonly List<TextEdit> edits = [];

        public IReadOnlyList<TextEdit> Edits => this.edits;

        /// <summary>
        /// Gets a value indicating whether the query should be sanitized.
        /// </summary>
        /// <remarks>
        /// If the query is parameterized, we should skip sanitization.
        /// https://opentelemetry.io/docs/specs/semconv/database/database-spans/#sanitization-of-dbquerytext.
        /// </remarks>
        public bool ShouldSanitize { get; private set; } = true;

        public override void VisitDynamicExpression(DynamicExpression node) => this.edits.Add(CreatePlaceholder(node));

        public override void VisitSkippedTokens(SkippedTokens node) => this.RedactSkippedTokens(node);

        public override void VisitPrefixUnaryExpression(PrefixUnaryExpression node)
        {
            // Only strip the sign when it applies to a literal (e.g. -5 -> ?). For a non-literal operand
            // (e.g. -Column) removing the operator would change the meaning of the query in the sanitized text.
            if (node.Expression is { IsLiteral: true })
            {
                this.edits.Add(CreateRemoval(node.Operator));
            }

            base.VisitPrefixUnaryExpression(node);
        }

        public override void VisitQueryParametersStatement(QueryParametersStatement node) => this.ShouldSanitize = false;

        protected override void DefaultVisit(SyntaxNode node)
        {
            // Guard the recursive descent: deeply nested queries would otherwise overflow the stack.
            RuntimeHelpers.EnsureSufficientExecutionStack();

            // Sanitize all types of literal expressions, not just simple literals.
            if (node is Expression { IsLiteral: true } literal)
            {
                this.edits.Add(CreatePlaceholder(literal));
                return;
            }

            this.VisitChildren(node);
        }

#if NET
        private static FrozenSet<SyntaxKind> BuildLiteralTokenKinds()
#else
        private static HashSet<SyntaxKind> BuildLiteralTokenKinds()
#endif
        {
            var kinds = new HashSet<SyntaxKind>();
            foreach (var name in Enum.GetNames(typeof(SyntaxKind)))
            {
                if (name.EndsWith("LiteralToken", StringComparison.Ordinal))
                {
                    kinds.Add((SyntaxKind)Enum.Parse(typeof(SyntaxKind), name));
                }
            }

#if NET
            return kinds.ToFrozenSet();
#else
            return kinds;
#endif
        }

        private void VisitChildren(SyntaxNode node)
        {
            if (node != null)
            {
                for (var i = 0; i < node.ChildCount; i++)
                {
                    var child = node.GetChild(i);
                    if (child is SyntaxNode childNode)
                    {
                        childNode.Accept(this);
                    }
                    else if (child is SyntaxToken { Kind: SyntaxKind.InputTextToken } inputText)
                    {
                        // Raw inline data after "<|" (e.g. ".ingest inline" rows) is a single token, not a
                        // parsed expression, so the literal visitors never see it. Redact the whole payload.
                        this.edits.Add(CreatePlaceholder(inputText));
                    }
                }
            }
        }

        private void RedactSkippedTokens(SyntaxNode node)
        {
            // Guard the recursive descent over the skipped region's tokens.
            RuntimeHelpers.EnsureSufficientExecutionStack();

            for (var i = 0; i < node.ChildCount; i++)
            {
                var child = node.GetChild(i);
                if (child is SyntaxToken token)
                {
                    // In an unparsable region we cannot tell a field name from a value, so redact identifiers and
                    // barewords as well as literals. Punctuation, operators, and keywords are structural and kept.
                    if (token.Kind == SyntaxKind.IdentifierToken || LiteralTokenKinds.Contains(token.Kind))
                    {
                        this.edits.Add(CreatePlaceholder(token));
                    }
                }
                else if (child is SyntaxNode childNode)
                {
                    this.RedactSkippedTokens(childNode);
                }
            }
        }
    }

    /// <summary>
    /// Visitor that traverses the KQL to produce a summarized representation of the query.
    /// </summary>
    private sealed class SummarizerVisitor : DefaultSyntaxVisitor, IDisposable
    {
        private readonly TruncatingStringBuilder builder = new();

        // Names the query binds itself that can stand in for a table: let statements, function parameters and
        // 'as' aliases. A reference to one of these is not a table reference even when it sits in a table
        // position. Columns are deliberately not tracked, as they do not shadow tables in a table position.
        // The innermost scope is last: the query itself, then each function body, then each statement ('as'
        // aliases are only visible within the statement that declares them).
        private readonly List<HashSet<string>> scopes = [[]];

        public override void VisitPipeExpression(PipeExpression node)
        {
            // Pipe chains nest through the left expression and bypass DefaultVisit, so guard here too.
            RuntimeHelpers.EnsureSufficientExecutionStack();

            node.Expression.Accept(this);

            this.builder.Append(node.Bar.Text);
            this.builder.Append(' ');

            node.Operator.Accept(this);
        }

        public override void VisitLetStatement(LetStatement node)
        {
            // A let statement is not recursive, so its name is only bound once its expression has been visited.
            node.Expression.Accept(this);
            this.Declare(node.Name);
        }

        public override void VisitFunctionDeclaration(FunctionDeclaration node)
        {
            this.scopes.Add([]);
            try
            {
                // Declares the parameters, then visits the body.
                this.VisitChildren(node);
            }
            finally
            {
                this.scopes.RemoveAt(this.scopes.Count - 1);
            }
        }

        public override void VisitFunctionParameter(FunctionParameter node) => this.Declare(node.NameAndType.Name);

        public override void VisitExpressionStatement(ExpressionStatement node)
        {
            this.scopes.Add([]);
            try
            {
                this.VisitChildren(node);
            }
            finally
            {
                this.scopes.RemoveAt(this.scopes.Count - 1);
            }
        }

        public override void VisitNameDeclaration(NameDeclaration node)
        {
            if (node.Parent is AsOperator)
            {
                this.Declare(node);
            }
        }

        public override void VisitNameReference(NameReference node)
        {
            // The query is parsed without semantic analysis, so a name is classified
            // by where it appears rather than by what it resolves to.
            if (IsTableReference(node, out var qualified) && (qualified || !this.IsDeclared(node.SimpleName)))
            {
                this.builder.Append(node.SimpleName);
                this.builder.Append(' ');
            }
        }

        public override void VisitFunctionCallExpression(FunctionCallExpression node)
        {
            if (node.Name.SimpleName == "materialized_view")
            {
                this.builder.Append(node.ToString(IncludeTrivia.SingleLine));
                this.builder.Append(' ');
            }
        }

        public override void VisitDataTableExpression(DataTableExpression node)
        {
            this.builder.Append(node.DataTableKeyword.Text);
            this.builder.Append(' ');
        }

        public override void VisitCustomCommand(CustomCommand node)
        {
            this.builder.Append(node.DotToken.Text);
            this.builder.Append(node.Custom.GetFirstToken().Text);
            this.builder.Append(' ');
        }

        public string GetSummary()
        {
            this.builder.TrimEnd();
            return this.builder.ToString();
        }

        public void Dispose() => this.builder.Dispose();

        protected override void DefaultVisit(SyntaxNode node)
        {
            // Guard the recursive descent: deeply nested queries would otherwise overflow the stack.
            RuntimeHelpers.EnsureSufficientExecutionStack();

            if (node is QueryOperator qo)
            {
                this.VisitQueryOperator(qo);
            }
            else
            {
                this.VisitChildren(node);
            }
        }

        private static bool IsTableReference(NameReference node, out bool qualified)
        {
            // A name is taken as a table reference when it is the source of a query, a pipe, a function body or
            // a materialize(), or an operand of join, lookup, union, find or entity_group, optionally qualified
            // (database('db').Table) and/or parenthesized. A qualified name can never refer to a binding made
            // by the query itself.
            SyntaxNode expression = node;
            var parent = node.Parent;
            qualified = false;
            if (parent is PathExpression path && path.Selector == node)
            {
                qualified = true;
                expression = path;
                parent = path.Parent;
            }

            while (parent is ParenthesizedExpression)
            {
                expression = parent;
                parent = parent.Parent;
            }

            if (parent is SeparatedElement)
            {
                // Element of a comma-separated list: SeparatedElement -> SyntaxList -> owning node.
                return parent.Parent?.Parent is UnionOperator or FindInClause or EntityGroup;
            }

            return parent switch
            {
                PipeExpression pipe => pipe.Expression == expression,
                ExpressionStatement or LetStatement or JoinOperator or LookupOperator
                    or FunctionBody or MaterializeExpression or PartitionQuery => true,
                _ => false,
            };
        }

        private void Declare(NameDeclaration name) => this.scopes[this.scopes.Count - 1].Add(name.SimpleName);

        private bool IsDeclared(string name)
        {
            foreach (var scope in this.scopes)
            {
                if (scope.Contains(name))
                {
                    return true;
                }
            }

            return false;
        }

        private void VisitQueryOperator(QueryOperator node)
        {
            if (node is BadQueryOperator)
            {
                return;
            }

            this.builder.Append(node.GetFirstToken().ToString(IncludeTrivia.SingleLine));
            this.builder.Append(' ');

            this.VisitChildren(node);
        }

        private void VisitChildren(SyntaxNode node)
        {
            if (node != null)
            {
                for (var i = 0; i < node.ChildCount; i++)
                {
                    if (node.GetChild(i) is SyntaxNode child)
                    {
                        child.Accept(this);
                    }
                }
            }
        }
    }
}
