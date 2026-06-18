using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PDFOCRTarget.ViewModels;

namespace PDFOCRTarget;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null) return null;

        var viewName = data.GetType().FullName!
            .Replace("ViewModels", "Views")
            .Replace("ViewModel", "");

        var type = Type.GetType(viewName);
        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + viewName };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
