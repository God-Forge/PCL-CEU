using System.IO;
using PCL.App.Controls;
using System.Windows;
using PCL.App.Services;

namespace PCL.App.Views.Migrated;

public partial class PageOtherHelpDetail : PCL.App.Controls.MyPageRight {

    public PageOtherHelpDetail() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
        };
    }

    public bool Init(string xamlContent) {
        try {
            if (string.IsNullOrWhiteSpace(xamlContent)) return false;
            var content = xamlContent.Replace("clr-namespace:PCL;assembly=PCL-0.9.2ceu", "clr-namespace:PCL.App.Controls")
                .Replace("clr-namespace:PCL", "clr-namespace:PCL.App.Controls");
            if (!content.TrimStart().StartsWith("<StackPanel", StringComparison.OrdinalIgnoreCase)) {
                content = "<StackPanel xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                          "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
                          "xmlns:local=\"clr-namespace:PCL.App.Controls\">" + content + "</StackPanel>";
            }
            var element = System.Windows.Markup.XamlReader.Parse(content) as UIElement;
            if (element is null) return false;
            PanCustom.Children.Clear();
            PanCustom.Children.Add(element);
            return true;
        } catch (Exception ex) {
            System.Windows.MessageBox.Show("加载帮助内容失败：" + ex.Message, "帮助", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    public bool LoadFile(string path) {
        if (!File.Exists(path)) return false;
        return Init(File.ReadAllText(path));
    }

}



