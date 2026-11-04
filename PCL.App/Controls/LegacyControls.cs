using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.App.Controls;

/// <summary>
/// 兼容 PCL 旧 XAML 的控件属性层。页面迁移时先保证标记可编译，
/// 交互逻辑随后逐页接入 C# 服务。
/// </summary>
public class LegacyButton : System.Windows.Controls.Button {
    public string? EventType { get; set; }
    public string? EventData { get; set; }

    protected override void OnClick() {
        base.OnClick();
        CustomEventService.Raise(EventType ?? CustomEventService.GetEventType(this), EventData ?? CustomEventService.GetEventData(this));
    }
}

public class MyButton : LegacyButton {

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(MyButton),
            new PropertyMetadata("", (d, e) => ((MyButton)d).Content = e.NewValue));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public static readonly DependencyProperty ColorTypeProperty =
        DependencyProperty.Register(nameof(ColorType), typeof(string), typeof(MyButton), new PropertyMetadata(""));

    public string? ColorType { get => (string?)GetValue(ColorTypeProperty); set => SetValue(ColorTypeProperty, value); }
    public Thickness TextPadding { get; set; }

}

public class MyCard : System.Windows.Controls.StackPanel {

    private System.Windows.Controls.TextBlock? _titleBlock;

    private bool _canSwap;
    private bool _isSwapped;

    public string? Title {
        get => _titleBlock?.Text;
        set {
            if (string.IsNullOrWhiteSpace(value)) {
                if (_titleBlock is not null) Children.Remove(_titleBlock);
                _titleBlock = null;
                return;
            }
            if (_titleBlock is null) {
                _titleBlock = new System.Windows.Controls.TextBlock {
                    FontSize = 16,
                    FontWeight = System.Windows.FontWeights.SemiBold,
                    Margin = new Thickness(20, 12, 20, 10)
                };
                _titleBlock.MouseLeftButtonUp += (_, _) => {
                    if (_canSwap) IsSwapped = !IsSwapped;
                };
                Children.Insert(0, _titleBlock);
            }
            _titleBlock.Text = value;
            UpdateSwapState();
        }
    }
    public CornerRadius CornerRadius { get; set; } = new(6);
    public bool CanSwap {
        get => _canSwap;
        set { _canSwap = value; if (_titleBlock is not null) _titleBlock.Cursor = value ? System.Windows.Input.Cursors.Hand : null; }
    }
    public bool IsSwapped {
        get => _isSwapped;
        set { _isSwapped = value; UpdateSwapState(); }
    }
    public bool SwapLogoRight { get; set; }
    public bool UseAnimation { get; set; }

    private void UpdateSwapState() {
        var titleIndex = _titleBlock is null ? -1 : Children.IndexOf(_titleBlock);
        for (var i = 0; i < Children.Count; i++) {
            if (i == titleIndex) continue;
            Children[i].Visibility = _isSwapped ? Visibility.Collapsed : Visibility.Visible;
        }
    }

}

public class MyCheckBox : System.Windows.Controls.CheckBox {
    public string? Text { get => Content?.ToString(); set => Content = value; }
}

public class MyComboBox : System.Windows.Controls.ComboBox {
    public string? HintText { get; set; }
}

public class MyComboBoxItem : System.Windows.Controls.ComboBoxItem {
}

public class MyExtraButton : LegacyButton {
    public bool CanRightClick { get; set; }
    public string? Logo { get; set; }
    public double LogoScale { get; set; } = 1;
}

public class MyExtraTextButton : LegacyButton {
    public string? Logo { get; set; }
    public double LogoScale { get; set; } = 1;
    public string? Text { get => Content?.ToString(); set => Content = value; }
}

public class MyHint : System.Windows.Controls.Border {
    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(nameof(Theme), typeof(string), typeof(MyHint), new PropertyMetadata(""));

    public string? Theme { get => (string?)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }
    private System.Windows.Controls.TextBlock? _textBlock;
    public string? Text {
        get => _textBlock?.Text;
        set {
            _textBlock ??= new System.Windows.Controls.TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10, 6, 10, 6) };
            _textBlock.Text = value ?? "";
            Child = _textBlock;
        }
    }
    public bool CanClose { get; set; }
    public bool HasBorder { get; set; }
    public string? RelativeSetup { get; set; }
}

public class MyIconButton : LegacyButton {
    public string? Logo { get; set; }
    public double LogoScale { get; set; } = 1;
    public bool CanClick { get; set; } = true;
    public string? Theme { get; set; }
}

public class MyIconTextButton : LegacyButton {
    public string? Logo { get; set; }
    public double LogoScale { get; set; } = 1;
    public string? Text { get => Content?.ToString(); set => Content = value; }
    public string? ColorType { get; set; }
}

public class MyImage : System.Windows.Controls.Image {
    public ImageSource? FallbackSource { get; set; }

    public MyImage() {
        ImageFailed += (_, _) => {
            if (FallbackSource is not null) Source = FallbackSource;
        };
    }
}

public class MyListItem : LegacyButton {
    private System.Windows.Controls.StackPanel? _contentPanel;
    private System.Windows.Controls.TextBlock? _titleBlock;
    private System.Windows.Controls.TextBlock? _infoBlock;
    public object? Buttons { get; set; }

