using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PDFOCRTarget.Services;

public class PdfService
{
    /// <summary>
    /// 获取 PDF 页数
    /// </summary>
    public int GetPageCount(string pdfPath)
    {
        using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
        return doc.NumberOfPages;
    }

    /// <summary>
    /// 从 PDF 页面提取文字（适用于可搜索 PDF）
    /// </summary>
    public string ExtractTextFromPage(string pdfPath, int pageIndex)
    {
        using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
        var page = doc.GetPage(pageIndex + 1); // PdfPig 页码从 1 开始
        return page.Text ?? "";
    }

    /// <summary>
    /// 判断 PDF 是否为可搜索（包含文字层）
    /// </summary>
    public bool IsSearchablePdf(string pdfPath)
    {
        try
        {
            using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
            // 检查前3页是否包含文字
            var pagesToCheck = Math.Min(3, doc.NumberOfPages);
            for (int i = 1; i <= pagesToCheck; i++)
            {
                var text = doc.GetPage(i).Text;
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 10)
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 支持的文件扩展名
    /// </summary>
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".webp"
    };

    public static bool IsSupported(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext);
    }

    public static bool IsPdf(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsImage(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tiff" or ".tif" or ".webp";
    }
}
