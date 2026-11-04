# C# / .NET 8 迁移状态

- 迁移分支：`csharp/net8`
- VB 冻结快照：`legacy/vb-net48`
- 当前阶段：阶段 5（UI 页面细化与对等验收）已完成
- C# 迁移版本：`0.24.0`
- 旧 VB 稳定版本：`0.9.2-ceu`

## 已完成

- 创建 `PCL.CSharp.sln`。
- 新建 `PCL.App`（`net8.0-windows` WPF 应用壳）。
- `PCL.Core`、`PCLCS`、`MeloongCore.Wpf` 切换到 `net8.0-windows`。
- `PCL.Core.Tests` 切换到 `net8.0-windows`，现有 **275** 个测试通过。
- `PCL.App` 支持 `--version`、`--help`，GUI 启动冒烟通过。
- 自包含发布配置：`PCL.App/Properties/PublishProfiles/win-x64.pubxml`。
- 已生成并验证自包含单文件产物 `PCL.exe`。
- 已完成日志与异常基础设施：`FileLogger`、AppDomain/UI/Task 未处理异常记录，日志写入 `PCL/Logs/PCL-CSharp.log`。
- 已完成 Settings 基础设施：`IniFile`、普通/注册表/实例级设置、类型转换、缓存、加密、重置与变更事件。
- 已完成 PCL 路径布局：`PclPathService`，统一程序目录、PCL 数据、缓存、实例、库、资源与日志路径，并接入启动流程。
- 已完成阶段 2 其余基础设施：HTTP 客户端工厂、文件服务、本地化、UI 调度、通知/剪贴板、单实例、托盘、主题/DPI 与 Win32 P/Invoke。
- 已完成阶段 3 核心领域：实例扫描/校验、Java 包、离线/微软/Authlib-Nide 登录、下载/安装、Forge/NeoForge 安装编排、启动计划、Mod/资源/世界、崩溃/诊断、CLI、整合包 overrides、更新检查与提权 helper 白名单。
- 已完成阶段 4 C# UI 基础：导航壳、启动/下载/实例/百宝箱/设置/关于页面，接入实例扫描、原版安装、体检、Mod 扫描、每日人品、内存优化、测速与语言切换。
- PCL.Core 已拆分为 `PCL.Core`（领域）、`PCL.Core.Net`（网络/下载/更新）、`PCL.Core.Infrastructure`（设置/路径/IO/本地化/任务/原生互操作）。
- 已建立 25 个旧 XAML 自定义控件的 C# 兼容层（MyCard/MyButton/MyListItem/MyPageRight/MyScrollViewer 等）及 `SettingService`、`CustomEventService` 附加属性。
- 已完成全部 43 个页面 XAML 的结构迁移：统一命名空间、C# code-behind、控件兼容层与页面目录。
- 已迁移通用事件转发、页面淡入动画与主题资源字典；按钮的 CustomEventService 可处理打开网页/文件夹/复制/提示。
- 已接入动态数据基础：Settings 自动绑定保存、实例 Mod 列表、原版版本下载列表、帮助详情 XAML 加载、离线登录名缓存。
- 已迁移页面样式：完整 PCL 色表、Button/TextBox/ComboBox/ListItem/MyCard/MyHint/MyLoading 模板、卡片阴影与悬停效果、侧栏导航配色。
- 已迁移控件行为：MyCard 折叠、MyListItem 选中/勾选、MyHint 主题、MyScrollViewer 平滑滚动、MyImage 回退图、按钮事件转发与状态配色。
- 已接入登录页事件：离线登录、Authlib/Nide 账号密码登录、Microsoft 设备码登录与当前登录会话。
- 已接入 Modrinth 搜索列表：Mod/资源包/光影/数据包/整合包下载页支持搜索、分页与打开项目页。
- 实例目录改为 `.minecraft` 布局（versions/libraries/assets），兼容 VB 实例格式；新增 `MinecraftFolderService`，支持添加多个 Minecraft 文件夹并在首页/实例页统一扫描。
- 新增 Java 扫描与启动服务：自动查找 Java、构建启动计划并启动游戏进程；首页新增启动按钮。
- 修复启动链路：classpath 加入客户端 JAR、继承版本 JSON 合并、Natives 解压、旧版 minecraftArguments、Java 版本按实例要求选择，并增加进程退出提示。
- 新增 `InstanceManagementService`：读写实例 `PCL/Setup.ini`、收藏/描述/分类、实例目录快捷路径、版本重命名（同步 JSON、Jar、Natives 与设置中的路径）与删除。
- 实例页改为“概览 / 版本设置 / Mod 管理 / 导出”四页导航；概览页接入版本信息、重命名、描述、收藏、文件夹、体检、启动脚本导出与删除。
- 导出页接入离线整合包导出（实例文件、libraries、assets 索引与资源）。
- 新增可复用 `InputDialog`，用于版本重命名与描述输入。
- 阶段 5 页面验收：39 个 Page* 迁移页面全部通过 --page-smoke 加载（39/39）。
- 主界面导航已切换到迁移页面：启动（PageLaunchLeft/Right）、下载（PageDownloadLeft + 安装/Mod/整合包/资源包/光影）、设置（PageSetupLeft + Launch/System/UI/Link）、百宝箱（PageOtherTest）、帮助（PageOtherHelp）、关于（PageOtherAbout）。
- 新增 LaunchLogService，启动进程 stdout/stderr、退出码实时转发到启动页日志。
- 下载页接入版本列表、加载器可用性、安装进度/日志与项目详情；实例页接入概览、版本设置、Mod 管理与导出；设置页接入窗口尺寸、内存、主题、语言与链接设置。
- 帮助页从 Help.zip 读取 JSON 与 XAML，支持搜索和详情；百宝箱页接入体检、诊断、克隆、快照、世界/资源备份、JVM 预设、实时日志、Java 包、MCA Selector 与 Payload。
- MCA Selector 2.9 与 Payload 编解码工具改为嵌入资源，首次使用时释放到 PCL/McaSelector 与 runtime。
- 联机页（PageLinkMain）保留安全占位：EasyTier/联机模块尚未迁移，页面会明确提示而不会伪造联机成功。

## 构建与测试

```powershell
& "C:\Users\neolx\.dotnet10\dotnet.exe" build "PCL.CSharp.sln" -c Release
$env:DOTNET_ROLL_FORWARD = "LatestMajor"
& "C:\Users\neolx\.dotnet10\dotnet.exe" run --project "PCL.Core.Tests\PCL.Core.Tests.csproj" -c Release
```

页面加载验收：

```powershell
& "PCL.App\bin\Release\net8.0-windows\PCL.exe" --page-smoke
```

> 当前开发机只安装了 .NET 10 x64 运行时，net8 测试需要 `DOTNET_ROLL_FORWARD=LatestMajor` 临时运行。
> 正式发布使用自包含产物，不依赖用户安装 .NET 8。

## 自包含发布

```powershell
& "C:\Users\neolx\.dotnet10\dotnet.exe" publish "PCL.App\PCL.App.csproj" -c Release -p:PublishProfile=win-x64
```

产物目录：`PCL.App/bin/Release/net8.0-windows/publish/win-x64/`

## 阶段 1 边界

本阶段只建立可构建、可测试、可启动的 C#/.NET 8 骨架，不迁移现有 VB 页面。
下一阶段开始迁移基础设施：日志、Settings、路径/文件、HTTP/下载、任务框架、通知与主题资源。

## 约束

- `main` 继续保留 VB 稳定版；本分支不推送，直到迁移完成。
- 不修改现有用户数据格式与设置键。
- 迁移期间冻结新功能。














































