using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PdfSharpCore.Pdf.IO;

namespace UniPrint.Business.Services;

public interface IDocumentInspector
{
    /// <summary>
    /// Kiểm tra tính hợp lệ và tự động tính số trang từ nội dung file PDF hoặc DOCX.
    /// Ném ngoại lệ InvalidOperationException nếu file bị hỏng hoặc không đúng định dạng (EC01).
    /// </summary>
    int InspectPageCount(Stream stream, string fileExtension);

    /// <summary>
    /// Kiểm tra đuôi file có được hệ thống hỗ trợ hay không (.pdf, .docx).
    /// </summary>
    bool IsSupportedExtension(string fileExtension);
}

public class DocumentInspector : IDocumentInspector
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx"
    };

    public bool IsSupportedExtension(string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension)) return false;
        var normalized = fileExtension.StartsWith('.') ? fileExtension : $".{fileExtension}";
        return SupportedExtensions.Contains(normalized);
    }

    public int InspectPageCount(Stream stream, string fileExtension)
    {
        if (stream == null || stream.Length == 0)
        {
            throw new InvalidOperationException("File rỗng không có nội dung dữ liệu.");
        }

        var normalizedExt = (fileExtension ?? string.Empty).Trim().ToLowerInvariant();
        if (!normalizedExt.StartsWith('.'))
        {
            normalizedExt = $".{normalizedExt}";
        }

        if (!IsSupportedExtension(normalizedExt))
        {
            throw new InvalidOperationException($"Định dạng file '{fileExtension}' không được hỗ trợ. Hệ thống chỉ chấp nhận file .pdf và .docx.");
        }

        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        return normalizedExt switch
        {
            ".pdf" => InspectPdfPageCount(stream),
            ".docx" => InspectDocxPageCount(stream),
            _ => throw new InvalidOperationException($"Định dạng {normalizedExt} không được hỗ trợ.")
        };
    }

    private static int InspectPdfPageCount(Stream stream)
    {
        try
        {
            // Thử đọc qua PdfSharpCore trước
            using var pdfDoc = PdfReader.Open(stream, PdfDocumentOpenMode.ReadOnly);
            if (pdfDoc.PageCount > 0)
            {
                return pdfDoc.PageCount;
            }
        }
        catch
        {
            // Fallback: Quét stream nhị phân tìm metadata PDF chuẩn /Type /Pages /Count hoặc /Type /Page
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            try
            {
                using var reader = new StreamReader(stream, leaveOpen: true);
                var content = reader.ReadToEnd();

                // Kiểm tra PDF magic header
                if (!content.StartsWith("%PDF-") && !content.Contains("%PDF-"))
                {
                    throw new InvalidOperationException("File không phải định dạng PDF chuẩn hoặc đã bị hỏng.");
                }

                // Tìm /Type /Pages ... /Count X
                var countMatches = Regex.Matches(content, @"/Type\s*/Pages.*?/Count\s+(\d+)", RegexOptions.Singleline);
                if (countMatches.Count > 0)
                {
                    var maxCount = 0;
                    foreach (Match m in countMatches)
                    {
                        if (int.TryParse(m.Groups[1].Value, out var c) && c > maxCount)
                        {
                            maxCount = c;
                        }
                    }
                    if (maxCount > 0) return maxCount;
                }

                // Tìm số lượng node /Type /Page (không phải /Pages)
                var pageMatches = Regex.Matches(content, @"/Type\s*/Page(?![s\w])");
                if (pageMatches.Count > 0)
                {
                    return pageMatches.Count;
                }
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException("Không thể đọc cấu trúc file PDF bị hỏng hoặc mã hóa.", ex);
            }

            throw new InvalidOperationException("File PDF bị hỏng hoặc cấu trúc không hợp lệ (EC01).");
        }

        throw new InvalidOperationException("File PDF không chứa trang in hợp lệ (EC01).");
    }

    private static int InspectDocxPageCount(Stream stream)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var appEntry = archive.GetEntry("docProps/app.xml");
            if (appEntry != null)
            {
                using var entryStream = appEntry.Open();
                var xDoc = XDocument.Load(entryStream);

                var pagesElement = xDoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Pages");
                if (pagesElement != null && int.TryParse(pagesElement.Value, out var pages) && pages > 0)
                {
                    return pages;
                }

                // Nếu Pages chưa được Word ghi nhận, thử ước lượng từ Words hoặc Paragraphs
                var wordsElement = xDoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Words");
                if (wordsElement != null && int.TryParse(wordsElement.Value, out var words) && words > 0)
                {
                    // Khoảng 350 từ mỗi trang A4 tiêu chuẩn
                    return Math.Max(1, (int)Math.Ceiling(words / 350.0));
                }
            }

            // Kiểm tra word/document.xml có tồn tại không để chứng minh đây là DOCX hợp lệ
            var docEntry = archive.GetEntry("word/document.xml");
            if (docEntry != null)
            {
                return 1; // Mặc định ít nhất 1 trang nếu là docx hợp lệ
            }

            throw new InvalidOperationException("File DOCX thiếu cấu trúc tài liệu bắt buộc (EC01).");
        }
        catch (InvalidDataException)
        {
            throw new InvalidOperationException("File DOCX không phải định dạng nén hợp lệ hoặc đã bị lỗi (EC01).");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Lỗi phân tích file DOCX: {ex.Message}", ex);
        }
    }
}
