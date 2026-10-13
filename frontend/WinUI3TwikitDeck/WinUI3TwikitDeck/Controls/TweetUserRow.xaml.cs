using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WinUI3TwikitDeck.Controls;

public sealed partial class TweetUserRow : UserControl
{
    public TweetUserRow()
    {
        InitializeComponent();
    }

    public string UserName
    {
        get => (string)GetValue(UserNameProperty);
        set => SetValue(UserNameProperty, value);
    }

    public static readonly DependencyProperty UserNameProperty =
        DependencyProperty.Register(nameof(UserName), typeof(string), typeof(TweetUserRow), new PropertyMetadata(string.Empty));

    public string UserScreenName
    {
        get => (string)GetValue(UserScreenNameProperty);
        set => SetValue(UserScreenNameProperty, value);
    }

    public static readonly DependencyProperty UserScreenNameProperty =
        DependencyProperty.Register(nameof(UserScreenName), typeof(string), typeof(TweetUserRow), new PropertyMetadata(string.Empty, OnMetaChanged));

    public string CreatedAtAbsoluteDisplay
    {
        get => (string)GetValue(CreatedAtAbsoluteDisplayProperty);
        set => SetValue(CreatedAtAbsoluteDisplayProperty, value);
    }

    public static readonly DependencyProperty CreatedAtAbsoluteDisplayProperty =
        DependencyProperty.Register(nameof(CreatedAtAbsoluteDisplay), typeof(string), typeof(TweetUserRow), new PropertyMetadata(string.Empty, OnMetaChanged));

    public string CreatedAtRelativeDisplay
    {
        get => (string)GetValue(CreatedAtRelativeDisplayProperty);
        set => SetValue(CreatedAtRelativeDisplayProperty, value);
    }

    public static readonly DependencyProperty CreatedAtRelativeDisplayProperty =
        DependencyProperty.Register(nameof(CreatedAtRelativeDisplay), typeof(string), typeof(TweetUserRow), new PropertyMetadata(string.Empty, OnMetaChanged));

    public ImageSource? UserProfileImage
    {
        get => (ImageSource?)GetValue(UserProfileImageProperty);
        set => SetValue(UserProfileImageProperty, value);
    }

    public static readonly DependencyProperty UserProfileImageProperty =
        DependencyProperty.Register(nameof(UserProfileImage), typeof(ImageSource), typeof(TweetUserRow), new PropertyMetadata(null));

    public bool IsUserProtected
    {
        get => (bool)GetValue(IsUserProtectedProperty);
        set => SetValue(IsUserProtectedProperty, value);
    }

    public static readonly DependencyProperty IsUserProtectedProperty =
        DependencyProperty.Register(nameof(IsUserProtected), typeof(bool), typeof(TweetUserRow), new PropertyMetadata(false));

    public bool IsUserVerified
    {
        get => (bool)GetValue(IsUserVerifiedProperty);
        set => SetValue(IsUserVerifiedProperty, value);
    }

    public static readonly DependencyProperty IsUserVerifiedProperty =
        DependencyProperty.Register(nameof(IsUserVerified), typeof(bool), typeof(TweetUserRow), new PropertyMetadata(false));

    public string ReplyToDisplay
    {
        get => (string)GetValue(ReplyToDisplayProperty);
        set => SetValue(ReplyToDisplayProperty, value);
    }

    public static readonly DependencyProperty ReplyToDisplayProperty =
        DependencyProperty.Register(nameof(ReplyToDisplay), typeof(string), typeof(TweetUserRow), new PropertyMetadata(string.Empty));

    public bool HasReplyTo
    {
        get => (bool)GetValue(HasReplyToProperty);
        set => SetValue(HasReplyToProperty, value);
    }

    public static readonly DependencyProperty HasReplyToProperty =
        DependencyProperty.Register(nameof(HasReplyTo), typeof(bool), typeof(TweetUserRow), new PropertyMetadata(false));

    public string MetaLine
    {
        get => (string)GetValue(MetaLineProperty);
        private set => SetValue(MetaLineProperty, value);
    }

    public static readonly DependencyProperty MetaLineProperty =
        DependencyProperty.Register(nameof(MetaLine), typeof(string), typeof(TweetUserRow), new PropertyMetadata(string.Empty));

    private static void OnMetaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TweetUserRow)d).UpdateMetaLine();

    private void UpdateMetaLine()
    {
        var parts = new[] { UserScreenName, CreatedAtAbsoluteDisplay, CreatedAtRelativeDisplay };
        MetaLine = string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
