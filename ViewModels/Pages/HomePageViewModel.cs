using CommunityToolkit.Mvvm.ComponentModel;

namespace PDFOCRTarget.ViewModels.Pages;

public partial class HomePageViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _welcomeText = "欢迎使用 PDFOCRTarget — 全平台 PDF 图片转文字 OCR 工具";

    [ObservableProperty]
    private bool _isTessDataReady;

    [ObservableProperty]
    private string _tessDataStatus = "检测中...";
}
