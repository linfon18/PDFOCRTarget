using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Material.Icons;
using Material.Icons.Avalonia;
using PDFOCRTarget.Services;
using PDFOCRTarget.ViewModels;

namespace PDFOCRTarget.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnWindowLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainWindowViewModel;
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        Activate();

        // 加载背景图片
        LoadPhotoBackground();

        // 订阅背景变更事件
        ThemeService.Instance.PhotoBackgroundChanged += (_, path) =>
        {
            LoadPhotoBackground(path);
        };
    }

    private void LoadPhotoBackground(string? customPath = null)
    {
        var path = customPath ?? ConfigService.Get("PhotoPath", "");
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
        {
            BackgroundImage.IsVisible = false;
            return;
        }

        try
        {
            BackgroundImage.Source = new Bitmap(path);
            BackgroundImage.IsVisible = true;
        }
        catch
        {
            BackgroundImage.IsVisible = false;
        }
    }

    // 标题栏拖动
    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    // 窗口控制
    private void OnMinimizeButtonClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeButtonClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

        var icon = this.FindControl<MaterialIcon>("MaximizeIcon");
        if (icon != null)
        {
            icon.Kind = WindowState == WindowState.Maximized
                ? MaterialIconKind.WindowRestore
                : MaterialIconKind.WindowMaximize;
        }
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    // 导航
    private void OnNavigationButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
        {
            _viewModel?.NavigateToCommand.Execute(tag);
            UpdateNavigationState(tag);
        }
    }

    private void UpdateNavigationState(string activeKey)
    {
        var buttons = new[] { "Home", "Ocr", "PdfConvert", "Search", "Settings" };
        foreach (var key in buttons)
        {
            var indicator = this.FindControl<Border>($"{key}Indicator");
            var icon = this.FindControl<MaterialIcon>($"{key}Icon");
            var text = this.FindControl<TextBlock>($"{key}Text");

            if (indicator == null || icon == null || text == null) continue;

            if (key == activeKey)
            {
                indicator.Background = this.FindResource("MaterialSecondaryContainerBrush") as IBrush;
                icon.Opacity = 1;
                icon.Foreground = this.FindResource("MaterialOnSecondaryContainerBrush") as IBrush;
                text.Opacity = 1;
                text.FontWeight = FontWeight.Medium;
            }
            else
            {
                indicator.Background = Brushes.Transparent;
                icon.Opacity = 0.7;
                icon.Foreground = this.FindResource("MaterialOnSurfaceBrush") as IBrush;
                text.Opacity = 0.7;
                text.FontWeight = FontWeight.Normal;
            }
        }
    }
}
