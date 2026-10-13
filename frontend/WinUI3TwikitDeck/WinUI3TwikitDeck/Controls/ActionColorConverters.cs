using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace WinUI3TwikitDeck.Controls;

public sealed class LikeColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => new SolidColorBrush(value is bool and true ? Colors.Red : Colors.Gray);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

public sealed class RetweetColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => new SolidColorBrush(value is bool and true ? Colors.LimeGreen : Colors.Gray);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
