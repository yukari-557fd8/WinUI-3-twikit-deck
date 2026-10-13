using Microsoft.UI.Xaml;
using WinUI3TwikitDeck.Services;
using WinUI3TwikitDeck.ViewModels;

namespace WinUI3TwikitDeck;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var timelineService = new TwikitTimelineService(new MockTwikitClient());
        var viewModel = new MainViewModel(timelineService);

        _window = new MainWindow
        {
            DataContext = viewModel
        };
        _window.Activate();
    }
}
