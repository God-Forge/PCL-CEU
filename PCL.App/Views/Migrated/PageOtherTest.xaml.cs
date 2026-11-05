using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using PCL.App.Controls;
using PCL.App.Services;
using PCL.Core.Download;
using MeloongCore;
using PCL.Core.Java;
using PCL.Core.Login;
using PCL.Core.Memory;
using PCL.Core.Minecraft;
using PCL.Core.Mods;
using PCL.Core.Settings;
using PCL.Core.Tools;
using PCL.Core.Worlds;

namespace PCL.App.Views.Migrated;

public partial class PageOtherTest : PCL.App.Controls.MyPageRight {

    /// <summary>设置键：测速后自动选中的下载源名称。</summary>
    private const string SourcePreferenceKey = "DownloadSourcePreference";

    private readonly DispatcherTimer _liveLogTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private long _liveLogLength;

    public PageOtherTest() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            WireEvents();
            RefreshMemoryStatus();
        };
        Unloaded += (_, _) => StopLiveLog();
        _liveLogTimer.Tick += (_, _) => ReadLiveLog();
    }

    private static string InstancePath {
        get {
            var instance = CurrentInstance;
            return instance is null ? "" : Path.Combine(instance.Folder.Versions, instance.Version.Folder);
        }
    }

    private static string MinecraftPath => AppServices.SelectedMinecraftFolder?.Path ?? AppServices.Paths.Minecraft;

    private static string InstanceName {
        get {
            var instance = CurrentInstance;
            return instance?.Version.Folder ?? "";
        }
    }

    private static MinecraftInstanceLocation? CurrentInstance {
        get {
            var selected = AppServices.SelectedInstanceName;
            return (!string.IsNullOrWhiteSpace(selected) ? AppServices.MinecraftFolders.FindInstance(selected) : null)
                   ?? AppServices.MinecraftFolders.ScanAll().FirstOrDefault();
        }
    }

    private static string BackupRoot => Path.Combine(AppServices.Paths.Pcl, "WorldBackups");
    private static string SnapshotRoot => Path.Combine(AppServices.Paths.Pcl, "InstanceSnapshots");
    private static string LogPath => Path.Combine(AppServices.Paths.Logs, "PCL-CSharp.log");

    private void WireEvents() {
        BtnJrrp.Click += (_, _) => ShowLuck();
        BtnMemoryQuick.Click += (_, _) => RunMemory(false);
        BtnMemoryDeep.Click += (_, _) => RunMemory(true);
        BtnMemoryCurrent.Click += (_, _) => RunMemoryKind(MemoryOptimizationKind.CurrentProcess);
        BtnMemoryWorkingSet.Click += (_, _) => RunMemoryKind(MemoryOptimizationKind.WorkingSets);
        BtnMemorySystemWorkingSet.Click += (_, _) => RunMemoryKind(MemoryOptimizationKind.SystemWorkingSet);
        BtnMemoryModified.Click += (_, _) => RunMemoryKind(MemoryOptimizationKind.ModifiedPageList);
        BtnMemoryStandby.Click += (_, _) => RunMemoryKind(MemoryOptimizationKind.StandbyList);
        BtnMemoryPriority0.Click += (_, _) => RunMemoryKind(MemoryOptimizationKind.Priority0StandbyList);
        BtnMemoryRefresh.Click += (_, _) => RefreshMemoryStatus();
        BtnMemoryAdmin.Click += (_, _) => RunElevatedMemory();

        BtnSaves.Click += (_, _) => OpenInstanceFolder("saves");
        BtnChooseWorld.Click += (_, _) => ChooseWorld();
        BtnHome.Click += (_, _) => OpenUrl("https://github.com/Querz/mcaselector");
        BtnLaunch.Click += (_, _) => LaunchMcaSelector();

        BtnPayloadEncode.Click += (_, _) => EncodePayload();
        BtnPayloadDecode.Click += (_, _) => DecodePayload();
        BtnPayloadImport.Click += (_, _) => ImportPayload();

        BtnWorldBackup.Click += (_, _) => BackupSaves();
        BtnWorldRestore.Click += (_, _) => RestoreLatestSaves();
        BtnWorldBackupOpen.Click += (_, _) => OpenFolder(BackupRoot);
        BtnWorldManage.Click += (_, _) => ShowWorlds();
        BtnWorldBackupSelected.Click += (_, _) => BackupWorld();
        BtnWorldRestoreSelected.Click += (_, _) => RestoreWorld();

        BtnInstanceCheck.Click += (_, _) => CheckInstance();
        BtnInstanceRepair.Click += (_, _) => _ = RepairInstanceAsync();
        BtnModsCheck.Click += (_, _) => CheckMods();
        BtnCrashDiagnostic.Click += (_, _) => ExportDiagnostic();
        BtnDiagnosticShare.Click += (_, _) => CopyDiagnostic();
        BtnCrashHistory.Click += (_, _) => ShowCrashHistory();
        BtnCrashAnalyze.Click += (_, _) => AnalyzeCrashLogs();
        BtnCrashFixUndo.Click += (_, _) => UndoLastAutoFix();
        BtnImportModpack.Click += (_, _) => ImportModpack();
        BtnLogViewer.Click += (_, _) => ShowLatestLog();
        BtnCrashCompare.Click += (_, _) => CompareCrashReports();
        BtnEnvironment.Click += (_, _) => ShowEnvironment();
        BtnAdvancedAudit.Click += (_, _) => RunAudit();

        BtnCloneInstance.Click += (_, _) => CloneInstance();
        BtnSnapshot.Click += (_, _) => CreateSnapshot();
        BtnSnapshotRestore.Click += (_, _) => RestoreSnapshot();
        BtnMigrate.Click += (_, _) => MigrateInstance();
        BtnOfflinePack.Click += (_, _) => ExportOfflinePack();
        BtnExportLaunchConfig.Click += (_, _) => ExportLaunchConfig();
        BtnImportLaunchConfig.Click += (_, _) => ImportLaunchConfig();
        BtnLaunchTiming.Click += (_, _) => ShowLaunchTiming();

        BtnResourceScan.Click += (_, _) => ScanResources();
        BtnResourceToggle.Click += (_, _) => ToggleResource();
        BtnResourceOpen.Click += (_, _) => OpenInstanceFolder("resourcepacks");
        BtnResourceBackup.Click += (_, _) => BackupResources();
        BtnResourceRestore.Click += (_, _) => RestoreResources();
        BtnModAutoFix.Click += (_, _) => AutoFixMods();
        BtnModConfig.Click += (_, _) => OpenInstanceFolder("config");
        BtnSpeedTest.Click += (_, _) => _ = SpeedTestAsync();
        BtnSourceHistory.Click += (_, _) => ShowSourceHistory();

        BtnJvmLow.Click += (_, _) => ShowJvmPreset(0);
        BtnJvmBalanced.Click += (_, _) => ShowJvmPreset(1);
        BtnJvmHigh.Click += (_, _) => ShowJvmPreset(2);
        BtnProcessMonitor.Click += (_, _) => MonitorProcess();
        BtnLiveLog.Click += (_, _) => StartLiveLog();
        BtnLiveLogStop.Click += (_, _) => StopLiveLog();

        BtnJavaPack.Click += (_, _) => PackJava();
        BtnJavaPackImport.Click += (_, _) => ImportJavaPack();
        BtnJavaPackOpen.Click += (_, _) => OpenFolder(AppServices.Paths.Java);
    }

    private void ShowLuck() {
        var luck = DailyLuck.GetToday();
        var image = luck.Value < 10 ? MessageBoxImage.Warning : MessageBoxImage.Information;
        System.Windows.MessageBox.Show(
            $"今日人品：{luck.Value}\r\n{luck.Comment}",
            "今日人品", MessageBoxButton.OK, image);
    }

    private void RefreshMemoryStatus() {
        try {
            var status = MemoryOptimizer.GetStatus();
            LabMemoryStatus.Text = $"当前可用物理内存：{status.AvailableMB:N0} MB / {status.TotalMB:N0} MB";
            LabMemoryAdmin.Text = MemoryOptimizer.IsAdministrator
                ? "当前权限：管理员，可执行完整内存优化。"
                : "当前权限：普通用户；待机列表等系统级清理需要管理员权限。";
            BtnMemoryAdmin.Visibility = MemoryOptimizer.IsAdministrator ? Visibility.Collapsed : Visibility.Visible;
        } catch (Exception ex) {
            LabMemoryStatus.Text = "读取内存状态失败：" + ex.Message;
        }
    }

    private void RunMemory(bool includeStandby) {
        try {
            var result = MemoryOptimizer.OptimizeAll(includeStandby);
            RefreshMemoryStatus();
            NotificationService.Show(result.Message, "内存优化",
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        } catch (Exception ex) {
            NotificationService.Error("内存优化失败：" + ex.Message);
        }
    }

    private void RunMemoryKind(MemoryOptimizationKind kind) {
        try {
            var result = MemoryOptimizer.Optimize(kind);
            RefreshMemoryStatus();
            NotificationService.Show(result.Message, "内存优化",
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        } catch (Exception ex) {
            NotificationService.Error("内存优化失败：" + ex.Message);
        }
    }

    private void RunElevatedMemory() {
        try {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe)) return;
            Process.Start(new ProcessStartInfo {
                FileName = exe,
                Arguments = "--elevated memory",
                Verb = "runas",
                UseShellExecute = true
            });
        } catch (Exception ex) {
            NotificationService.Error("请求管理员权限失败：" + ex.Message);
        }
    }

    private void LaunchMcaSelector() {
        try {
            McaSelectorService.Launch(AppServices.Paths.Base, Path.Combine(InstancePath, "saves"));
        } catch (Exception ex) {
            NotificationService.Error("启动 MCA Selector 失败：" + ex.Message);
        }
    }

    private void EncodePayload() {
        var path = MeloongCore.Wpf.Dialogs.SaveFile("保存 Payload", InstanceName + ".payload",
            AppServices.Paths.Pcl, [("payload", "Payload 文件")]);
        if (string.IsNullOrWhiteSpace(path)) return;
        try {
            PayloadService.Encode(AppServices.Paths.Base, InstancePath, path, InstanceName);
            NotificationService.Info("Payload 已创建：" + path);
        } catch (Exception ex) {
            NotificationService.Error("打包 Payload 失败：" + ex.Message);
        }
    }

    private void DecodePayload() {
        var files = MeloongCore.Wpf.Dialogs.SelectFile("选择 Payload", false, AppServices.Paths.Pcl,
            "Payload 文件|*.payload");
        if (files.Count == 0) return;
        var folders = MeloongCore.Wpf.Dialogs.SelectFolder("选择解包目标文件夹", false, AppServices.Paths.Pcl);
        if (folders.Count == 0) return;
        try {
            PayloadService.Decode(AppServices.Paths.Base, files[0], folders[0]);
            NotificationService.Info("Payload 已解包：" + folders[0]);
        } catch (Exception ex) {
            NotificationService.Error("解包 Payload 失败：" + ex.Message);
        }
    }

    private void ImportPayload() {
        var files = MeloongCore.Wpf.Dialogs.SelectFile("选择 Payload", false, AppServices.Paths.Pcl,
            "Payload 文件|*.payload");
        if (files.Count == 0) return;
        var folders = MeloongCore.Wpf.Dialogs.SelectFolder("选择 Minecraft 文件夹", false, MinecraftPath);
        if (folders.Count == 0) return;
        try {
            var name = PayloadService.Import(AppServices.Paths.Base, files[0], folders[0]);
            NotificationService.Info("Payload 已导入为实例：" + name);
        } catch (Exception ex) {
            NotificationService.Error("导入 Payload 失败：" + ex.Message);
        }
    }
    private void ChooseWorld() {
        var folders = MeloongCore.Wpf.Dialogs.SelectFolder("选择世界文件夹", false, Path.Combine(InstancePath, "saves"));
        if (folders.Count == 0) return;
        var world = folders[0];
        var backup = new WorldBackupService().CreateBackup(world, BackupRoot, InstanceName, Path.GetFileName(world.TrimEnd('\\', '/')));
        NotificationService.Info("世界已备份：" + backup.Path);
        try {
            McaSelectorService.Launch(AppServices.Paths.Base, world);
        } catch (Exception ex) {
            NotificationService.Warning("启动 MCA Selector 失败：" + ex.Message);
        }
    }

    private void BackupSaves() {
        var saves = Path.Combine(InstancePath, "saves");
        if (!Directory.Exists(saves)) {
            NotificationService.Warning("当前实例没有 saves 文件夹。");
            return;
        }
        var backup = new WorldBackupService().CreateBackup(saves, BackupRoot, InstanceName, "saves", WorldBackupKind.Saves);
        new WorldBackupService().Prune(BackupRoot, InstanceName, 10);
        NotificationService.Info("存档备份已创建：" + backup.Path);
    }

    private void RestoreLatestSaves() {
        var backups = new WorldBackupService().ListBackups(BackupRoot, InstanceName)
            .Where(item => item.Kind == WorldBackupKind.Saves)
            .ToList();
        if (backups.Count == 0) {
            NotificationService.Warning("没有可恢复的存档备份。");
            return;
        }
        var target = Path.Combine(InstancePath, "saves");
        new WorldBackupService().Restore(backups[0].Path, target, BackupRoot, InstanceName);
        NotificationService.Info("已恢复最近存档备份。");
    }

    private void ShowWorlds() {
        var worlds = AdvancedAuditService.ListWorlds(InstancePath);
        if (worlds.Count == 0) {
            NotificationService.Info("未找到存档。");
            return;
        }
        var text = string.Join("\r\n", worlds.Select(world =>
            $"{world.Name}  {FormatSize(world.Size)}  最后游玩 {world.LastWrite:yyyy-MM-dd HH:mm}"));
        System.Windows.MessageBox.Show(text, "存档列表", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BackupWorld() {
        var worlds = AdvancedAuditService.ListWorlds(InstancePath);
        if (worlds.Count == 0) {
            NotificationService.Warning("未找到存档。");
            return;
        }
        var selected = ChooseItem(worlds.Select(world => world.Name).ToList(), "选择要备份的存档");
        if (selected is null) return;
        var world = worlds.First(item => item.Name == selected);
        var backup = AdvancedMaintenanceService.BackupWorld(world.Path, BackupRoot, InstanceName);
        NotificationService.Info("世界备份已创建：" + backup);
    }

    private void RestoreWorld() {
        var backups = new WorldBackupService().ListBackups(BackupRoot, InstanceName)
            .Where(item => item.Kind == WorldBackupKind.World)
            .ToList();
        if (backups.Count == 0) {
            NotificationService.Warning("没有可恢复的世界备份。");
            return;
        }
        var selected = ChooseItem(backups.Select(item => $"{item.WorldName}  {item.CreatedUtc:MM-dd HH:mm}").ToList(), "选择要恢复的世界备份");
        if (selected is null) return;
        var backup = backups[backups.Select(item => $"{item.WorldName}  {item.CreatedUtc:MM-dd HH:mm}").ToList().IndexOf(selected)];
        var target = Path.Combine(InstancePath, "saves", backup.WorldName);
        new WorldBackupService().Restore(backup.Path, target, BackupRoot, InstanceName);
        NotificationService.Info("世界已恢复。");
    }

    private void CheckInstance() {
        var result = InstanceCheckService.Check(InstancePath, MinecraftPath);
        System.Windows.MessageBox.Show(InstanceReportFormatter.FormatCheck(result), "实例体检", MessageBoxButton.OK,
            result.HasErrors ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    private async Task RepairInstanceAsync() {
        try {
            var result = await new VanillaInstallService().RepairAsync(MinecraftPath, InstancePath, InstanceName);
            NotificationService.Info(result);
        } catch (Exception ex) {
            NotificationService.Error("修复基础文件失败：" + ex.Message);
        }
    }

    private void CheckMods() {
        var result = ModScanner.Scan(Path.Combine(InstancePath, "mods"));
        System.Windows.MessageBox.Show(InstanceReportFormatter.FormatModScan(result), "Mod 检查", MessageBoxButton.OK,
            result.HasErrors ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    private void ExportDiagnostic() {
        var path = MeloongCore.Wpf.Dialogs.SaveFile("导出崩溃诊断包", $"{InstanceName}-diagnostic.zip",
            AppServices.Paths.Pcl, [("zip", "ZIP 压缩包")]);
        if (string.IsNullOrWhiteSpace(path)) return;
        try {
            var result = DiagnosticPackageService.Create(InstancePath, MinecraftPath, path);
            NotificationService.Info($"诊断包已导出：{result.Path}\r\n包含 {result.FileCount} 个文件，{FormatSize(result.Size)}。");
        } catch (Exception ex) {
            NotificationService.Error("导出诊断包失败：" + ex.Message);
        }
    }

    private void CopyDiagnostic() {
        var history = CrashHistoryService.Latest(Path.Combine(AppServices.Paths.Pcl, "CrashHistory.json"));
        var log = LogViewerService.ExtractLatest(InstancePath, 12);
        var text = DiagnosticShareService.Build(
            InstanceName,
            history?.Reasons ?? Array.Empty<string>(),
            history?.ExitCode ?? 0,
            log.Lines);
        ClipboardService.SetText(text);
        NotificationService.Info("诊断摘要已复制到剪贴板。");
    }

    /// <summary>从本地 zip 导入整合包到当前版本目录。</summary>
    private async void ImportModpack() {
        var files = MeloongCore.Wpf.Dialogs.SelectFile("选择整合包", false, AppServices.Paths.Pcl,
            "整合包|*.zip|所有文件|*.*");
        if (files.Count == 0) return;
        var zip = files[0];
        var name = System.IO.Path.GetFileNameWithoutExtension(zip);
        // 先预检：告诉用户会装什么、会不会覆盖已有实例
        var preview = PCL.Core.Modpack.ModpackPreviewService.Preview(zip, AppServices.Paths.Versions, name);
        if (!preview.Valid) {
            NotificationService.Error(preview.Error ?? "整合包无效。", "导入整合包失败");
            return;
        }
        // 目标确认与编辑：可直接修改各组件版本，或恢复为整合包声明的值
        var dialog = new PCL.App.Dialogs.InstallTargetDialog(preview) {
            Owner = System.Windows.Window.GetWindow(this)
        };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;
        var chosen = dialog.Result;
        if (PCL.Core.Minecraft.InstallTargetComparer.HasChanges(preview.Target, chosen)) {
            Logger.Info($"[整合包] 用户调整目标：{PCL.Core.Minecraft.InstallTargetComparer.Describe(preview.Target)}"
                        + $" → {PCL.Core.Minecraft.InstallTargetComparer.Describe(chosen)}");
        }        NotificationService.Info($"正在导入《{name}》，完成后会提示结果。", "导入整合包");
        try {
            var result = await new PCL.Core.Modpack.ModpackImportService()
                .ImportAsync(zip, AppServices.Paths.Versions, name);
            if (result.Success) {
                Logger.Info($"[整合包] 已导入到 {result.TargetFolder}，下载 {result.Downloaded} 个文件");
                NotificationService.Info(
                    $"整合包已导入：{System.IO.Path.GetFileName(result.TargetFolder)}\r\n下载 {result.Downloaded} 个文件，解压 {result.Overrides} 个覆盖文件。",
                    "导入整合包");
            } else {
                NotificationService.Error(result.Message, "导入整合包失败");
            }
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "导入整合包失败");
            NotificationService.Error("导入失败：" + ex.Message, "导入整合包失败");
        }
    }

    /// <summary>撤销上次启动失败时应用的一键修复（恢复实例 JVM 参数）。</summary>
    private void UndoLastAutoFix() {
        var record = PCL.Core.Minecraft.CrashFixUndoStore.Load(AppServices.Paths.Pcl);
        if (record is null) {
            NotificationService.Info("没有可撤销的修复记录。", "撤销修复");
            return;
        }
        var (instancePath, previous) = record.Value;
        try {
            AppServices.Settings.Set(PCL.Core.Settings.InstanceSettingKeys.JvmArguments, previous, instancePath);
            PCL.Core.Minecraft.CrashFixUndoStore.Clear(AppServices.Paths.Pcl);
            var display = string.IsNullOrWhiteSpace(previous) ? "（空）" : previous;
            Logger.Info($"[一键修复] 已撤销，{instancePath} 的 JVM 参数恢复为：{display}");
            NotificationService.Info($"已恢复 JVM 参数为：{display}", "撤销修复");
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "撤销一键修复失败");
            NotificationService.Error("撤销失败：" + ex.Message, "撤销修复");
        }
    }

    /// <summary>分析实例日志或用户选定的崩溃日志，输出原因与修复建议。</summary>
    private void AnalyzeCrashLogs() {
        var logs = CrashAnalysisService.LocateCrashLogs(InstancePath);
        if (logs.Count == 0) {
            var picked = MeloongCore.Wpf.Dialogs.SelectFile("选择崩溃日志", false, InstancePath,
                "日志文件|*.log;*.txt|所有文件|*.*");
            if (picked.Count == 0) {
                NotificationService.Warning("实例中没有找到日志，也没有选择文件。", "崩溃分析");
                return;
            }
            logs = picked;
        }
        var report = CrashAnalysisService.AnalyzeFiles(logs);
        Logger.Info($"[崩溃分析] 分析了 {logs.Count} 个文件，发现 {report.Diagnoses.Count} 个问题");
        System.Windows.MessageBox.Show(CrashAnalysisService.Format(report), "崩溃分析", MessageBoxButton.OK,
            report.HasFindings ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    private void ShowCrashHistory() {
        var path = Path.Combine(AppServices.Paths.Pcl, "CrashHistory.json");
        System.Windows.MessageBox.Show(CrashHistoryService.Format(path), "崩溃历史", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowLatestLog() {
        var result = LogViewerService.ExtractLatest(InstancePath);
        System.Windows.MessageBox.Show(LogViewerService.Format(result), "最新日志", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CompareCrashReports() {
        var folder = Path.Combine(InstancePath, "crash-reports");
        if (!Directory.Exists(folder)) {
            NotificationService.Warning("当前实例没有崩溃报告。");
            return;
        }
        var files = Directory.EnumerateFiles(folder, "*.txt").OrderByDescending(File.GetLastWriteTime).Take(2).ToList();
        if (files.Count < 2) {
            NotificationService.Warning("至少需要两份崩溃报告才能对比。");
            return;
        }
        System.Windows.MessageBox.Show(AdvancedDiagnosticsService.CompareLogs(files[1], files[0]),
            "崩溃报告对比", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowEnvironment() {
        var java = JavaScanner.FindBest(JavaScanner.DefaultRoots(AppServices.Paths.Base,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), CurrentInstance?.Version.JavaVersionMajor ?? 21);
        var info = AdvancedDiagnosticsService.CollectEnvironment(java?.JavaExe);
        System.Windows.MessageBox.Show($"系统：{info.Os}\r\n显卡：{info.Gpu}\r\nJava：{info.Java}", "环境诊断",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RunAudit() {
        var report = AdvancedAuditService.FormatReport(InstancePath, MinecraftPath,
            Path.Combine(AppServices.Paths.Pcl, "LaunchTiming.json"));
        System.Windows.MessageBox.Show(report, "高级体检", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CloneInstance() {
        var name = InputDialog.Show("克隆实例", "输入新实例名称。", InstanceName + "-Clone");
        if (string.IsNullOrWhiteSpace(name)) return;
        var target = Path.Combine(Path.GetDirectoryName(InstancePath)!, name.Trim());
        try {
            InstanceCloneService.Clone(InstancePath, target, new InstanceCloneOptions {
                IncludeSaves = true, IncludeMods = true, IncludeConfig = true,
                IncludeResourcePacks = true, IncludeShaderPacks = true
            });
            NotificationService.Info("实例已克隆：" + target);
        } catch (Exception ex) {
            NotificationService.Error("克隆实例失败：" + ex.Message);
        }
    }

    private void CreateSnapshot() {
        var info = InstanceSnapshotService.Create(InstancePath, SnapshotRoot);
        NotificationService.Info($"快照已创建：{info.Name}\r\n{FormatSize(info.Size)}");
    }

    private void RestoreSnapshot() {
        var snapshots = InstanceSnapshotService.List(InstancePath, SnapshotRoot);
        if (snapshots.Count == 0) {
            NotificationService.Warning("当前实例没有快照。");
            return;
        }
        var selected = ChooseItem(snapshots.Select(item => $"{item.Name}  {item.Created:MM-dd HH:mm}").ToList(), "选择要恢复的快照");
        if (selected is null) return;
        var index = snapshots.Select(item => $"{item.Name}  {item.Created:MM-dd HH:mm}").ToList().IndexOf(selected);
        InstanceSnapshotService.Restore(snapshots[index].Path, InstancePath);
        NotificationService.Info("快照已恢复。");
    }

    private void MigrateInstance() {
        var folders = MeloongCore.Wpf.Dialogs.SelectFolder("选择目标 Minecraft 文件夹", false, MinecraftPath);
        if (folders.Count == 0) return;
        var target = Path.Combine(folders[0], "versions", InstanceName);
        try {
            AdvancedMaintenanceService.Migrate(InstancePath, target, false);
            NotificationService.Info("实例已迁移：" + target);
        } catch (Exception ex) {
            NotificationService.Error("迁移实例失败：" + ex.Message);
        }
    }

    private void ExportOfflinePack() {
        var path = MeloongCore.Wpf.Dialogs.SaveFile("导出离线整合包", InstanceName + "-offline.zip",
            AppServices.Paths.Pcl, [("zip", "ZIP 压缩包")]);
        if (string.IsNullOrWhiteSpace(path)) return;
        try {
            var result = AdvancedMaintenanceService.ExportOfflinePack(InstancePath, MinecraftPath, path);
            NotificationService.Info("离线整合包已导出：" + result);
        } catch (Exception ex) {
            NotificationService.Error("导出离线整合包失败：" + ex.Message);
        }
    }

    private void ExportLaunchConfig() {
        var path = MeloongCore.Wpf.Dialogs.SaveFile("导出启动配置", InstanceName + "-launch.json",
            AppServices.Paths.Pcl, [("json", "JSON 配置")]);
        if (string.IsNullOrWhiteSpace(path)) return;
        var values = new Dictionary<string, object?> {
            ["VersionArgumentIndieV2"] = GetInstanceSetting("VersionArgumentIndieV2", 0),
            ["VersionArgumentTitle"] = GetInstanceSetting("VersionArgumentTitle", ""),
            ["VersionRamType"] = GetInstanceSetting("VersionRamType", 0),
            ["VersionRamCustom"] = GetInstanceSetting("VersionRamCustom", 4096)
        };
        InstanceLaunchConfigService.Export(path, values, InstanceName);
        NotificationService.Info("启动配置已导出：" + path);
    }

    private void ImportLaunchConfig() {
        var files = MeloongCore.Wpf.Dialogs.SelectFile("导入启动配置", false, AppServices.Paths.Pcl,
            "JSON 配置|*.json");
        if (files.Count == 0) return;
        try {
            var values = InstanceLaunchConfigService.Import(files[0]);
            foreach (var pair in values) {
                SetInstanceSetting(pair.Key, pair.Value.ToString());
            }
            NotificationService.Info("启动配置已导入。");
        } catch (Exception ex) {
            NotificationService.Error("导入启动配置失败：" + ex.Message);
        }
    }

    private void ShowLaunchTiming() {
        var records = LaunchTimingService.List(Path.Combine(AppServices.Paths.Pcl, "LaunchTiming.json"));
        System.Windows.MessageBox.Show(LaunchTimingService.Format(records), "启动耗时", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ScanResources() {
        var result = ResourcePackService.Scan(InstancePath);
        System.Windows.MessageBox.Show(ResourcePackService.Format(result), "资源包/光影", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ToggleResource() {
        var result = ResourcePackService.Scan(InstancePath);
        if (result.Packs.Count == 0) {
            NotificationService.Warning("未找到资源包或光影。");
            return;
        }
        var selected = ChooseItem(result.Packs.Select(pack => $"[{(pack.IsShader ? "光影" : "资源包")}] {pack.Name}").ToList(), "选择要启用/禁用的项目");
        if (selected is null) return;
        var index = result.Packs.Select(pack => $"[{(pack.IsShader ? "光影" : "资源包")}] {pack.Name}").ToList().IndexOf(selected);
        try {
            ResourcePackService.Toggle(result.Packs[index].Path);
            NotificationService.Info("已切换状态。");
        } catch (Exception ex) {
            NotificationService.Error("切换资源包失败：" + ex.Message);
        }
    }

    private void BackupResources() {
        var path = MeloongCore.Wpf.Dialogs.SaveFile("备份资源包/光影", InstanceName + "-resources.zip",
            AppServices.Paths.Pcl, [("zip", "ZIP 压缩包")]);
        if (string.IsNullOrWhiteSpace(path)) return;
        try {
            AdvancedMaintenanceService.BackupResourcePacks(InstancePath, path);
            NotificationService.Info("资源包/光影备份已创建：" + path);
        } catch (Exception ex) {
            NotificationService.Error("备份失败：" + ex.Message);
        }
    }

    private void RestoreResources() {
        var files = MeloongCore.Wpf.Dialogs.SelectFile("恢复资源包/光影", false, AppServices.Paths.Pcl,
            "ZIP 备份|*.zip");
        if (files.Count == 0) return;
        try {
            AdvancedMaintenanceService.RestoreResourcePacks(files[0], InstancePath);
            NotificationService.Info("资源包/光影已恢复。");
        } catch (Exception ex) {
            NotificationService.Error("恢复失败：" + ex.Message);
        }
    }

    private void AutoFixMods() {
        var messages = AdvancedMaintenanceService.AutoFixMods(InstancePath);
        System.Windows.MessageBox.Show(string.Join("\r\n", messages), "自动修复 Mod", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task SpeedTestAsync() {
        try {
            var tester = new DownloadSourceSpeedTester();
            var results = await tester.TestAllAsync(DownloadSourceSpeedTester.DefaultTargets());
            AdvancedDiagnosticsService.RecordSourceSpeed(Path.Combine(AppServices.Paths.Pcl, "SourceSpeed.json"), results);
            var best = DownloadSourceSelector.PickBest(results);
            if (best is not null) {
                AppServices.Settings.Set(SourcePreferenceKey, best.Name);
                Logger.Info($"已选择最优下载源：{best.Name}（{best.ElapsedMilliseconds} ms）");
            }
            var message = DownloadSourceSpeedTester.Format(results)
                          + (best is null ? "\r\n\r\n没有可用源。" : $"\r\n\r\n已自动选择：{best.Name}（{best.ElapsedMilliseconds} ms）");
            System.Windows.MessageBox.Show(message, "下载源测速", MessageBoxButton.OK, MessageBoxImage.Information);
        } catch (Exception ex) {
            NotificationService.Error("测速失败：" + ex.Message);
        }
    }

    private void ShowSourceHistory() {
        var records = AdvancedDiagnosticsService.LoadSourceHistory(Path.Combine(AppServices.Paths.Pcl, "SourceSpeed.json"));
        System.Windows.MessageBox.Show(AdvancedDiagnosticsService.FormatSourceHistory(records), "下载源历史",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowJvmPreset(int index) {
        var presets = AdvancedMaintenanceService.JvmPresets();
        if (index < 0 || index >= presets.Count) return;
        var preset = presets[index];
        var text = string.Join("\r\n", preset.Settings.Select(pair => $"{pair.Key} = {pair.Value}"));
        System.Windows.MessageBox.Show($"JVM {preset.Name} 预设：\r\n{text}\r\n\r\n可在版本设置中手动应用这些参数。",
            "JVM 预设", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MonitorProcess() {
        var processes = Process.GetProcessesByName("java").Concat(Process.GetProcessesByName("javaw")).ToList();
        if (processes.Count == 0) {
            NotificationService.Warning("未找到正在运行的 Java 游戏进程。");
            return;
        }
        var selected = ChooseItem(processes.Select(process => $"{process.ProcessName} ({process.Id})").ToList(), "选择要监控的进程");
        if (selected is null) return;
        var process = processes[processes.Select(item => $"{item.ProcessName} ({item.Id})").ToList().IndexOf(selected)];
        var sample = AdvancedMaintenanceService.SampleProcess(process);
        if (sample is null) {
            NotificationService.Error("读取进程信息失败。");
            return;
        }
        System.Windows.MessageBox.Show(
            $"工作集：{FormatSize(sample.WorkingSet)}\r\nCPU：{sample.CpuPercent:F1}%\r\n线程：{sample.Threads}",
            "游戏资源监控", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void StartLiveLog() {
        if (!File.Exists(LogPath)) {
            NotificationService.Warning("日志文件不存在：" + LogPath);
            return;
        }
        _liveLogLength = new FileInfo(LogPath).Length;
        TxtLiveLog.Clear();
        _liveLogTimer.Start();
        BtnLiveLog.IsEnabled = false;
    }

    private void StopLiveLog() {
        _liveLogTimer.Stop();
        BtnLiveLog.IsEnabled = true;
    }

    private void ReadLiveLog() {
        try {
            if (!File.Exists(LogPath)) return;
            using var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length < _liveLogLength) _liveLogLength = 0;
            stream.Seek(_liveLogLength, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = reader.ReadToEnd();
            _liveLogLength = stream.Position;
            if (text.Length == 0) return;
            TxtLiveLog.AppendText(text);
            TxtLiveLog.ScrollToEnd();
        } catch {
            StopLiveLog();
        }
    }

    private void PackJava() {
        var files = MeloongCore.Wpf.Dialogs.SelectFile("选择 java.exe", false, AppServices.Paths.Java,
            "Java 可执行文件|*.exe");
        if (files.Count == 0) return;
        var output = MeloongCore.Wpf.Dialogs.SaveFile("保存 Java 包", "java.javapkg", AppServices.Paths.Java,
            [("javapkg", "PCL Java 包")]);
        if (string.IsNullOrWhiteSpace(output)) return;
        try {
            var info = JavaPackageService.CreatePackage(files[0], output);
            NotificationService.Info($"Java 包已创建：{info.Path}\r\n{info.JavaVersion} {info.TypeText}");
        } catch (Exception ex) {
            NotificationService.Error("打包 Java 失败：" + ex.Message);
        }
    }

    private void ImportJavaPack() {
        var files = MeloongCore.Wpf.Dialogs.SelectFile("选择 Java 包", false, AppServices.Paths.Java,
            "PCL Java 包|*.javapkg");
        if (files.Count == 0) return;
        var folders = MeloongCore.Wpf.Dialogs.SelectFolder("选择解压目标文件夹", false, AppServices.Paths.Java);
        if (folders.Count == 0) return;
        try {
            var info = JavaPackageService.Extract(files[0], folders[0]);
            NotificationService.Info($"Java 包已导入：{info.JavaHome}");
        } catch (Exception ex) {
            NotificationService.Error("导入 Java 包失败：" + ex.Message);
        }
    }

    private static object? GetInstanceSetting(string key, object defaultValue) {
        try {
            return AppServices.Settings.Get(key, InstancePath);
        } catch (KeyNotFoundException) {
            AppServices.Settings.Register(key, defaultValue, SettingSource.Instance);
            return AppServices.Settings.Get(key, InstancePath);
        }
    }

    private static void SetInstanceSetting(string key, object? value) {
        try {
            AppServices.Settings.Set(key, value, InstancePath);
        } catch (KeyNotFoundException) {
            AppServices.Settings.Register(key, value, SettingSource.Instance);
            AppServices.Settings.Set(key, value, InstancePath);
        }
    }
    private static string? ChooseItem(IReadOnlyList<string> items, string title) {
        if (items.Count == 0) return null;
        if (items.Count == 1) return items[0];
        var dialog = new Window {
            Title = title,
            Width = 420,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current?.MainWindow,
            Content = new System.Windows.Controls.ListBox { ItemsSource = items, Margin = new Thickness(12) }
        };
        if (dialog.Content is System.Windows.Controls.ListBox list) {
            list.MouseDoubleClick += (_, _) => {
                if (list.SelectedItem is string) dialog.DialogResult = true;
            };
            dialog.ShowDialog();
            return list.SelectedItem as string;
        }
        return null;
    }

    private static void OpenInstanceFolder(string name) {
        var path = Path.Combine(InstancePath, name);
        OpenFolder(path);
    }

    private static void OpenFolder(string path) {
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private static void OpenUrl(string url) {
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }


    private static string FormatSize(long bytes) {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) {
            value /= 1024;
            index++;
        }
        return $"{value:0.##} {units[index]}";
    }

}
