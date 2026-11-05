# VB → C# 迁移状态

本文件记录 `Plain Craft Launcher 2`（VB.NET，冻结的参考实现）到 C#（`PCL.App` / `PCL.Core`）的迁移进度。

## 判定口径

**不要按行数判断缺口。** VB 的页面文件里大量是 XAML 生成的控件声明与样式代码，C# 侧往往用更紧凑的方式实现同等功能。判断依据应当是「功能点是否可用」，而不是「文件大小是否接近」。

在核对过程中，曾三次仅凭行数就误判为缺失，核实时发现功能其实早已实现。

## 模块对照

| VB 模块 | C# 对应 | 状态 |
|---|---|---|
| ModMinecraft | `MinecraftVersionScanner`、`InstanceCheckService`、`MinecraftLogRules` | 已迁移 |
| ModNet | `HttpDownloadService`、`NetFileLoader`、`DownloadSourceSelector`、`DownloadCacheService` | 已迁移（运行时动态限速未做，C# 为请求级静态限速） |
| ModLaunch | `LaunchPlanService`、`LaunchService`、`LaunchParameterService`、`LaunchStateMachine` | 已迁移 |
| ModBase | `IoUtils`、`PclFileService`、校验与路径工具 | 已迁移 |
| ModCrash | `CrashPatternAnalyzer`、`CrashFixSuggestionService`、`CrashAnalysisService`、`CrashHistoryService` | 已迁移 |
| ModMain | 各 Service（提示、对话框、主题、导航等） | 已迁移 |
| ModAnimation | `PclAnimationService`（WPF Storyboard 版） | 已迁移 |
| ModDownload | `ResourceInstaller`、`ResourceBatchDownloader`、`ResourceDependencyResolver` | 已迁移 |
| ModModpack | `ModpackInstaller`、`ModpackInstallPlanner`、`ModpackExportOptions` | 已迁移 |
| ModLoader | `ForgelikeInstallService`、`OptiFineInstaller`、`LiteLoaderInstaller`、`JarInstallerRunner` | 已迁移 |
| ResourceProject / Searcher / Version | `ResourceModels`、`ModrinthResourceService`、`CurseForgeResourceService`、`ResourceVersionSelector` | 已迁移 |
| ModJava | `JavaScanner`、`JavaPackageService`、`JavaSelectionModeResolver` | 已迁移 |
| ModCommandLine | `CliCommandService` | 已迁移（命令覆盖不完全一致） |
| ModEvent | `CustomEventParser`、`CustomEventService`、`NavigationService` | 已迁移（高危事件类型出于安全考虑未开放） |
| ModWatcher | `GameProcessWatcher` | 已迁移 |
| ModMusic | `MusicPlayerService`、`MusicPlaylist`、`MusicDisplayFormatter` | 已迁移 |
| LocalResourceLoaders / File | `LocalResourceIndex`、`LocalResourceService` | 已迁移 |
| ModSecret | `ThemePalette`（含渐变计算） | 已迁移（其余在 VB 中为空壳或已删除） |
| MyBitmap | `ImageLoader`、`ImageGeometry` | 已迁移 |
| DragHelper | `DropImportClassifier` + WPF 原生 DragDrop | 已迁移 |
| ModPayload | `PayloadService` | 已迁移 |
| ModInstanceRepair | `InstanceCheckService`、`InstanceReportFormatter`、`VanillaInstallService.RepairAsync` | 已迁移 |
| ModElevatedHelper | `ElevatedTaskService` | 已迁移 |
| ModJavaPackage | `JavaPackageService` | 已迁移 |
| PclLogger | `FileLogger` | 已迁移 |
| ModWorldBackup | `WorldBackupService` | 已迁移 |
| ModValidate | 校验工具 | 已迁移 |
| LoaderProgressAdapter | `IProgress<T>` 体系 | 不需要迁移（框架机制不同） |
| ModDevelop | — | 不需要迁移（VB 中为空的 `Start()`） |

## 页面层

C# 页面与 VB 页面并非一一对应，部分 VB 页面逻辑被拆进了 Core 服务。例如 `PageInstanceExport.vb`（784 行）的打包逻辑位于 `AdvancedMaintenanceService.ExportOfflinePack`，C# 页面只剩调用与选项。

已实现的页面功能包括：启动、下载、实例管理、设置、百宝箱、登录、皮肤预览等。设置页的背景图、Logo、音乐目录功能在 `PageSetupUI.xaml.cs` 中已完整实现。

## 仍未对齐的细节

1. **下载运行时动态限速**：C# 支持请求级限速，但不支持下载过程中调整。
2. **CLI 命令集合**：`CliCommandService` 的命令与 VB `ModCommandLine` 不完全一致。
3. **VB 的 `PageDownloadModify`（安装目标编辑器）**：C# 有 `InstallTarget` / `InstallTargetComparer` 模型，但还没有对应的编辑界面。
4. **皮肤渲染**：C# 使用外部渲染服务（crafatar），离线时降级为「加载失败」。

## 验证方式

- 全量构建：`dotnet build PCL.CSharp.sln -c Release`
- 全量测试：`PCL.Core.Tests.exe`（当前 637 个用例）
- 无 VB 依赖：临时移走 `Plain Craft Launcher 2` 目录后构建仍然成功（资源已移入 `PCL.Core/Assets`）
