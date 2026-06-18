using CommunityToolkit.Mvvm.ComponentModel;

namespace PDFOCRTarget.Models;

public partial class SearchResultItem : ObservableObject
{
    [ObservableProperty]
    private string _keyword = "";

    [ObservableProperty]
    private int _pageNumber;

    [ObservableProperty]
    private string _context = "";

    [ObservableProperty]
    private string _fileName = "";
}
