// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Kusto.Language;
using Kusto.Language.Editor;
using Kusto.Language.Symbols;
using Kusto.Language.Syntax;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Use the Kusto query language services to process Kusto queries for summarization and sanitization.
/// </summary>
internal static class KustoProcessor
{
    // Because we're not doing full semantic analysis for queries, we can reuse the default global state (which includes all built-in functions and types)
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

        KustoCode? code = null;

        // Note that order matters here as summarization requires semantic analysis to find potential table references,
        // but we want to avoid parsing twice if both are requested.
        if (shouldSummarize)
        {
            code = KustoCode.ParseAndAnalyze(query, KustoParserGlobalState);
            summarized = Summarize(code);
        }

        if (shouldSanitize)
        {
            code ??= KustoCode.Parse(query, KustoParserGlobalState);
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
        // Literal token kinds (StringLiteralToken, LongLiteralToken, ...) discovered once by name so any
        // future literal kind is covered automatically. Used to redact literals the parser left as loose
        // tokens inside a skipped (unparsable) region of a malformed query.
        private static readonly HashSet<SyntaxKind> LiteralTokenKinds = BuildLiteralTokenKinds();

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

        public override void VisitSkippedTokens(SkippedTokens node) => this.RedactLiteralTokens(node);

        public override void VisitPrefixUnaryExpression(PrefixUnaryExpression node)
        {
            this.edits.Add(CreateRemoval(node.Operator));
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

        private static HashSet<SyntaxKind> BuildLiteralTokenKinds()
        {
            var kinds = new HashSet<SyntaxKind>();
            foreach (var name in Enum.GetNames(typeof(SyntaxKind)))
            {
                if (name.EndsWith("LiteralToken", StringComparison.Ordinal))
                {
                    kinds.Add((SyntaxKind)Enum.Parse(typeof(SyntaxKind), name));
                }
            }

            return kinds;
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

        private void RedactLiteralTokens(SyntaxNode node)
        {
            // Guard the recursive descent over the skipped region's tokens.
            RuntimeHelpers.EnsureSufficientExecutionStack();

            for (var i = 0; i < node.ChildCount; i++)
            {
                var child = node.GetChild(i);
                if (child is SyntaxToken token)
                {
                    if (LiteralTokenKinds.Contains(token.Kind))
                    {
                        this.edits.Add(CreatePlaceholder(token));
                    }
                }
                else if (child is SyntaxNode childNode)
                {
                    this.RedactLiteralTokens(childNode);
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

        public override void VisitPipeExpression(PipeExpression node)
        {
            // Pipe chains nest through the left expression and bypass DefaultVisit, so guard here too.
            RuntimeHelpers.EnsureSufficientExecutionStack();

            node.Expression.Accept(this);

            this.builder.Append(node.Bar.Text);
            this.builder.Append(' ');

            node.Operator.Accept(this);
        }

        public override void VisitNameReference(NameReference node)
        {
            if (node.ResultType is TableSymbol ts)
            {
                this.builder.Append(ts.Name);
                this.builder.Append(' ');
            }
            else if (node.ResultType is ErrorSymbol)
            {
                this.builder.Append(node.ToString(IncludeTrivia.SingleLine));
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
