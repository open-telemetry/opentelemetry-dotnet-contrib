// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Kusto.Language;
using Kusto.Language.Editor;
using Kusto.Language.Symbols;
using Kusto.Language.Syntax;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal static class KustoProcessor
{
    private static readonly GlobalState KustoParserGlobalState = GlobalState.Default.WithCache();

    private enum ReplacementKind
    {
        Placeholder,
        Remove,
    }

    public static KustoStatementInfo Process(bool shouldSummarize, bool shouldSanitize, ReadOnlySpan<char> query)
    {
        string? summarized = null;
        string? sanitized = null;

        KustoCode? code = null;

        // Note that order matters here as summarization requires semantic analysis, but we want to avoid parsing twice if both are requested.
        if (shouldSummarize)
        {
            code ??= KustoCode.ParseAndAnalyze(query.ToString(), KustoParserGlobalState);
            summarized = Summarize(code);
        }

        if (shouldSanitize)
        {
            code ??= KustoCode.Parse(query.ToString(), KustoParserGlobalState);
            sanitized = Sanitize(code);
        }

        return new KustoStatementInfo(summarized, sanitized);
    }

    private static string Sanitize(KustoCode code)
    {
        // Collect nodes that need replacements
        var collector = new SanitizerVisitor();
        code.Syntax.Accept(collector);

        if (!collector.ShouldSanitize)
        {
            return code.Text;
        }

        // Apply edits to text
        var edits = collector.Edits;
        var text = new EditString(code.Text);
        if (edits.Count == 0 || !text.CanApplyAll(edits))
        {
            return code.Text;
        }

        var newText = text.ApplyAll(edits);
        return newText;
    }

    private static string Summarize(KustoCode code)
    {
        using var walker = new SummarizerVisitor();
        code.Syntax.Accept(walker);
        return walker.GetSummary();
    }

    private static TextEdit CreatePlaceholder(SyntaxElement node) => TextEdit.Replacement(node.TextStart, node.Width, "?");

    private static TextEdit CreateRemoval(SyntaxElement node) => TextEdit.Deletion(node.TextStart, node.Width);

    private sealed class SanitizerVisitor : DefaultSyntaxVisitor
    {
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

        public override void VisitLiteralExpression(LiteralExpression node) => this.edits.Add(CreatePlaceholder(node));

        public override void VisitDynamicExpression(DynamicExpression node) => this.edits.Add(CreatePlaceholder(node));

        public override void VisitPrefixUnaryExpression(PrefixUnaryExpression node)
        {
            this.edits.Add(CreateRemoval(node.Operator));
            base.VisitPrefixUnaryExpression(node);
        }

        public override void VisitQueryParametersStatement(QueryParametersStatement node) => this.ShouldSanitize = false;

        protected override void DefaultVisit(SyntaxNode node) => this.VisitChildren(node);

        private void VisitChildren(SyntaxNode node)
        {
            if (node != null)
            {
                for (int i = 0; i < node.ChildCount; i++)
                {
                    if (node.GetChild(i) is SyntaxNode child)
                    {
                        child.Accept(this);
                    }
                }
            }
        }
    }

    private sealed class SummarizerVisitor : DefaultSyntaxVisitor, IDisposable
    {
        private readonly TruncatingStringBuilder builder = new();

        public override void VisitPipeExpression(PipeExpression node)
        {
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
                for (int i = 0; i < node.ChildCount; i++)
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
