using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using LegalAssistant.Application.Documents.Services;

namespace LegalAssistant.Infrastructure.Documents;

public sealed class StructuredHtmlToTextConverter : IHtmlToTextConverter
{
    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "article", "aside", "blockquote", "div", "dl", "dt", "dd", "figcaption", "figure",
        "footer", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr", "li", "main",
        "nav", "p", "pre", "section", "table", "tbody", "thead", "tfoot", "tr"
    };

    private static readonly HashSet<string> SkipTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "noscript", "template"
    };

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex BlankLineRegex = new(@"\n{3,}", RegexOptions.Compiled);

    public string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var doc = new HtmlDocument();
        doc.OptionFixNestedTags = true;
        doc.LoadHtml(html);

        RemoveUnwantedNodes(doc);

        var root = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        var sb = new StringBuilder();

        foreach (var child in root.ChildNodes)
            WriteNode(child, sb, listDepth: 0);

        return NormalizeOutput(sb.ToString());
    }

    private static void RemoveUnwantedNodes(HtmlDocument doc)
    {
        var nodes = doc.DocumentNode.SelectNodes("//script|//style|//noscript|//template");
        if (nodes is null)
            return;

        foreach (var node in nodes)
            node.Remove();
    }

    private static void WriteNode(HtmlNode node, StringBuilder sb, int listDepth)
    {
        if (node.NodeType == HtmlNodeType.Comment)
            return;

        if (node.NodeType == HtmlNodeType.Text)
        {
            AppendInlineText(sb, HtmlEntity.DeEntitize(node.InnerText));
            return;
        }

        var tag = node.Name;
        if (SkipTags.Contains(tag))
            return;

        switch (tag.ToLowerInvariant())
        {
            case "html":
            case "body":
            case "thead":
            case "tbody":
            case "tfoot":
            case "tr":
                foreach (var child in node.ChildNodes)
                    WriteNode(child, sb, listDepth);
                if (tag.Equals("tr", StringComparison.OrdinalIgnoreCase))
                    AppendBlockBreak(sb);
                return;

            case "br":
                AppendLineBreak(sb);
                return;

            case "ul":
            case "ol":
                foreach (var li in node.Elements("li"))
                    WriteListItem(li, sb, listDepth + 1, isOrdered: tag.Equals("ol", StringComparison.OrdinalIgnoreCase));
                return;

            case "li":
                WriteListItem(node, sb, listDepth, isOrdered: IsOrderedListItem(node));
                return;

            case "table":
                WriteTable(node, sb, listDepth);
                return;

            case "td":
            case "th":
                foreach (var child in node.ChildNodes)
                    WriteNode(child, sb, listDepth);
                return;
        }

        if (BlockTags.Contains(tag))
            AppendBlockBreak(sb);

        foreach (var child in node.ChildNodes)
            WriteNode(child, sb, listDepth);

        if (BlockTags.Contains(tag))
            AppendBlockBreak(sb);
    }

    private static void WriteListItem(HtmlNode li, StringBuilder sb, int listDepth, bool isOrdered)
    {
        AppendBlockBreak(sb);

        var prefix = GetListPrefix(li, listDepth, isOrdered);
        AppendInlineText(sb, prefix);

        foreach (var child in li.ChildNodes)
        {
            if (child.Name.Equals("ul", StringComparison.OrdinalIgnoreCase) ||
                child.Name.Equals("ol", StringComparison.OrdinalIgnoreCase))
            {
                AppendBlockBreak(sb);
                WriteNode(child, sb, listDepth + 1);
                continue;
            }

            WriteNode(child, sb, listDepth);
        }

        AppendBlockBreak(sb);
    }

    private static void WriteTable(HtmlNode table, StringBuilder sb, int listDepth)
    {
        AppendBlockBreak(sb);

        foreach (var row in table.Descendants("tr"))
        {
            var cells = row.Elements("th").Concat(row.Elements("td")).ToList();
            if (cells.Count == 0)
            {
                foreach (var child in row.ChildNodes)
                    WriteNode(child, sb, listDepth);
                continue;
            }

            var line = new StringBuilder();
            for (var i = 0; i < cells.Count; i++)
            {
                if (i > 0)
                    line.Append(" | ");

                line.Append(ExtractInlineText(cells[i]));
            }

            AppendLine(sb, line.ToString());
        }

        AppendBlockBreak(sb);
    }

    private static string ExtractInlineText(HtmlNode node)
    {
        var sb = new StringBuilder();
        foreach (var child in node.ChildNodes)
            WriteInlineNode(child, sb);

        return NormalizeInlineText(sb.ToString());
    }

    private static void WriteInlineNode(HtmlNode node, StringBuilder sb)
    {
        if (node.NodeType == HtmlNodeType.Comment)
            return;

        if (node.NodeType == HtmlNodeType.Text)
        {
            AppendInlineText(sb, HtmlEntity.DeEntitize(node.InnerText));
            return;
        }

        var tag = node.Name;
        if (SkipTags.Contains(tag))
            return;

        if (tag.Equals("br", StringComparison.OrdinalIgnoreCase))
        {
            AppendLineBreak(sb);
            return;
        }

        foreach (var child in node.ChildNodes)
            WriteInlineNode(child, sb);
    }

    private static bool IsOrderedListItem(HtmlNode li)
        => li.ParentNode is { Name: "ol" } or { Name: "OL" };

    private static string GetListPrefix(HtmlNode li, int listDepth, bool isOrdered)
    {
        var indentation = new string(' ', Math.Max(0, listDepth - 1) * 2);
        if (!isOrdered)
            return indentation + "- ";

        var siblings = li.ParentNode?.Elements("li").ToList();
        var index = siblings?.FindIndex(x => ReferenceEquals(x, li)) ?? -1;
        var number = index >= 0 ? index + 1 : 1;
        return indentation + number + ". ";
    }

    private static void AppendInlineText(StringBuilder sb, string text)
    {
        text = NormalizeInlineText(text);
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (sb.Length > 0)
        {
            var last = sb[^1];
            if (last != '\n' && last != ' ' && last != '\t')
                sb.Append(' ');
        }

        sb.Append(text);
    }

    private static void AppendLineBreak(StringBuilder sb)
    {
        if (sb.Length == 0)
            return;

        if (sb[^1] != '\n')
            sb.Append('\n');
    }

    private static void AppendLine(StringBuilder sb, string line)
    {
        line = NormalizeInlineText(line);
        if (string.IsNullOrWhiteSpace(line))
            return;

        AppendBlockBreak(sb);
        sb.Append(line);
        AppendBlockBreak(sb);
    }

    private static void AppendBlockBreak(StringBuilder sb)
    {
        if (sb.Length == 0)
            return;

        while (sb.Length > 0 && (sb[^1] == ' ' || sb[^1] == '\t' || sb[^1] == '\r'))
            sb.Length--;

        if (sb.Length == 0)
            return;

        if (sb[^1] == '\n')
        {
            if (sb.Length >= 2 && sb[^2] == '\n')
                return;

            sb.Append('\n');
            return;
        }

        sb.AppendLine();
        sb.AppendLine();
    }

    private static string NormalizeInlineText(string text)
        => WhitespaceRegex.Replace(HtmlEntity.DeEntitize(text ?? string.Empty), " ").Trim();

    private static string NormalizeOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        text = Regex.Replace(text, "[ \t]+\n", "\n");
        text = Regex.Replace(text, "\n[ \t]+", "\n");
        text = BlankLineRegex.Replace(text, "\n\n");
        return text.Trim();
    }
}
