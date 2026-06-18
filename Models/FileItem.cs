using CommunityToolkit.Mvvm.ComponentModel;

namespace PDFOCRTarget.Models;

public partial class FileItem : ObservableObject
{
    [ObservableProperty]
    private string _fileName = "";

    [ObservableProperty]
    private string _filePath = "";

    [ObservableProperty]
    private long _fileSize;

    [ObservableProperty]
    private string _fileType = "";

    [ObservableProperty]
    private string _status = "等待中";

    [ObservableProperty]
    private string _extractedText = "";

    [ObservableProperty]
    private float _confidence;

    public string FileSizeText
    {
        get
        {
            if (FileSize < 1024) return $"{FileSize} B";
            if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
            return $"{FileSize / 1024.0 / 1024.0:F1} MB";
        }
    }
}
