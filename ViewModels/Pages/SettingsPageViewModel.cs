using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFOCRTarget.Services;

namespace PDFOCRTarget.ViewModels.Pages;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly ThemeService _theme = ThemeService.Instance;

    [ObservableProperty]
    private string _photoPath = "";

    [ObservableProperty]
    private double _overlayOpacity = 0.85;

    [ObservableProperty]
    private string _ocrStatus = "检测中...";

    public SettingsPageViewModel()
    {
        PhotoPath = ConfigService.Get("PhotoPath", "");
        OverlayOpacity = ConfigService.Get("OverlayOpacity", 0.85);
        _ = CheckOcrStatus();
    }

    private async Task CheckOcrStatus()
    {
        var ready = await OcrService.IsReadyAsync();
        OcrStatus = ready ? "Python RapidOCR 已就绪" : "未检测到 Python RapidOCR";
    }

    [RelayCommand]
    private async Task SelectBackground()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择背景图片",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("图片文件") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"] }
            }
        });

        if (files.Count > 0)
        {
            PhotoPath = files[0].Path.LocalPath;
            _theme.PhotoPath = PhotoPath;
        }
    }

    [RelayCommand]
    private void ClearBackground()
    {
        PhotoPath = "";
        _theme.PhotoPath = "";
    }

    partial void OnOverlayOpacityChanged(double value)
    {
        _theme.OverlayOpacity = value;
    }

    [RelayCommand]
    private void OpenGithub()
    {
        var url = "https://github.com/linfon18/PDFOCRTarget";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
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
