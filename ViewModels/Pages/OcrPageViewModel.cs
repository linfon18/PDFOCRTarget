using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFOCRTarget.Models;
using PDFOCRTarget.Services;

namespace PDFOCRTarget.ViewModels.Pages;

public partial class OcrPageViewModel : ViewModelBase
{
    private readonly OcrService _ocrService = new();
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private ObservableCollection<FileItem> _fileItems = [];

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private FileItem? _selectedFileItem;

    [ObservableProperty]
    private string _previewText = "";

    [ObservableProperty]
    private int _selectedDpi = 200;

    public List<int> DpiOptions { get; } = [150, 200, 300];

    partial void OnSelectedFileItemChanged(FileItem? value)
    {
        PreviewText = value?.ExtractedText ?? "";
    }

    [RelayCommand]
    private async Task AddFiles()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 PDF 或图片文件",
            AllowMultiple = true,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("PDF 和图片文件")
                {
                    Patterns = ["*.pdf", "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tiff", "*.tif", "*.webp"]
                },
                new("PDF 文件") { Patterns = ["*.pdf"] },
                new("图片文件") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tiff", "*.tif", "*.webp"] }
            }
        });

        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            if (!File.Exists(path)) continue;
            if (FileItems.Any(f => f.FilePath == path)) continue;

            var item = new FileItem
            {
                FileName = Path.GetFileName(path),
                FilePath = path,
                FileSize = new FileInfo(path).Length,
                FileType = PdfService.IsPdf(path) ? "PDF" : PdfService.IsImage(path) ? "图片" : "未知",
                Status = "等待中"
            };
            FileItems.Add(item);
        }

        TotalCount = FileItems.Count;
        StatusText = $"已添加 {FileItems.Count} 个文件";
    }

    [RelayCommand]
    private void ClearFiles()
    {
        FileItems.Clear();
        CompletedCount = 0;
        TotalCount = 0;
        ProgressValue = 0;
        ProgressText = "";
        StatusText = "已清空";
        PreviewText = "";
        SelectedFileItem = null;
    }

    [RelayCommand]
    private void RemoveFile(FileItem? item)
    {
        if (item == null) return;
        FileItems.Remove(item);
        TotalCount = FileItems.Count;
    }

    [RelayCommand]
    private async Task StartOcr()
    {
        if (FileItems.Count == 0 || IsProcessing) return;

        IsProcessing = true;
        CompletedCount = 0;
        TotalCount = FileItems.Count;
        _cts = new CancellationTokenSource();

        foreach (var item in FileItems)
        {
            if (_cts.Token.IsCancellationRequested) break;

            item.Status = "识别中...";
            try
            {
                string text;

                if (PdfService.IsPdf(item.FilePath))
                {
                    var pdfService = new PdfService();
                    if (pdfService.IsSearchablePdf(item.FilePath))
                    {
                        var pageCount = pdfService.GetPageCount(item.FilePath);
                        var allText = "";
                        for (int i = 0; i < pageCount; i++)
                        {
                            allText += pdfService.ExtractTextFromPage(item.FilePath, i) + "\n\n";
                        }
                        text = allText.Trim();
                        item.Status = $"完成 (文字提取, {pageCount}页)";
                    }
                    else
                    {
                        text = "[扫描版 PDF 请使用「PDF转换」页面]";
                        item.Status = "请使用PDF转换页面";
                    }
                }
                else if (PdfService.IsImage(item.FilePath))
                {
                    var result = await _ocrService.OcrImageAsync(item.FilePath, _cts.Token);
                    text = result.Text;
                    item.Status = "完成";
                }
                else
                {
                    text = "不支持的文件格式";
                    item.Status = "格式不支持";
                }

                item.ExtractedText = text;
                SelectedFileItem = item;
            }
            catch (OperationCanceledException)
            {
                item.Status = "已取消";
                break;
            }
            catch (Exception ex)
            {
                item.ExtractedText = "";
                item.Status = $"失败: {ex.Message}";
            }

            CompletedCount++;
            ProgressValue = (double)CompletedCount / TotalCount * 100;
            ProgressText = $"{CompletedCount}/{TotalCount}";
            StatusText = $"处理中 {ProgressText}";
        }

        IsProcessing = false;
        StatusText = $"完成 {CompletedCount}/{TotalCount} 个文件";
    }

    [RelayCommand]
    private void StopOcr()
    {
        _cts?.Cancel();
        IsProcessing = false;
        StatusText = "已停止";
    }

    [RelayCommand]
    private async Task CopyText()
    {
        if (string.IsNullOrEmpty(PreviewText)) return;
        var topLevel = GetTopLevel();
        if (topLevel?.Clipboard != null)
        {
            await topLevel.Clipboard.SetTextAsync(PreviewText);
            StatusText = "已复制到剪贴板";
        }
    }

    [RelayCommand]
    private async Task ExportText()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出识别结果",
            SuggestedFileName = "OCR结果.txt",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("文本文件") { Patterns = ["*.txt"] }
            }
        });

        if (file != null)
        {
            var path = file.Path.LocalPath;
            var lines = FileItems.Select(f => $"=== {f.FileName} ===\n{f.ExtractedText}\n");
            await File.WriteAllTextAsync(path, string.Join("\n", lines));
            StatusText = $"已导出到 {Path.GetFileName(path)}";
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
