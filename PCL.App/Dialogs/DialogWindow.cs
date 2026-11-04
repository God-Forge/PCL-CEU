using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.Ui;

namespace PCL.App.Dialogs;

/// <summary>
/// 通用对话框窗口：文本、输入、单选三种形态共用一套布局。
/// 由 VB 版 MyMsgBox / MyMsgBoxInput / MyMsgBoxSelect 迁移而来。
/// </summary>
public sealed class DialogWindow : Window {

    private readonly DialogRequest _request;
    private readonly TextBlock _errorText = new() {
        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 40, 40)),
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 6, 0, 0),
        Visibility = Visibility.Collapsed
    };

    private TextBox? _input;
    private ListBox? _selection;

    public DialogWindow(DialogRequest request) {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        Title = request.Title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Background = new SolidColorBrush(request.IsWarn
            ? System.Windows.Media.Color.FromRgb(255, 246, 246)
            : System.Windows.Media.Color.FromRgb(252, 252, 252));
        Content = BuildContent();
    }

    /// <summary>对话框结果，关闭前为默认值。</summary>
    public DialogResult Result { get; private set; } = new();

    private UIElement BuildContent() {
        var panel = new StackPanel { Margin = new Thickness(20) };

        if (!string.IsNullOrWhiteSpace(_request.Caption)) {
            panel.Children.Add(new TextBlock {
                Text = _request.Caption,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 40))
            });
        }

        switch (_request.Kind) {
            case DialogKind.Input:
                BuildInput(panel);
                break;
            case DialogKind.Select:
                BuildSelection(panel);
                break;
        }

        panel.Children.Add(_errorText);
        panel.Children.Add(BuildButtons());
        return panel;
    }

    private void BuildInput(StackPanel panel) {
        if (!string.IsNullOrWhiteSpace(_request.HintText)) {
            panel.Children.Add(new TextBlock {
                Text = _request.HintText,
                FontSize = 11,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(130, 130, 130)),
                Margin = new Thickness(0, 10, 0, 4)
            });
        }
        _input = new TextBox {
            Text = _request.DefaultInput,
            Height = 30,
            Margin = new Thickness(0, 10, 0, 0),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(_input);
        Loaded += (_, _) => {
            _input.Focus();
            _input.CaretIndex = _input.Text.Length;
        };
    }

    private void BuildSelection(StackPanel panel) {
        _selection = new ListBox { Margin = new Thickness(0, 10, 0, 0), MaxHeight = 260 };
        foreach (var option in _request.Options) {
            _selection.Items.Add(option);
        }
        if (_selection.Items.Count > 0) _selection.SelectedIndex = 0;
        panel.Children.Add(_selection);
    }

    private UIElement BuildButtons() {
        var buttons = _request.Buttons;
        var row = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        for (var i = 0; i < buttons.Count; i++) {
            var index = i + 1;
            var button = new Button {
                Content = buttons[i],
                Width = 96,
                Height = 32,
                Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0),
                IsDefault = i == 0,
                IsCancel = i == 1
            };
            button.Click += (_, _) => Confirm(index);
            row.Children.Add(button);
        }
        return row;
    }

    private void Confirm(int buttonIndex) {
        if (buttonIndex == 1 && _request.Kind == DialogKind.Input) {
            var text = _input?.Text ?? "";
            var error = _request.Validator?.Invoke(text);
            if (error is not null) {
                _errorText.Text = error;
                _errorText.Visibility = Visibility.Visible;
                return;
            }
            Result = new DialogResult { ButtonIndex = 1, Input = text };
        } else if (buttonIndex == 1 && _request.Kind == DialogKind.Select) {
            var index = _selection?.SelectedIndex ?? -1;
            if (index < 0) {
                _errorText.Text = "请选择一项。";
                _errorText.Visibility = Visibility.Visible;
                return;
            }
            Result = new DialogResult { ButtonIndex = 1, Selection = index };
        } else {
            Result = PCL.Core.Ui.DialogResult.Cancel(buttonIndex);
        }
        Close();
    }

}
