using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PDFOCRTarget.Services;

public class OcrResultItem
{
    public int PageIndex { get; set; }
    public string Text { get; set; } = "";
    public float Confidence { get; set; }
}

public class OcrService
{
    /// <summary>
    /// 使用 Python RapidOCR 识别图片文件中的文字
    /// </summary>
    public async Task<OcrResultItem> OcrImageAsync(string imagePath, CancellationToken ct = default)
    {
        var script = $@"
import sys
sys.stdout.reconfigure(encoding='utf-8')
try:
    from rapidocr_onnxruntime import RapidOCR
    engine = RapidOCR()
    result, _ = engine(r'{imagePath}')
    if result:
        lines = [item[1] for item in result]
        print('\n'.join(lines))
    else:
        print('')
except Exception as e:
    print(f'OCR_ERROR: {{e}}', file=sys.stderr)
    print('')
";
        var text = await RunPythonScriptAsync(script, ct);
        return new OcrResultItem
        {
            PageIndex = 0,
            Text = text,
            Confidence = 0
        };
    }

    /// <summary>
    /// 使用 Python RapidOCR 识别图片字节数据中的文字（PDF页面渲染后调用）
    /// </summary>
    public async Task<OcrResultItem> OcrImageBytesAsync(byte[] imageBytes, int pageIndex = 0, CancellationToken ct = default)
    {
        var tempImage = Path.Combine(Path.GetTempPath(), $"pdfoctr_img_{Guid.NewGuid():N}.png");
        try
        {
            await File.WriteAllBytesAsync(tempImage, imageBytes, ct);
            var result = await OcrImageAsync(tempImage, ct);
            result.PageIndex = pageIndex;
            return result;
        }
        finally
        {
            try { File.Delete(tempImage); } catch { }
        }
    }

    /// <summary>
    /// 使用 Python RapidOCR 识别 PDF 单页
    /// </summary>
    public async Task<OcrResultItem> OcrPdfPageAsync(string pdfPath, int pageIndex, int dpi = 200, CancellationToken ct = default)
    {
        var script = $@"
import sys
sys.stdout.reconfigure(encoding='utf-8')
try:
    import fitz
    doc = fitz.open(r'{pdfPath}')
    page = doc[{pageIndex}]
    mat = fitz.Matrix({dpi}/72, {dpi}/72)
    pix = page.get_pixmap(matrix=mat)
    img_bytes = pix.tobytes('png')
    doc.close()

    from rapidocr_onnxruntime import RapidOCR
    engine = RapidOCR()
    result, _ = engine(img_bytes)
    if result:
        lines = [item[1] for item in result]
        print('\n'.join(lines))
    else:
        print('')
except Exception as e:
    print(f'OCR_ERROR: {{e}}', file=sys.stderr)
    print('')
";
        var text = await RunPythonScriptAsync(script, ct);
        return new OcrResultItem
        {
            PageIndex = pageIndex,
            Text = text,
            Confidence = 0
        };
    }

    /// <summary>
    /// 检查 OCR 环境是否就绪
    /// </summary>
    public static async Task<bool> IsReadyAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "-c \"from rapidocr_onnxruntime import RapidOCR; print('OK')\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return false;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 && output.Contains("OK");
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> RunPythonScriptAsync(string script, CancellationToken ct)
    {
        var tempScript = Path.Combine(Path.GetTempPath(), $"pdfoctr_{Guid.NewGuid():N}.py");
        try
        {
            await File.WriteAllTextAsync(tempScript, script, ct);

            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{tempScript}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process == null) return "[无法启动 Python]";

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            {
                return $"[OCR 失败: {error.Trim()}]";
            }

            return output.Trim();
        }
        catch (Exception ex)
        {
            return $"[OCR 异常: {ex.Message}]";
        }
        finally
        {
            try { File.Delete(tempScript); } catch { }
        }
    }
}
