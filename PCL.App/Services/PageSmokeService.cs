using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PCL.App.Services;

public static class PageSmokeService {

    public static int Run() {
        var app = new PCL.App.App();
        app.InitializeComponent();
        ThemeService.Apply(app);
        SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

        var pages = Assembly.GetExecutingAssembly().GetTypes()
            .Where(type => type.Namespace == "PCL.App.Views.Migrated"
                           && type.Name.StartsWith("Page", StringComparison.Ordinal)
                           && !type.IsAbstract
                           && !type.IsNested
                           && typeof(FrameworkElement).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var host = new ContentControl();
        var window = new Window {
            Width = 1000,
            Height = 800,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Opacity = 0,
            Left = -10000,
            Top = -10000,
            Content = host
        };
        window.Show();

        var failures = 0;
        foreach (var type in pages) {
            try {
                var page = (FrameworkElement)Activator.CreateInstance(type)!;
                host.Content = page;
                window.UpdateLayout();
                Pump(TimeSpan.FromMilliseconds(120));
                Console.WriteLine($"OK\t{type.Name}");
            } catch (Exception ex) {
                failures++;
                var root = ex;
                while (root.InnerException is not null) root = root.InnerException;
                Console.WriteLine($"FAIL\t{type.Name}\t{root.GetType().Name}: {root.Message}");
            } finally {
                host.Content = null;
            }
        }

        window.Close();
        Console.WriteLine($"页面冒烟：{pages.Count - failures}/{pages.Count} 通过");
        app.Shutdown();
        return failures == 0 ? 0 : 1;
    }

    private static void Pump(TimeSpan duration) {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) {
            Interval = duration
        };
        timer.Tick += (_, _) => {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

}
