using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.App.Services;
using PCL.Core.Login;

namespace PCL.App.Dialogs;

/// <summary>
/// 账号管理：列出已保存账号，支持切换与删除。
/// 数据来自 <see cref="LoginSessionService"/>，不直接操作存储。
/// </summary>
public sealed class AccountManagerDialog : Window {

    private readonly StackPanel _list = new() { Margin = new Thickness(0, 4, 0, 0) };

    public AccountManagerDialog() {
        Title = "账号管理";
        Width = 520;
        Height = 420;
        MinWidth = 420;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(250, 250, 252));
        Content = BuildContent();
        Loaded += (_, _) => Refresh();
    }

    private UIElement BuildContent() {
        var root = new DockPanel { Margin = new Thickness(20) };

        var header = new TextBlock {
            Text = "已保存的账号",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var footer = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var close = new Button { Content = "关闭", Width = 90, Height = 30, IsDefault = true };
        close.Click += (_, _) => Close();
        footer.Children.Add(close);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        root.Children.Add(new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _list
        });
        return root;
    }

    private void Refresh() {
        _list.Children.Clear();
        var accounts = LoginSessionService.Accounts;
        if (accounts.Count == 0) {
            _list.Children.Add(new TextBlock {
                Text = "还没有保存的账号。登录后会出现在这里。",
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 20, 0, 0)
            });
            return;
        }
        foreach (var account in accounts) {
            _list.Children.Add(BuildRow(account));
        }
    }

    private UIElement BuildRow(AccountEntry account) {
        var isCurrent = string.Equals(account.Key, LoginSessionService.CurrentKey, StringComparison.OrdinalIgnoreCase);
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4), Height = 44 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock {
            Text = account.Name + (isCurrent ? "（当前）" : ""),
            FontSize = 13,
            FontWeight = isCurrent ? FontWeights.SemiBold : FontWeights.Normal
        });
        text.Children.Add(new TextBlock {
            Text = account.UserType + (string.IsNullOrWhiteSpace(account.RefreshToken) ? "" : " · 可续期"),
            FontSize = 11,
            Foreground = Brushes.Gray
        });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var switchButton = new Button {
            Content = isCurrent ? "使用中" : "切换",
            Width = 72,
            Height = 28,
            IsEnabled = !isCurrent,
            Margin = new Thickness(0, 0, 8, 0)
        };
        switchButton.Click += (_, _) => {
            if (LoginSessionService.SwitchTo(account.Key)) {
                NotificationService.Info("已切换到账号：" + account.Name, "账号管理");
                Refresh();
            }
        };
        Grid.SetColumn(switchButton, 1);
        grid.Children.Add(switchButton);

        var removeButton = new Button { Content = "删除", Width = 64, Height = 28 };
        removeButton.Click += (_, _) => {
            var confirm = MessageBox.Show(
                $"确定要删除账号「{account.Name}」吗？只是从列表中移除，不影响游戏内的玩家数据。",
                "删除账号", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
            if (LoginSessionService.RemoveAccount(account.Key)) {
                NotificationService.Info("已删除账号：" + account.Name, "账号管理");
                Refresh();
            }
        };
        Grid.SetColumn(removeButton, 2);
        grid.Children.Add(removeButton);

        return new Border {
            Background = isCurrent ? new SolidColorBrush(Color.FromRgb(240, 246, 255)) : Brushes.Transparent,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 0, 10, 0),
            Child = grid
        };
    }

}
