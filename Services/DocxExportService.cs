using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;

namespace LegalDraftAI.Services;

public sealed class DocxExportService : IDocxExportService
{
    private static readonly HashSet<string> Headings = new(StringComparer.OrdinalIgnoreCase)
    {
        "IN THE MATTER OF:",
        "PARTIES",
        "1. PARTIES",
        "2. FACTS OF THE CASE",
        "LEGAL GROUNDS",
        "SCHEDULE OF SUIT PROPERTY",
        "3. CAUSE OF ACTION",
        "4. JURISDICTION",
        "5. LIMITATION",
        "6. COURT FEE",
        "7. RELIEFS SOUGHT",
        "MOST RESPECTFULLY SHOWETH:",
        "FACTUAL BACKGROUND",
        "GROUNDS",
        "DOCUMENTS RELIED ON",
        "SUPPORTING AFFIDAVIT",
        "PRAYER",
        "VERIFICATION"
    };

    private static readonly Regex PlaceholderPattern = new(
        @"\{\{\s*(?<name>[^{}]+?)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public byte[] GenerateDocx(string text) => CreateCivilPlaintDocx(text);

    public byte[] GenerateCivilPlaintDocx(
        string text,
        byte[]? templateDocx = null,
        IReadOnlyDictionary<string, string>? templateValues = null)
    {
        return templateDocx is null
            ? CreateCivilPlaintDocx(text)
            : MergeCivilPlaintIntoTemplate(templateDocx, templateValues ?? new Dictionary<string, string>());
    }

    private static byte[] CreateCivilPlaintDocx(string text)
    {
        using var stream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(
                   stream,
                   WordprocessingDocumentType.Document,
                   autoSave: true))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            var body = new Body();
            mainPart.Document = new Document(body);

            foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal)
                         .Replace('\r', '\n')
                         .Split('\n'))
            {
                body.Append(CreateParagraph(line));
            }

            body.Append(new SectionProperties(
                new PageSize
                {
                    Width = 11906U,
                    Height = 16838U
                },
                new PageMargin
                {
                    Top = 1134,
                    Right = 1134U,
                    Bottom = 1134,
                    Left = 1134U,
                    Header = 567U,
                    Footer = 567U,
                    Gutter = 0U
                }));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static byte[] MergeCivilPlaintIntoTemplate(
        byte[] templateDocx,
        IReadOnlyDictionary<string, string> templateValues)
    {
        using var stream = new MemoryStream();
        stream.Write(templateDocx);
        stream.Position = 0;
        using (var wordDocument = WordprocessingDocument.Open(stream, true))
        {
            var mainDocumentPart = wordDocument.MainDocumentPart
                ?? throw new OpenXmlPackageException("The DOCX template does not contain a main document part.");
            var document = mainDocumentPart.Document
                ?? throw new OpenXmlPackageException("The DOCX template does not contain a document.");
            var body = document.Body
                ?? throw new OpenXmlPackageException("The DOCX template does not contain a document body.");

            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                ReplacePlaceholders(paragraph, templateValues);
            }
            foreach (var header in mainDocumentPart.HeaderParts)
            {
                var headerContent = header.Header
                    ?? throw new OpenXmlPackageException("The DOCX template contains an invalid header part.");
                foreach (var paragraph in headerContent.Descendants<Paragraph>())
                    ReplacePlaceholders(paragraph, templateValues);
                headerContent.Save();
            }
            foreach (var footer in mainDocumentPart.FooterParts)
            {
                var footerContent = footer.Footer
                    ?? throw new OpenXmlPackageException("The DOCX template contains an invalid footer part.");
                foreach (var paragraph in footerContent.Descendants<Paragraph>())
                    ReplacePlaceholders(paragraph, templateValues);
                footerContent.Save();
            }

            document.Save();
        }

        return stream.ToArray();
    }

    private static void ReplacePlaceholders(
        Paragraph paragraph,
        IReadOnlyDictionary<string, string> templateValues)
    {
        var textNodes = paragraph.Descendants<Text>().ToList();
        if (textNodes.Count == 0)
            return;

        var originalTexts = textNodes.Select(node => node.Text ?? string.Empty).ToArray();
        var combinedText = string.Concat(originalTexts);
        var matches = PlaceholderPattern.Matches(combinedText);
        if (matches.Count == 0)
            return;

        var offsetDelta = 0;
        foreach (Match match in matches.Cast<Match>())
        {
            var key = match.Groups["name"].Value.Trim();
            templateValues.TryGetValue(key, out var replacement);
            replacement = (replacement ?? string.Empty)
                .Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace('\r', ' ')
                .Replace('\n', ' ');

            var currentTexts = textNodes.Select(node => node.Text ?? string.Empty).ToArray();
            var nodeStarts = new int[currentTexts.Length];
            for (var index = 1; index < currentTexts.Length; index++)
                nodeStarts[index] = nodeStarts[index - 1] + currentTexts[index - 1].Length;

            var matchStart = match.Index + offsetDelta;
            var matchEnd = matchStart + match.Length;
            var firstNode = FindTextNode(nodeStarts, currentTexts, matchStart);
            var lastNode = FindTextNode(nodeStarts, currentTexts, matchEnd - 1);
            var firstOffset = matchStart - nodeStarts[firstNode];
            var lastOffset = matchEnd - nodeStarts[lastNode];

            if (firstNode == lastNode)
            {
                textNodes[firstNode].Text =
                    currentTexts[firstNode][..firstOffset] +
                    replacement +
                    currentTexts[firstNode][lastOffset..];
                textNodes[firstNode].Space = SpaceProcessingModeValues.Preserve;
            }
            else
            {
                textNodes[firstNode].Text = currentTexts[firstNode][..firstOffset] + replacement;
                textNodes[firstNode].Space = SpaceProcessingModeValues.Preserve;
                for (var index = firstNode + 1; index < lastNode; index++)
                    textNodes[index].Text = string.Empty;
                textNodes[lastNode].Text = currentTexts[lastNode][lastOffset..];
                textNodes[lastNode].Space = SpaceProcessingModeValues.Preserve;
            }

            offsetDelta += replacement.Length - match.Length;
        }
    }

    private static int FindTextNode(int[] nodeStarts, string[] texts, int characterIndex)
    {
        for (var index = nodeStarts.Length - 1; index >= 0; index--)
        {
            if (characterIndex >= nodeStarts[index] &&
                (characterIndex < nodeStarts[index] + texts[index].Length ||
                 texts[index].Length == 0 && characterIndex == nodeStarts[index]))
            {
                return index;
            }
        }

        throw new InvalidOperationException("Could not map a DOCX placeholder to its text run.");
    }

    private static Paragraph CreateParagraph(string line)
    {
        var paragraph = new Paragraph();
        var properties = new ParagraphProperties(
            new SpacingBetweenLines
            {
                After = "120",
                Line = "300",
                LineRule = LineSpacingRuleValues.Auto
            });
        var normalizedLine = line.Trim();
        if (normalizedLine.StartsWith("APPLICATION ", StringComparison.OrdinalIgnoreCase))
        {
            properties.Append(new Justification { Val = JustificationValues.Center });
            paragraph.Append(properties);
            paragraph.Append(new Run(
                new RunProperties(
                    new Bold(),
                    new FontSize { Val = "24" }),
                new Text(normalizedLine)
                {
                    Space = SpaceProcessingModeValues.Preserve
                }));
            return paragraph;
        }

        if (Headings.Contains(normalizedLine))
        {
            if (normalizedLine.Equals("IN THE MATTER OF:", StringComparison.OrdinalIgnoreCase))
                properties.Append(new Justification { Val = JustificationValues.Center });
            properties.Append(new KeepNext());
            paragraph.Append(properties);
            paragraph.Append(new Run(
                new RunProperties(
                    new Bold(),
                    new FontSize { Val = "24" }),
                new Text(normalizedLine)
                {
                    Space = SpaceProcessingModeValues.Preserve
                }));
            return paragraph;
        }

        if (normalizedLine.StartsWith("IN THE COURT OF ", StringComparison.OrdinalIgnoreCase) ||
            normalizedLine.Equals("VERSUS", StringComparison.OrdinalIgnoreCase))
        {
            properties.Append(new Justification { Val = JustificationValues.Center });
            paragraph.Append(properties);
            paragraph.Append(new Run(
                new RunProperties(new Bold()),
                new Text(normalizedLine)
                {
                    Space = SpaceProcessingModeValues.Preserve
                }));
            return paragraph;
        }

        paragraph.Append(properties);
        paragraph.Append(new Run(new Text(line)
        {
            Space = SpaceProcessingModeValues.Preserve
        }));
        return paragraph;
    }
}
