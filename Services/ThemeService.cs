using System;

namespace PDFOCRTarget.Services;

public class ThemeService
{
    public static ThemeService Instance { get; } = new();

    public event EventHandler<string>? PhotoBackgroundChanged;
    public event EventHandler<double>? OverlayOpacityChanged;

    private string _photoPath = "";
    private double _overlayOpacity = 0.85;

    public string PhotoPath
    {
        get => _photoPath;
        set
        {
            _photoPath = value;
            PhotoBackgroundChanged?.Invoke(this, value);
            ConfigService.Set("PhotoPath", value);
        }
    }

    public double OverlayOpacity
    {
        get => _overlayOpacity;
        set
        {
            _overlayOpacity = value;
            OverlayOpacityChanged?.Invoke(this, value);
            ConfigService.Set("OverlayOpacity", value);
        }
    }

    public void LoadSettings()
    {
        _photoPath = ConfigService.Get("PhotoPath", "");
        _overlayOpacity = ConfigService.Get("OverlayOpacity", 0.85);
    }
}
