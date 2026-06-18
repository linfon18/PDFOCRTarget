using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFOCRTarget.Services;

namespace PDFOCRTarget.ViewModels.Pages;

public partial class PageOcrResult
{
    public int PageIndex { get; set; }
    public string Text { get; set; } = "";
    public Bitmap? PreviewImage { get; set; }
}

public partial class PdfConvertPageViewModel : ViewModelBase
{
    private readonly OcrService _ocrService = new();
    private readonly PdfService _pdfService = new();
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _inputFilePath = "";

    [ObservableProperty]
    private string _inputFileName = "";

    [ObservableProperty]
    private string _outputFilePath = "";

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private int _currentPageIndex;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private string _statusText = "选择扫描版 PDF 文件开始转换";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private int _selectedDpi = 200;

    [ObservableProperty]
    private string _currentOcrText = "";

    [ObservableProperty]
    private Bitmap? _currentPreviewImage;

    private readonly List<PageOcrResult> _pageResults = [];

    [ObservableProperty]
    private string _pageNavigatorText = "";

    [ObservableProperty]
    private bool _hasResults;

    public List<int> DpiOptions { get; } = [150, 200, 300];

    [RelayCommand]
    private async Task SelectInputFile()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择扫描版 PDF 文件",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("PDF 文件") { Patterns = ["*.pdf"] }
            }
        });

        if (files.Count > 0)
        {
            InputFilePath = files[0].Path.LocalPath;
            InputFileName = Path.GetFileName(InputFilePath);

            try
            {
                TotalPages = _pdfService.GetPageCount(InputFilePath);
                CurrentPageIndex = 0;
                _pageResults.Clear();
                HasResults = false;
                CurrentOcrText = "";
                CurrentPreviewImage = null;
                StatusText = $"已加载: {InputFileName} ({TotalPages} 页)";
            }
            catch (Exception ex)
            {
                StatusText = $"加载失败: {ex.Message}";
                TotalPages = 0;
            }
        }
    }

    [RelayCommand]
    private async Task StartConvert()
    {
        if (string.IsNullOrEmpty(InputFilePath) || IsProcessing) return;

        IsProcessing = true;
        CurrentPageIndex = 0;
        ProgressValue = 0;
        _cts = new CancellationTokenSource();
        _pageResults.Clear();

        try
        {
            for (int i = 0; i < TotalPages; i++)
            {
                if (_cts.Token.IsCancellationRequested) break;

                ProgressValue = (double)i / TotalPages * 100;
                ProgressText = $"{i + 1}/{TotalPages}";

                // 渲染 PDF 页面为图片预览
                StatusText = $"正在渲染第 {i + 1}/{TotalPages} 页...";
                Bitmap? preview = null;
                try
                {
                    preview = await RenderPdfPageAsBitmap(i);
                }
                catch { }

                // OCR 识别
                StatusText = $"正在 OCR 第 {i + 1}/{TotalPages} 页...";
                var result = await _ocrService.OcrPdfPageAsync(InputFilePath, i, SelectedDpi, _cts.Token);
                var ocrText = result.Text;

                // 也检查文字层
                var existingText = _pdfService.ExtractTextFromPage(InputFilePath, i);
                if (!string.IsNullOrWhiteSpace(existingText) && existingText.Length > 20)
                {
                    ocrText = existingText;
                    StatusText = $"第 {i + 1}/{TotalPages} 页 (文字层提取)";
                }

                _pageResults.Add(new PageOcrResult
                {
                    PageIndex = i,
                    Text = ocrText,
                    PreviewImage = preview
                });

                CurrentPageIndex = i;
                UpdateCurrentPageDisplay();
                HasResults = true;
            }

            ProgressValue = 100;
            StatusText = $"转换完成! 共 {TotalPages} 页";
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消转换";
        }
        catch (Exception ex)
        {
            StatusText = $"转换失败: {ex.Message}";
        }

        IsProcessing = false;
    }

    [RelayCommand]
    private void StopConvert()
    {
        _cts?.Cancel();
        IsProcessing = false;
        StatusText = "已停止";
    }

    [RelayCommand]
    private void GoToPreviousPage()
    {
        if (CurrentPageIndex > 0)
        {
            CurrentPageIndex--;
            UpdateCurrentPageDisplay();
        }
    }

    [RelayCommand]
    private void GoToNextPage()
    {
        if (CurrentPageIndex < _pageResults.Count - 1)
        {
            CurrentPageIndex++;
            UpdateCurrentPageDisplay();
        }
    }

    [RelayCommand]
    private void GoToFirstPage()
    {
        if (_pageResults.Count > 0)
        {
            CurrentPageIndex = 0;
            UpdateCurrentPageDisplay();
        }
    }

    [RelayCommand]
    private void GoToLastPage()
    {
        if (_pageResults.Count > 0)
        {
            CurrentPageIndex = _pageResults.Count - 1;
            UpdateCurrentPageDisplay();
        }
    }

    private void UpdateCurrentPageDisplay()
    {
        if (CurrentPageIndex >= 0 && CurrentPageIndex < _pageResults.Count)
        {
            var page = _pageResults[CurrentPageIndex];
            CurrentOcrText = page.Text;
            CurrentPreviewImage = page.PreviewImage;
            PageNavigatorText = $"第 {CurrentPageIndex + 1} / {_pageResults.Count} 页";
        }
        else
        {
            CurrentOcrText = "";
            CurrentPreviewImage = null;
            PageNavigatorText = "";
        }
    }

    /// <summary>
    /// 将 PDF 某页渲染为 Avalonia Bitmap 用于预览
    /// </summary>
    private async Task<Bitmap?> RenderPdfPageAsBitmap(int pageIndex)
    {
        var script = $@"
import sys
sys.stdout.buffer.write(b'')
try:
    import fitz
    doc = fitz.open(r'{InputFilePath}')
    page = doc[{pageIndex}]
    mat = fitz.Matrix(1.5, 1.5)
    pix = page.get_pixmap(matrix=mat)
    sys.stdout.buffer.write(pix.tobytes('png'))
    doc.close()
except Exception as e:
    print(str(e), file=sys.stderr)
";
        var tempScript = Path.Combine(Path.GetTempPath(), $"pdfoctr_render_{Guid.NewGuid():N}.py");
        try
        {
            await File.WriteAllTextAsync(tempScript, script);

            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{tempScript}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            using var ms = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(ms);
            await process.WaitForExitAsync();

            if (ms.Length > 0)
            {
                ms.Position = 0;
                return new Bitmap(ms);
            }
            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            try { File.Delete(tempScript); } catch { }
        }
    }

    [RelayCommand]
    private async Task CopyText()
    {
        if (string.IsNullOrEmpty(CurrentOcrText)) return;
        var topLevel = GetTopLevel();
        if (topLevel?.Clipboard != null)
        {
            await topLevel.Clipboard.SetTextAsync(CurrentOcrText);
            StatusText = "已复制到剪贴板";
        }
    }

    [RelayCommand]
    private async Task ExportText()
    {
        if (_pageResults.Count == 0) return;

        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出识别结果",
            SuggestedFileName = $"{Path.GetFileNameWithoutExtension(InputFileName)}_OCR.txt",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("文本文件") { Patterns = ["*.txt"] }
            }
        });

        if (file != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _pageResults.Count; i++)
            {
                sb.AppendLine($"=== 第 {i + 1} 页 ===");
                sb.AppendLine(_pageResults[i].Text);
                sb.AppendLine();
            }
            await File.WriteAllTextAsync(file.Path.LocalPath, sb.ToString());
            StatusText = $"已导出到 {Path.GetFileName(file.Path.LocalPath)}";
        }
    }

    /// <summary>
    /// 导出为可搜索 PDF（原图 + 隐形文字层）
    /// 使用 RapidOCR 获取每个文字块的坐标，在对应位置插入透明文字
    /// </summary>
    [RelayCommand]
    private async Task ExportSearchablePdf()
    {
        if (_pageResults.Count == 0 || string.IsNullOrEmpty(InputFilePath)) return;

        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出可搜索 PDF",
            SuggestedFileName = $"{Path.GetFileNameWithoutExtension(InputFileName)}_可搜索.pdf",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("PDF 文件") { Patterns = ["*.pdf"] }
            }
        });

        if (file == null) return;

        var outputPath = file.Path.LocalPath;
        StatusText = "正在生成可搜索 PDF...";
        IsProcessing = true;
        ProgressValue = 0;
        var total = _pageResults.Count;

        try
        {
            // 逐页处理：渲染图片 + OCR 获取坐标 + 插入透明文字层
            // 使用 Python 脚本但逐页输出进度
            var tempDir = Path.Combine(Path.GetTempPath(), $"pdfoctr_pages_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var dpi = SelectedDpi;

            // 步骤1：逐页生成带文字层的 PDF 页面（JSON 格式存储坐标）
            var pageDataFiles = new List<string>();
            for (int i = 0; i < total; i++)
            {
                if (_cts?.Token.IsCancellationRequested == true) break;

                ProgressValue = (double)i / total * 80;
                ProgressText = $"OCR {i + 1}/{total}";
                StatusText = $"正在 OCR 第 {i + 1}/{total} 页并生成文字层...";

                var pageScript = $@"
import sys, json
sys.stdout.reconfigure(encoding='utf-8')
try:
    import fitz
    from rapidocr_onnxruntime import RapidOCR

    src_path = r'{InputFilePath}'
    page_idx = {i}
    dpi = {dpi}
    out_path = r'{Path.Combine(tempDir, $"page_{i}.json")}'

    engine = RapidOCR()
    doc = fitz.open(src_path)
    page = doc[page_idx]
    w, h = page.rect.width, page.rect.height
    mat = fitz.Matrix(dpi/72, dpi/72)
    pix = page.get_pixmap(matrix=mat)
    img_bytes = pix.tobytes('png')

    result, _ = engine(img_bytes)
    text_items = []
    if result:
        scale_x = w / pix.width
        scale_y = h / pix.height
        for item in result:
            bbox, text, conf = item
            x0 = bbox[0][0] * scale_x
            y0 = bbox[0][1] * scale_y
            x2 = bbox[2][0] * scale_x
            y2 = bbox[2][1] * scale_y
            text_h = y2 - y0
            text_items.append({{'x0': x0, 'y2': y2, 'h': text_h, 'text': text}})

    with open(out_path, 'w', encoding='utf-8') as f:
        json.dump({{'w': w, 'h': h, 'items': text_items}}, f, ensure_ascii=False)
    doc.close()
    print('OK')
except Exception as e:
    print(f'ERROR: {{e}}', file=sys.stderr)
    print('FAIL')
";
                var result = await RunPythonAsync(pageScript, _cts?.Token ?? CancellationToken.None);
                if (!result.Contains("OK"))
                {
                    StatusText = $"OCR 第 {i + 1} 页失败: {result}";
                    continue;
                }
                pageDataFiles.Add(Path.Combine(tempDir, $"page_{i}.json"));
            }

            if (_cts?.Token.IsCancellationRequested == true)
            {
                StatusText = "已取消导出";
                CleanupTempDir(tempDir);
                IsProcessing = false;
                return;
            }

            // 步骤2：合并所有页面生成最终 PDF
            ProgressValue = 85;
            ProgressText = $"合并 {total} 页";
            StatusText = "正在合并生成可搜索 PDF...";

            var mergeScript = $@"
import sys, json, os
sys.stdout.reconfigure(encoding='utf-8')
try:
    import fitz

    src_path = r'{InputFilePath}'
    dst_path = r'{outputPath}'
    page_dir = r'{tempDir}'
    dpi = {dpi}

    src_doc = fitz.open(src_path)
    new_doc = fitz.open()

    for i in range(len(src_doc)):
        page = src_doc[i]
        w, h = page.rect.width, page.rect.height
        new_page = new_doc.new_page(width=w, height=h)

        # 加载 OCR 坐标数据，在图片下方插入文字层
        # 文字在图片后面，视觉上不可见，但 PDF 阅读器可搜索/选中
        json_path = os.path.join(page_dir, f'page_{{i}}.json')
        if os.path.exists(json_path):
            with open(json_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            tw = fitz.TextWriter(new_page.rect)
            for item in data['items']:
                x0 = item['x0']
                y_bottom = item['y2']
                text_h = item['h']
                text = item['text']
                if text_h <= 0 or not text.strip():
                    continue
                fontsize = max(text_h * 0.9, 4)
                # 基线位置：bbox 底部向上偏移一小段
                baseline_y = y_bottom - text_h * 0.1
                try:
                    tw.append(fitz.Point(x0, baseline_y), text, fontsize=fontsize)
                except:
                    pass
            # 写入文字层（在图片下方，overlay=False）
            try:
                tw.write_text(new_page, overlay=False)
            except:
                pass

        # 渲染原页面为图片，覆盖在文字层上方
        mat = fitz.Matrix(dpi/72, dpi/72)
        pix = page.get_pixmap(matrix=mat)
        img_bytes = pix.tobytes('png')
        new_page.insert_image(new_page.rect, stream=img_bytes, overlay=True)

    new_doc.save(dst_path)
    new_doc.close()
    src_doc.close()

    # 清理临时文件
    import shutil
    shutil.rmtree(page_dir, ignore_errors=True)

    print('OK')
except Exception as e:
    print(f'ERROR: {{e}}', file=sys.stderr)
    print('FAIL')
";
            var mergeResult = await RunPythonAsync(mergeScript, CancellationToken.None);

            ProgressValue = 100;
            ProgressText = $"完成";

            if (mergeResult.Contains("OK"))
            {
                OutputFilePath = outputPath;
                StatusText = $"可搜索 PDF 已导出: {Path.GetFileName(outputPath)}";
            }
            else
            {
                StatusText = $"导出失败: {mergeResult}";
                CleanupTempDir(tempDir);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消导出";
        }
        catch (Exception ex)
        {
            StatusText = $"导出失败: {ex.Message}";
        }

        IsProcessing = false;
    }

    private static void CleanupTempDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }

    private static async Task<string> RunPythonAsync(string script, CancellationToken ct)
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
                return error.Trim();
            return output.Trim();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        finally
        {
            try { File.Delete(tempScript); } catch { }
        }
    }

    [RelayCommand]
    private void OpenExportedFile()
    {
        if (!string.IsNullOrEmpty(OutputFilePath) && File.Exists(OutputFilePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(OutputFilePath) { UseShellExecute = true });
            }
            catch { }
        }
    }

    private static TopLevel? GetTopLevel()
    {
        if (App.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
}
