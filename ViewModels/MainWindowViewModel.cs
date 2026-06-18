using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFOCRTarget.Views.Pages;
using PDFOCRTarget.ViewModels.Pages;

namespace PDFOCRTarget.ViewModels;

public partial class NavigationItem : ObservableObject
{
    [ObservableProperty]
    private string _key = "";

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _icon = "";

    [ObservableProperty]
    private bool _isSelected;
}

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<NavigationItem> _navigationItems = [];

    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    [ObservableProperty]
    private Control? _currentPage;

    [ObservableProperty]
    private string _currentPageKey = "";

    // 页面缓存
    private readonly Dictionary<string, Control> _pageCache = new();

    public MainWindowViewModel()
    {
        InitializeNavigationItems();
        NavigateTo("Home");
    }

    private void InitializeNavigationItems()
    {
        NavigationItems.Add(new NavigationItem { Key = "Home", Title = "首页", Icon = "Home" });
        NavigationItems.Add(new NavigationItem { Key = "Ocr", Title = "OCR识别", Icon = "FileDocument" });
        NavigationItems.Add(new NavigationItem { Key = "PdfConvert", Title = "PDF转换", Icon = "FileReplace" });
        NavigationItems.Add(new NavigationItem { Key = "Search", Title = "关键词搜索", Icon = "Magnify" });
        NavigationItems.Add(new NavigationItem { Key = "Settings", Title = "设置", Icon = "CogOutline" });
    }

    [RelayCommand]
    private void NavigateTo(string pageKey)
    {
        if (CurrentPageKey == pageKey) return;

        if (!_pageCache.TryGetValue(pageKey, out var page))
        {
            page = pageKey switch
            {
                "Home" => new HomePage { DataContext = new HomePageViewModel() },
                "Ocr" => new OcrPage { DataContext = new OcrPageViewModel() },
                "PdfConvert" => new PdfConvertPage { DataContext = new PdfConvertPageViewModel() },
                "Search" => new SearchPage { DataContext = new SearchPageViewModel() },
                "Settings" => new SettingsPage { DataContext = new SettingsPageViewModel() },
                _ => new HomePage { DataContext = new HomePageViewModel() }
            };
            _pageCache[pageKey] = page;
        }

        CurrentPage = page;
        CurrentPageKey = pageKey;

        foreach (var item in NavigationItems)
        {
            item.IsSelected = item.Key == pageKey;
        }
    }
}
