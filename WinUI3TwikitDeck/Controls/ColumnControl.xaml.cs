using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI3TwikitDeck.ViewModels;

namespace WinUI3TwikitDeck.Controls;

public sealed partial class ColumnControl : UserControl
{
    public ColumnControl()
    {
        InitializeComponent();
    }

    public ColumnViewModelBase? ColumnViewModel
    {
        get => (ColumnViewModelBase?)GetValue(ColumnViewModelProperty);
        set => SetValue(ColumnViewModelProperty, value);
    }

    public static readonly DependencyProperty ColumnViewModelProperty = DependencyProperty.Register(
        nameof(ColumnViewModel),
        typeof(ColumnViewModelBase),
        typeof(ColumnControl),
        new PropertyMetadata(null));
}
