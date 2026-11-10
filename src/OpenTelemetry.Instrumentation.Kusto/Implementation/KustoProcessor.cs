// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
#if NET
using System.Collections.Frozen;
#endif
using System.Runtime.CompilerServices;
using System.Text;
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

    /// <summary>
    /// Processes the specified Kusto query and optionally generates a summary and/or a sanitized version based on the
    /// provided options.
    /// </summary>
    /// <remarks>
    /// The query is parsed only if at least one of summarization or sanitization is requested, and only once if both
    /// are. The returned <see cref="KustoStatementInfo"/> will have null values for summary or sanitized output
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
        if (!shouldSummarize && !shouldSanitize)
        {
            return default;
        }

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
        // emitting it unsanitized. The visitor collects the edits in document order without overlap, so they
        // are applied in a single pass here. EditString.ApplyAll is not used because it merges touching edits
        // with List.RemoveAt, which is quadratic in the number of edits.
        var text = code.Text;
        var builder = new StringBuilder(text.Length);
        var position = 0;
        foreach (var edit in edits)
        {
            if (edit.Start < position || edit.Start + edit.DeleteLength > text.Length)
            {
                sanitized = null;
                return false;
            }

            builder.Append(text, position, edit.Start - position);
            builder.Append(edit.InsertText);
            position = edit.Start + edit.DeleteLength;
        }

        builder.Append(text, position, text.Length - position);
        sanitized = builder.ToString();
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

    /// <summary>
    /// Visitor that traverses the KQL looking for literal values to replace with the PLACEHOLDER value.
    /// </summary>
    private sealed class SanitizerVisitor : DefaultSyntaxVisitor
    {
        // Literal token kinds (StringLiteralToken, LongLiteralToken, ...) discovered once by name so any future
        // literal kind is covered automatically. Used to redact literal tokens a node holds directly, and
        // (together with identifier tokens) value-bearing tokens the parser left loose inside a skipped
        // (unparsable) region of a malformed query or an unknown control command.
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

        // A control command whose leading keyword the grammar does not know is kept as a flat token list
        // without any syntax error, so the literal visitors never see its values. Treat it as the unparsable
        // region it is and apply the same policy as for skipped tokens.
        public override void VisitUnknownCommand(UnknownCommand node) => this.RedactSkippedTokens(node);

        public override void VisitPrefixUnaryExpression(PrefixUnaryExpression node)
        {
            // Only strip the sign when it applies to a literal (e.g. -5 -> ?). For a non-literal operand
            // (e.g. -Column) removing the operator would change the meaning of the query in the sanitized text.
            // The sign and the literal are redacted together as a single edit.
            if (node.Expression is { IsLiteral: true })
            {
                this.edits.Add(CreatePlaceholder(node));
                return;
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
                    else if (child is SyntaxToken token
                        && (token.Kind == SyntaxKind.InputTextToken || LiteralTokenKinds.Contains(token.Kind)))
                    {
                        // Raw inline data after "<|" (e.g. ".ingest inline" rows) is a single token, not a
                        // parsed expression, so the literal visitors never see it. Redact the whole payload.
                        // The same applies to a literal token a node holds directly (e.g. the boolean in a
                        // control command) rather than wrapped in a literal expression.
                        this.edits.Add(CreatePlaceholder(token));
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
        // The innermost scope is last: the query itself, then each function body and partition operand. An 'as'
        // alias stays visible to the rest of the enclosing scope, including later statements and the subqueries
        // of operators such as join and union.
        private readonly List<HashSet<string>> scopes = [[]];

        public override void VisitPipeExpression(PipeExpression node)
        {
            // Pipe chains nest through the left expression and bypass DefaultVisit, so guard here too.
            RuntimeHelpers.EnsureSufficientExecutionStack();

            node.Expression.Accept(this);

            if (this.builder.IsTruncated)
            {
                return;
            }

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

        // Declares the parameters, then visits the body.
        public override void VisitFunctionDeclaration(FunctionDeclaration node) => this.VisitScoped(node);

        public override void VisitPartitionQuery(PartitionQuery node) => this.VisitScoped(node);

        public override void VisitPartitionSubquery(PartitionSubquery node) => this.VisitScoped(node);

        // Each match of a pattern has its own body, whose statements are not visible outside it.
        public override void VisitPatternMatch(PatternMatch node) => this.VisitScoped(node);

        public override void VisitMacroExpandOperator(MacroExpandOperator node)
        {
            // The statements in the body are not visible after it. This is an operator, so it is also summarized.
            RuntimeHelpers.EnsureSufficientExecutionStack();

            this.scopes.Add([]);
            try
            {
                this.VisitQueryOperator(node);
            }
            finally
            {
                this.scopes.RemoveAt(this.scopes.Count - 1);
            }
        }

        public override void VisitFunctionParameter(FunctionParameter node)
        {
            // FunctionParameter -> SeparatedElement -> SyntaxList -> owning node. The declarations of a scan
            // operator share this syntax, but they are columns added to the input schema, not parameters.
            if (node.Parent?.Parent?.Parent is not ScanDeclareClause)
            {
                this.Declare(node.NameAndType.Name);
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
            // Once the summary is full nothing more can be appended, so skip the (potentially expensive) rendering.
            if (this.builder.IsTruncated)
            {
                return;
            }

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
            // Once the summary is full nothing more can be appended, so skip the (potentially expensive) rendering.
            if (!this.builder.IsTruncated && node.Name.SimpleName == "materialized_view")
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
            if (this.builder.IsTruncated)
            {
                return;
            }

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
                // The same syntax is used to access a member of a scalar (a property of a dynamic value,
                // for example), which is never a table name, so only a path that starts with database() or
                // cluster() can be a qualified table reference.
                if (!IsEntityQualifier(path.Expression))
                {
                    return false;
                }

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

        private static bool IsEntityQualifier(Expression expression) => expression switch
        {
            FunctionCallExpression call => call.Name.SimpleName is "database" or "cluster",
            ParenthesizedExpression parenthesized => IsEntityQualifier(parenthesized.Expression),
            PathExpression { Selector: FunctionCallExpression call } path => IsEntityQualifier(call) && IsEntityQualifier(path.Expression),
            _ => false,
        };

        private void VisitScoped(SyntaxNode node)
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
                // Stop walking as soon as the summary is full: nothing further can be appended.
                for (var i = 0; i < node.ChildCount && !this.builder.IsTruncated; i++)
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
