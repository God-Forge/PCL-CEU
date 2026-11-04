using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using PCL.App.Controls;
using PCL.App.Services;

using System.IO;
using Brushes = System.Windows.Media.Brushes;
using RadioButton = System.Windows.Controls.RadioButton;
using ComboBox = System.Windows.Controls.ComboBox;
using Color = System.Windows.Media.Color;
using Clipboard = System.Windows.Clipboard;
namespace PCL.App.Views.Migrated;

public partial class PageOtherHelp : PCL.App.Controls.MyPageRight {

    public sealed class HelpDocument {
        public string Key { get; init; } = "";
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public string Search { get; init; } = "";
        public string? Logo { get; init; }
        public IReadOnlyList<string> Types { get; init; } = Array.Empty<string>();
        public bool ShowInSearch { get; init; } = true;
        public bool ShowInPublic { get; init; } = true;
        public bool IsEvent { get; init; }
        public string? EventType { get; init; }
        public string? EventData { get; init; }
        public string XamlContent { get; init; } = "";
    }

    public sealed class HelpRequestedEventArgs : EventArgs {
        public HelpDocument Document { get; }
        public PageOtherHelpDetail Detail { get; }

        public HelpRequestedEventArgs(HelpDocument document, PageOtherHelpDetail detail) {
            Document = document;
            Detail = detail;
        }
    }

    private readonly List<HelpDocument> _documents = [];
    private readonly Dictionary<string, HelpDocument> _lookup = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;
    private bool _loading;

    public event EventHandler<HelpRequestedEventArgs>? HelpRequested;

    public PageOtherHelp() {
        InitializeComponent();
        SearchBox.TextChanged += Search_TextChanged;
        Loaded += async (_, _) => {
            PageAnimationService.FadeIn(this);
            if (_initialized) return;
            _initialized = true;
            await LoadAsync();
        };
    }

    public void Refresh() {
        if (_loading) return;
        _ = LoadAsync();
    }

    private async Task LoadAsync() {
        if (_loading) return;
        _loading = true;
        PanLoad.Visibility = Visibility.Visible;
        PanBack.Visibility = Visibility.Collapsed;
        try {
            var result = await Task.Run(LoadDocuments);
            _documents.Clear();
            _lookup.Clear();
            foreach (var document in result.Documents) {
                _documents.Add(document);
                _lookup[document.Key] = document;
            }
            if (_documents.Count == 0) {
                ShowEmpty(result.Error ?? "未找到可用的帮助内容。");
                return;
            }
            BuildList();
            PanBack.Visibility = Visibility.Visible;
            if (!string.IsNullOrWhiteSpace(SearchBox.Text)) ApplySearch();
        } catch (Exception ex) {
            ShowEmpty("加载帮助列表失败：" + ex.Message);
        } finally {
            _loading = false;
            PanLoad.Visibility = Visibility.Collapsed;
        }
    }

