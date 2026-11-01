using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Pix;
using UglyToad.PdfPig;

namespace LegalDraftAI.Services;

public class DocumentExtractionService : IDocumentExtractionService
{
    public async Task<string> ExtractTextAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        await using var stream = file.OpenReadStream();

        return extension switch
        {
            ".pdf" => await ExtractFromPdfAsync(stream, ct),
            ".docx" => await ExtractFromDocxAsync(stream, ct),
            ".txt" => await new StreamReader(stream).ReadToEndAsync(ct),
            ".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff" or ".bmp" or ".gif" or ".webp" =>
                await ExtractFromImageAsync(stream, file.FileName, ct),
            _ => throw new NotSupportedException(
                $"File type '{extension}' is not supported. Supported formats are PDF, DOCX, TXT, PNG, JPG, TIFF, BMP, GIF, and WebP.")
        };
    }

    public async Task<string> ExtractTextFromMultipleAsync(
        IEnumerable<IFormFile> files, CancellationToken ct = default)
    {
        var parts = new List<string>();
        int index = 1;

        foreach (var file in files)
        {
            var text = await ExtractTextAsync(file, ct);
            parts.Add($"=== Document {index}: {file.FileName} ===\n{text}\n");
            index++;
        }

        return string.Join("\n", parts);
    }

    public async Task<string> ExtractTextFromStreamAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => await ExtractFromPdfAsync(stream, ct),
            ".docx" => await ExtractFromDocxAsync(stream, ct),
            ".txt" => await new StreamReader(stream).ReadToEndAsync(ct),
            ".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff" or ".bmp" or ".gif" or ".webp" =>
                await ExtractFromImageAsync(stream, fileName, ct),
            _ => throw new NotSupportedException(
                $"File type '{extension}' is not supported. Supported formats are PDF, DOCX, TXT, PNG, JPG, TIFF, BMP, GIF, and WebP.")
        };
    }

    private static async Task<string> ExtractFromImageAsync(
        Stream stream,
        string fileName,
        CancellationToken ct)
    {
        using var imageData = new MemoryStream();
        await stream.CopyToAsync(imageData, ct);
        ct.ThrowIfCancellationRequested();
        var imageBytes = imageData.ToArray();
        var imageFormat = DetectImageFormat(imageBytes);
        if (imageFormat is null)
        {
            throw new InvalidDataException(
                $"'{Path.GetFileName(fileName)}' is not a readable PNG, JPEG, TIFF, BMP, GIF, or WebP image. If it is a HEIC/HEIF image, convert it to JPEG or PNG and upload it again.");
        }

        var extension = Path.GetExtension(fileName);
        if (!IsMatchingImageExtension(imageFormat, extension))
        {
            throw new InvalidDataException(
                $"'{Path.GetFileName(fileName)}' has a .{extension.TrimStart('.')} extension but contains {imageFormat} data. Rename or convert it to match its actual image format and upload it again.");
        }

        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "tessdata");
        var missingLanguages = new[] { "eng", "hin" }
            .Where(language => !File.Exists(Path.Combine(dataDirectory, $"{language}.traineddata")))
            .ToArray();
        if (missingLanguages.Length > 0)
        {
            throw new InvalidOperationException(
                $"OCR language data is missing from '{dataDirectory}': {string.Join(", ", missingLanguages)}. Deploy the English and Hindi Tesseract traineddata files with the API.");
        }

        return await Task.Run(() =>
        {
            using var engine = new Engine(dataDirectory, "eng+hin", EngineMode.Default);
            using var image = LoadImage(imageBytes, fileName);
            using var page = engine.Process(image);
            return page.Text.Trim();
        }, ct);
    }

    private static Image LoadImage(byte[] imageBytes, string fileName)
    {
        try
        {
            return Image.LoadFromMemory(imageBytes);
        }
        catch (IOException exception)
        {
            throw new InvalidDataException(
                $"Could not decode '{Path.GetFileName(fileName)}' as an image. Re-save it as PNG or JPEG and try again.",
                exception);
        }
    }

    private static string? DetectImageFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "PNG";
        if (bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
            return "JPEG";
        if (bytes.StartsWith(new byte[] { 0x49, 0x49, 0x2A, 0x00 }) ||
            bytes.StartsWith(new byte[] { 0x4D, 0x4D, 0x00, 0x2A }) ||
            bytes.StartsWith(new byte[] { 0x49, 0x49, 0x2B, 0x00 }) ||
            bytes.StartsWith(new byte[] { 0x4D, 0x4D, 0x00, 0x2B }))
        {
            return "TIFF";
        }
        if (bytes.StartsWith("BM"u8))
            return "BMP";
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8))
            return "GIF";
        if (bytes.Length >= 12 &&
            bytes.StartsWith("RIFF"u8) &&
            bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "WebP";
        }
        if (bytes.Length >= 12 &&
            bytes.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = System.Text.Encoding.ASCII.GetString(bytes.Slice(8, 4));
            if (brand.StartsWith("hei", StringComparison.OrdinalIgnoreCase) ||
                brand.StartsWith("mif", StringComparison.OrdinalIgnoreCase))
            {
                return "HEIC/HEIF";
            }
        }

        return null;
    }

    private static bool IsMatchingImageExtension(string imageFormat, string extension) =>
        imageFormat switch
        {
            "PNG" => extension.Equals(".png", StringComparison.OrdinalIgnoreCase),
            "JPEG" => extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase),
            "TIFF" => extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase),
            "BMP" => extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase),
            "GIF" => extension.Equals(".gif", StringComparison.OrdinalIgnoreCase),
            "WebP" => extension.Equals(".webp", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    private static async Task<string> ExtractFromPdfAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;

        using var document = PdfDocument.Open(ms);
        var sb = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    private static async Task<string> ExtractFromDocxAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;

        using var wordDoc = WordprocessingDocument.Open(ms, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;

        if (body == null)
            return string.Empty;

        var sb = new StringBuilder();

        foreach (var para in body.Elements<Paragraph>())
        {
            var text = para.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
                sb.AppendLine(text);
        }

        return sb.ToString().Trim();
    }
}
