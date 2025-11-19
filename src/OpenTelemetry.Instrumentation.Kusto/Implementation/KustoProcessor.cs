// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Kusto.Language;
using Kusto.Language.Editor;
using Kusto.Language.Symbols;
using Kusto.Language.Syntax;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal static class KustoProcessor
{
    private enum ReplacementKind
    {
        Placeholder,
        Remove,
    }

    public static KustoStatementInfo Process(bool shouldSummarize, bool shouldSanitize, string query)
    {
        var code = KustoCode.ParseAndAnalyze(query);

        string? summarized = null;
        string? sanitized = null;

        if (shouldSummarize)
        {
            summarized = Summarize(code);
        }

        if (shouldSanitize)
        {
            sanitized = Sanitize(code);
        }

        return new KustoStatementInfo(summarized, sanitized);
    }

    private static string Sanitize(KustoCode code)
    {
        // Collect nodes that need replacements
        var collector = new SanitizerVisitor();
        code.Syntax.Accept(collector);

        // Build edits
        var edits = new List<TextEdit>();
        foreach (var replacement in collector.Replacements)
        {
            var edit = replacement.Kind switch
            {
                ReplacementKind.Placeholder => TextEdit.Replacement(replacement.Element.TextStart, replacement.Element.Width, "?"),
                ReplacementKind.Remove => TextEdit.Deletion(replacement.Element.TextStart, replacement.Element.Width),
                _ => throw new NotSupportedException($"Unexpected replacement kind: {replacement.Kind}"),
            };

            edits.Add(edit);
        }

        // Apply edits to text
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
        var walker = new SummarizerVisitor();
        code.Syntax.Accept(walker);

        var sb = new StringBuilder();
        foreach (var segment in walker.Builder)
        {
            sb.Append(segment).Append(' ');
        }

        sb.TrimEnd();
        return sb.ToString(0, Math.Min(255, sb.Length));
    }

    private readonly struct Replacement
    {
        public Replacement(SyntaxElement element, ReplacementKind kind)
        {
            this.Element = element;
            this.Kind = kind;
        }

        public SyntaxElement Element { get; }

        public ReplacementKind Kind { get; }
    }

    private sealed class SanitizerVisitor : DefaultSyntaxVisitor
    {
        public readonly List<Replacement> Replacements = [];

        public override void VisitLiteralExpression(LiteralExpression node) => this.Replacements.Add(new Replacement(node, ReplacementKind.Placeholder));

        public override void VisitDynamicExpression(DynamicExpression node) => this.Replacements.Add(new Replacement(node, ReplacementKind.Placeholder));

        public override void VisitPrefixUnaryExpression(PrefixUnaryExpression node)
        {
            this.Replacements.Add(new Replacement(node.Operator, ReplacementKind.Remove));
            base.VisitPrefixUnaryExpression(node);
        }

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

    private sealed class SummarizerVisitor : DefaultSyntaxVisitor
    {
        public readonly List<string> Builder = [];

        public override void VisitPipeExpression(PipeExpression node)
        {
            node.Expression.Accept(this);
            this.Builder.Add(node.Bar.Text);
            node.Operator.Accept(this);
        }

        public override void VisitNameReference(NameReference node)
        {
            if (node.ResultType is TableSymbol ts)
            {
                this.Builder.Add(ts.Name);
            }
            else if (node.ResultType is ErrorSymbol)
            {
                this.Builder.Add(node.ToString(IncludeTrivia.SingleLine));
            }
        }

        public override void VisitFunctionCallExpression(FunctionCallExpression node)
        {
            if (node.Name.SimpleName == "materialized_view")
            {
                this.Builder.Add(node.ToString(IncludeTrivia.SingleLine));
            }
        }

        public override void VisitDataTableExpression(DataTableExpression node) => this.Builder.Add(node.DataTableKeyword.Text);

        public override void VisitCustomCommand(CustomCommand node) => this.Builder.Add(node.DotToken + node.Custom.GetFirstToken().ToString(IncludeTrivia.SingleLine));

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

            this.Builder.Add(node.GetFirstToken().ToString(IncludeTrivia.SingleLine));

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
