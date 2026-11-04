using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace PCL.App.Views;

public partial class PageCatalogView : System.Windows.Controls.UserControl {

    private readonly List<Type> _pages;

    public PageCatalogView() {
        InitializeComponent();
        _pages = Assembly.GetExecutingAssembly().GetTypes()
            .Where(type => type.Namespace == "PCL.App.Views.Migrated" && !type.IsAbstract)
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        PageList.ItemsSource = _pages.Select(type => type.Name).ToList();
    }

    private void PageList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (PageList.SelectedIndex < 0 || PageList.SelectedIndex >= _pages.Count) return;
        try {
            var page = Activator.CreateInstance(_pages[PageList.SelectedIndex]);
            PageHost.Content = page;
            ErrorText.Visibility = Visibility.Collapsed;
        } catch (Exception ex) {
            PageHost.Content = null;
            ErrorText.Text = "页面加载失败（缺少资源或动态数据）：\r\n" + ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

}
