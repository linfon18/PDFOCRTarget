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

public partial class SearchPageViewModel : ViewModelBase
{
    private readonly OcrService _ocrService = new();
    private readonly PdfService _pdfService = new();
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _searchKeywords = "";

    [ObservableProperty]
    private ObservableCollection<SearchResultItem> _searchResults = [];

    [ObservableProperty]
    private string _filePath = "";

    [ObservableProperty]
    private string _fileName = "";

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _statusText = "选择文件并输入关键词开始搜索";

    [RelayCommand]
    private async Task SelectFile()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 PDF 或图片文件",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("PDF 和图片文件") { Patterns = ["*.pdf", "*.png", "*.jpg", "*.jpeg", "*.bmp"] },
            }
        });

        if (files.Count > 0)
        {
            FilePath = files[0].Path.LocalPath;
            FileName = Path.GetFileName(FilePath);
            SearchResults.Clear();
            StatusText = $"已选择: {FileName}";
        }
    }

    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(FilePath) || string.IsNullOrWhiteSpace(SearchKeywords))
        {
            StatusText = "请先选择文件并输入关键词";
            return;
        }

        IsSearching = true;
        SearchResults.Clear();
        _cts = new CancellationTokenSource();

        var keywords = SearchKeywords.Split([',', '，', ';', '；', '|', '\n', '\r'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (keywords.Length == 0)
        {
            IsSearching = false;
            StatusText = "请输入有效的关键词";
            return;
        }

        try
        {
            if (PdfService.IsPdf(FilePath))
            {
                var pageCount = _pdfService.GetPageCount(FilePath);

                for (int i = 0; i < pageCount; i++)
                {
                    if (_cts.Token.IsCancellationRequested) break;

                    var pageText = _pdfService.ExtractTextFromPage(FilePath, i);

                    if (string.IsNullOrWhiteSpace(pageText))
                    {
                        StatusText = $"扫描版 PDF，OCR 第 {i + 1}/{pageCount} 页...";
                        try
                        {
                            var ocrResult = await _ocrService.OcrPdfPageAsync(FilePath, i, 200, _cts.Token);
                            pageText = ocrResult.Text;
                        }
                        catch { pageText = ""; }
                    }

                    foreach (var kw in keywords)
                    {
                        if (pageText.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        {
                            var context = GetContextText(pageText, kw, 50);
                            SearchResults.Add(new SearchResultItem
                            {
                                Keyword = kw,
                                PageNumber = i + 1,
                                Context = context,
                                FileName = FileName
                            });
                        }
                    }

                    ProgressValue = (double)(i + 1) / pageCount * 100;
                }
            }
            else if (PdfService.IsImage(FilePath))
            {
                StatusText = "正在 OCR 识别图片...";
                var result = await _ocrService.OcrImageAsync(FilePath, _cts.Token);

                foreach (var kw in keywords)
                {
                    if (result.Text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        var context = GetContextText(result.Text, kw, 50);
                        SearchResults.Add(new SearchResultItem
                        {
                            Keyword = kw,
                            PageNumber = 1,
                            Context = context,
                            FileName = FileName
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"搜索失败: {ex.Message}";
        }

        IsSearching = false;
        StatusText = SearchResults.Count > 0
            ? $"找到 {SearchResults.Count} 个匹配结果"
            : "未找到匹配结果";
    }

    [RelayCommand]
    private void StopSearch()
    {
        _cts?.Cancel();
        IsSearching = false;
        StatusText = "已停止搜索";
    }

    [RelayCommand]
    private async Task ExportResults()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出搜索结果",
            SuggestedFileName = "搜索结果.txt",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("文本文件") { Patterns = ["*.txt"] }
            }
        });

        if (file != null)
        {
            var lines = SearchResults.Select(r =>
                $"关键词: {r.Keyword}\n文件: {r.FileName}\n页码: {r.PageNumber}\n上下文: {r.Context}\n");
            await File.WriteAllTextAsync(file.Path.LocalPath,
                $"搜索关键词: {SearchKeywords}\n结果数: {SearchResults.Count}\n\n" +
                string.Join(new string('-', 60) + "\n", lines));
            StatusText = "已导出搜索结果";
        }
    }

    private static string GetContextText(string text, string keyword, int contextLength)
    {
        var idx = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "";

        var start = Math.Max(0, idx - contextLength);
        var end = Math.Min(text.Length, idx + keyword.Length + contextLength);
        var context = text.Substring(start, end - start);

        if (start > 0) context = "..." + context;
        if (end < text.Length) context += "...";

        return context;
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