    private static (List<HelpDocument> Documents, string? Error) LoadDocuments() {
        var documents = new List<HelpDocument>();
        string? error = null;
        try {
            var customFolder = Path.Combine(AppServices.Paths.Pcl, "Help");
            if (Directory.Exists(customFolder)) {
                foreach (var file in Directory.EnumerateFiles(customFolder, "*.json", SearchOption.AllDirectories)) {
                    var document = ReadFileDocument(file);
                    if (document is not null) documents.Add(document);
                }
            }
            var archivePath = FindHelpArchive();
            if (archivePath is null) {
                if (documents.Count == 0) error = "未找到 Help.zip，也未找到自定义帮助内容。";
                return (documents, error);
            }
            using var archive = ZipFile.OpenRead(archivePath);
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var archiveEntry in archive.Entries) entries[archiveEntry.FullName.Replace('\\', '/')] = archiveEntry;
            foreach (var entry in archive.Entries) {
                if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                var key = entry.FullName[..^5].Replace('\\', '/');
                try {
                    using var reader = new StreamReader(entry.Open());
                    var json = reader.ReadToEnd();
                    string? xaml = null;
                    if (entries.TryGetValue(key + ".xaml", out var xamlEntry)) {
                        using var xamlReader = new StreamReader(xamlEntry.Open());
                        xaml = xamlReader.ReadToEnd();
                    }
                    var document = ParseDocument(key, json, xaml);
                    if (document is not null) documents.Add(document);
                } catch (Exception ex) {
                    error = "部分帮助条目读取失败：" + ex.Message;
                }
            }
        } catch (Exception ex) {
            error = "读取帮助资源失败：" + ex.Message;
        }
        return (documents, error);
    }

    private static HelpDocument? ReadFileDocument(string path) {
        try {
            var key = Path.GetRelativePath(Path.Combine(AppServices.Paths.Pcl, "Help"), path).Replace('\\', '/');
            key = key[..^5];
            var xamlPath = Path.ChangeExtension(path, ".xaml");
            var xaml = File.Exists(xamlPath) ? File.ReadAllText(xamlPath) : null;
            return ParseDocument(key, File.ReadAllText(path), xaml);
        } catch {
            return null;
        }
    }

    private static HelpDocument? ParseDocument(string key, string json, string? xaml) {
        var data = JObject.Parse(json);
        var title = data["Title"]?.ToString();
        if (string.IsNullOrWhiteSpace(title)) return null;
        var isEvent = ReadBool(data, "IsEvent", false);
        var xamlContent = xaml ?? "";
        if (!isEvent && string.IsNullOrWhiteSpace(xamlContent)) return null;
        var types = data["Types"] is JArray typeArray
            ? typeArray.Select(token => token.ToString()).Where(type => !string.IsNullOrWhiteSpace(type)).ToList()
            : new List<string>();
        return new HelpDocument {
            Key = key,
            Title = title,
            Description = data["Description"]?.ToString() ?? "",
            Search = data["Keywords"]?.ToString() ?? "",
            Logo = data["Logo"]?.ToString(),
            Types = types,
            ShowInSearch = ReadBool(data, "ShowInSearch", true),
            ShowInPublic = ReadBool(data, "ShowInPublic", true),
            IsEvent = isEvent,
            EventType = data["EventType"]?.ToString(),
            EventData = data["EventData"]?.ToString(),
            XamlContent = xamlContent
        };
    }

    private static bool ReadBool(JObject data, string name, bool fallback) {
        var token = data[name];
        if (token is null || token.Type == JTokenType.Null) return fallback;
        if (token.Type == JTokenType.Boolean) return token.Value<bool>();
        return bool.TryParse(token.ToString(), out var value) ? value : fallback;
    }

    private static string? FindHelpArchive() {
        var candidates = new List<string>();
        AddCandidate(candidates, AppServices.Paths.Pcl, "Help.zip");
        AddCandidate(candidates, AppContext.BaseDirectory, "Help.zip");
        AddCandidate(candidates, AppContext.BaseDirectory, "Resources", "Help.zip");
        AddCandidate(candidates, Environment.CurrentDirectory, "Help.zip");
        AddCandidate(candidates, Environment.CurrentDirectory, "Plain Craft Launcher 2", "Resources", "Help.zip");
        AddParentCandidates(candidates, AppContext.BaseDirectory);
        AddParentCandidates(candidates, Environment.CurrentDirectory);
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(File.Exists);
    }

    private static void AddParentCandidates(List<string> candidates, string start) {
        DirectoryInfo? directory = new DirectoryInfo(Path.GetFullPath(start));
        for (var depth = 0; depth < 8 && directory is not null; depth++, directory = directory.Parent) {
            AddCandidate(candidates, directory.FullName, "Help.zip");
            AddCandidate(candidates, directory.FullName, "Resources", "Help.zip");
            AddCandidate(candidates, directory.FullName, "Plain Craft Launcher 2", "Resources", "Help.zip");
        }
    }

    private static void AddCandidate(List<string> candidates, string root, params string[] parts) {
        try {
            candidates.Add(Path.Combine(new[] { root }.Concat(parts).ToArray()));
        } catch {
        }
    }

    private void BuildList() {
        PanList.Children.Clear();
        PanSearch.Visibility = Visibility.Collapsed;
        PanList.Visibility = Visibility.Visible;
        var visible = _documents.Where(document => document.ShowInPublic).ToList();
        if (visible.Count == 0) {
            ShowEmpty("未找到可用的帮助内容。");
            return;
        }
        var types = new List<string>();
        foreach (var document in visible) {
            if (document.Types.Count == 0) {
                if (!types.Contains("其他")) types.Add("其他");
                continue;
            }
            foreach (var type in document.Types) {
                if (!types.Contains(type)) types.Add(type);
            }
        }
        if (types.Remove("指南")) types.Insert(0, "指南");
        foreach (var type in types) {
            var items = visible.Where(document => document.Types.Contains(type) || (type == "其他" && document.Types.Count == 0)).ToList();
            if (items.Count == 0) continue;
            var card = new MyCard { Title = type, Margin = new Thickness(0, 0, 0, 15), CanSwap = true };
            var content = new StackPanel { Margin = new Thickness(20, 40, 18, 18) };
            foreach (var document in items) content.Children.Add(BuildListItem(document));
            card.Children.Add(content);
            card.IsSwapped = type != "指南";
            PanList.Children.Add(card);
        }
    }

    private MyListItem BuildListItem(HelpDocument document) {
        var item = new MyListItem {
            Title = document.Title,
            Info = document.Description,
            Height = 42,
            IsScaleAnimationEnabled = false
        };
        item.Click += (_, _) => OpenDocument(document);
        return item;
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) {
        if (_documents.Count > 0) ApplySearch();
    }

    private void ApplySearch() {
        var query = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query)) {
            PanSearch.Visibility = Visibility.Collapsed;
            PanList.Visibility = Visibility.Visible;
            return;
        }
        var results = _documents
            .Where(document => document.ShowInPublic && document.ShowInSearch && Matches(document, query))
            .Take(50)
            .ToList();
        PanSearchList.Children.Clear();
        PanSearch.Title = results.Count == 0 ? "无搜索结果" : "搜索结果";
        if (results.Count == 0) {
            PanSearchList.Children.Add(new TextBlock {
                Text = "没有找到匹配的帮助内容。",
                Foreground = Brushes.Gray,
                Margin = new Thickness(4)
            });
        } else {
            foreach (var document in results) PanSearchList.Children.Add(BuildListItem(document));
        }
        PanSearch.Visibility = Visibility.Visible;
        PanList.Visibility = Visibility.Collapsed;
    }

    private static bool Matches(HelpDocument document, string query) {
        return document.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
               || document.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
               || document.Search.Contains(query, StringComparison.OrdinalIgnoreCase)
               || document.Types.Any(type => type.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private void OpenDocument(HelpDocument document) {
        if (document.IsEvent) {
            if (IsHelpEvent(document.EventType)) {
                OpenHelpKey(document.EventData);
                return;
            }
            try {
                CustomEventService.Raise(document.EventType, document.EventData);
            } catch (Exception ex) {
                NotificationService.Warning("执行帮助操作失败：" + ex.Message);
            }
            return;
        }
        try {
            var detail = new PageOtherHelpDetail();
            if (!detail.Init(document.XamlContent)) return;
            var handler = HelpRequested;
            if (handler is not null) {
                handler(this, new HelpRequestedEventArgs(document, detail));
                return;
            }
            var window = new Window {
                Title = document.Title,
                Width = 900,
                Height = 700,
                Background = Brushes.White,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = detail
            };
            if (System.Windows.Application.Current?.MainWindow is { } owner && owner.IsLoaded) window.Owner = owner;
            window.Show();
        } catch (Exception ex) {
            NotificationService.Warning("打开帮助内容失败：" + ex.Message);
        }
    }

    private void OpenHelpKey(string? key) {
        if (string.IsNullOrWhiteSpace(key)) return;
        var normalized = key.Trim().Replace('\\', '/');
        if (normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) normalized = normalized[..^5];
        if (_lookup.TryGetValue(normalized, out var document) && !document.IsEvent) {
            OpenDocument(document);
            return;
        }
        NotificationService.Warning("未找到关联的帮助条目：" + key);
    }

    private static bool IsHelpEvent(string? eventType) {
        return string.Equals(eventType, "打开帮助", StringComparison.OrdinalIgnoreCase)
               || string.Equals(eventType, "OpenHelp", StringComparison.OrdinalIgnoreCase)
               || string.Equals(eventType, "Help", StringComparison.OrdinalIgnoreCase);
    }

    private void ShowEmpty(string message) {
        PanSearch.Visibility = Visibility.Collapsed;
        PanList.Visibility = Visibility.Visible;
        PanList.Children.Clear();
        PanList.Children.Add(new TextBlock {
            Text = message,
            Margin = new Thickness(4, 8, 4, 8),
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap
        });
        PanBack.Visibility = Visibility.Visible;
        PanLoad.Visibility = Visibility.Collapsed;
    }

}
