using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using PCL.App.Services;
using PCL.Core.Login;

namespace PCL.App.Views.Migrated;

public partial class MySkin : System.Windows.Controls.Grid {

    private LoginProfile? _profile;

    /// <summary>是否显示披风图层，XAML 中会设置该属性。</summary>
    public static readonly DependencyProperty HasCapeProperty = DependencyProperty.Register(
        nameof(HasCape), typeof(bool), typeof(MySkin), new PropertyMetadata(false));

    public bool HasCape {
        get => (bool)GetValue(HasCapeProperty);
        set => SetValue(HasCapeProperty, value);
    }

    public MySkin() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
        };
        BtnSkinRefresh.Click += (_, _) => SetPlayer(_profile ?? AppServices.CurrentLogin);
        BtnSkinSave.Click += (_, _) => SaveSkin();
    }

    /// <summary>展示指定账号的皮肤头像。未登录或图片加载失败时保持占位。</summary>
    public async void SetPlayer(LoginProfile? profile) {
        _profile = profile;
        var info = SkinDisplayInfo.From(profile);
        ToolTip = info.HasSkin
            ? $"{info.Name}\n{info.Uuid}\n右键可刷新或另存"
            : info.StatusText + "\n登录后可显示皮肤";
        if (!info.HasSkin) {
            ImgFore.Source = null;
            ImgBack.Source = null;
            return;
        }
        var image = await SkinImageService.LoadAsync(info.AvatarUrl);
        if (image is not null) ImgFore.Source = image;
    }

    private void SaveSkin() {
        if (ImgFore.Source is not BitmapSource source) {
            NotificationService.Warning("当前没有可保存的皮肤图像。", "皮肤");
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog {
            Filter = "PNG 图片|*.png",
            FileName = string.IsNullOrWhiteSpace(_profile?.Name) ? "skin.png" : _profile!.Name + ".png"
        };
        if (dialog.ShowDialog() != true) return;
        try {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
            NotificationService.Info("已保存：" + dialog.FileName, "皮肤");
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            NotificationService.Error("保存皮肤失败：" + ex.Message, "皮肤");
        }
    }

}