    public static readonly DependencyProperty CheckedProperty =
        DependencyProperty.Register(nameof(Checked), typeof(bool), typeof(MyListItem), new PropertyMetadata(false));

    public bool Checked { get => (bool)GetValue(CheckedProperty); set => SetValue(CheckedProperty, value); }

    public string? Title {
        get => _titleBlock?.Text;
        set { EnsureContent(); _titleBlock!.Text = value ?? ""; }
    }

    public string? Info {
        get => _infoBlock?.Text;
        set { EnsureContent(); _infoBlock!.Text = value ?? ""; }
    }

    private void EnsureContent() {
        if (_contentPanel is not null) return;
        _contentPanel = new System.Windows.Controls.StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _titleBlock = new System.Windows.Controls.TextBlock { FontSize = 14, FontWeight = FontWeights.SemiBold };
        _infoBlock = new System.Windows.Controls.TextBlock { FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray, TextTrimming = TextTrimming.CharacterEllipsis };
        _contentPanel.Children.Add(_titleBlock);
        _contentPanel.Children.Add(_infoBlock);
        Content = _contentPanel;
    }
    public string? Logo { get; set; }
    public double LogoScale { get; set; } = 1;
    public string? Type { get; set; }
    public bool IsScaleAnimationEnabled { get; set; }
    public double MinPaddingRight { get; set; }

    protected override void OnClick() {
        if (!string.IsNullOrWhiteSpace(Type)) {
            if (Type.Contains("Radio", StringComparison.OrdinalIgnoreCase)) Checked = true;
            if (Type.Contains("Check", StringComparison.OrdinalIgnoreCase)) Checked = !Checked;
        }
        base.OnClick();
    }
}

public class MyLoading : System.Windows.Controls.TextBlock {
    public bool AutoRun { get; set; }
    public bool HasAnimation { get; set; } = true;
    public bool ShowProgress { get; set; }
    public string? TextError { get; set; }
    public bool TextErrorInherit { get; set; }
}

public class MyMenuItem : System.Windows.Controls.MenuItem {
    public string? EventType { get; set; }
    public string? EventData { get; set; }
}

public class MyPageLeft : System.Windows.Controls.Grid {
    public string? AnimatedControl { get; set; }
}

public class MyPageRight : System.Windows.Controls.ContentControl {
    public string? PanScroll { get; set; }
}

public class MyRadioBox : System.Windows.Controls.RadioButton {
    public new bool Checked { get => IsChecked == true; set => IsChecked = value; }
    public string? Text { get => Content?.ToString(); set => Content = value; }
}

public class MyRadioButton : System.Windows.Controls.RadioButton {
    public new bool Checked { get => IsChecked == true; set => IsChecked = value; }
    public string? ColorType { get; set; }
    public string? Logo { get; set; }
    public double LogoScale { get; set; } = 1;
    public string? Text { get => Content?.ToString(); set => Content = value; }
}

public class MyScrollBar : System.Windows.Controls.Primitives.ScrollBar {
}

public class MyScrollViewer : System.Windows.Controls.ScrollViewer {
    public double DeltaMult { get; set; } = 1;

    protected override void OnPreviewMouseWheel(System.Windows.Input.MouseWheelEventArgs e) {
        ScrollToVerticalOffset(VerticalOffset - e.Delta * DeltaMult);
        e.Handled = true;
    }
}

public class MySearchBox : System.Windows.Controls.TextBox {
    public string? HintText { get; set; }
}

public class MySkin : System.Windows.Controls.ContentControl {

    public bool HasCape { get; set; }

    private readonly System.Windows.Controls.Image _avatar = new() {
        Stretch = System.Windows.Media.Stretch.Uniform,
        Width = 56,
        Height = 56
    };

    public MySkin() {
        Width = 64;
        Height = 64;
        Content = _avatar;
    }

    /// <summary>展示指定账号的皮肤头像；未登录或加载失败时保持空白。</summary>
    public async void SetPlayer(PCL.Core.Login.LoginProfile? profile) {
        var info = PCL.Core.Login.SkinDisplayInfo.From(profile);
        ToolTip = info.HasSkin ? info.Name + "\n" + info.Uuid : info.StatusText + "\n登录后可显示皮肤";
        if (!info.HasSkin) {
            _avatar.Source = null;
            return;
        }
        var image = await PCL.App.Services.SkinImageService.LoadAsync(info.AvatarUrl);
        if (image is not null) _avatar.Source = image;
    }

}

public class MySlider : System.Windows.Controls.Slider {
    public double MaxValue { get => Maximum; set => Maximum = value; }
    public string? ValueByKey { get; set; }
}

public class MyTextBox : System.Windows.Controls.TextBox {
    public bool HasBackground { get; set; }
    public string? HintText { get; set; }
    public bool ShowValidateResult { get; set; }
}

public sealed class ExportOption {
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Rules { get; set; }
    public bool DefaultChecked { get; set; }
    public bool RequireOptiFine { get; set; }
    public bool RequireModLoader { get; set; }
    public bool RequireModLoaderOrOptiFine { get; set; }
    public string? ShowRules { get; set; }
}

public class MyTextButton : LegacyButton {
    public string? Text { get => Content?.ToString(); set => Content = value; }
    public string? ColorType { get; set; }
}














