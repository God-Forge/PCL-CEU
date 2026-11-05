# C# / .NET 8 迁移状态

- 迁移分支：`csharp/net8`
- VB 冻结快照：`legacy/vb-net48`
- C# 迁移版本：`0.24.0-ceu` / `0.24.0.0`
- 旧 VB 稳定版本：`0.9.2-ceu`
- 最后扫描：2026-11-05
- 当前状态：阶段 5 的页面加载与主界面导航验收已完成；VB 功能对等仍有未完成项，见下文“仍未完成 / 仅部分迁移”。

## 当前扫描结果

| 项目 | 当前结果 |
| --- | --- |
| 解决方案 | `PCL.App`、`PCL.Core`、`PCL.Core.Net`、`PCL.Core.Infrastructure`、`PCLCS`、`MeloongCore`、`MeloongCore.Wpf`、`PCL.Core.Tests` |
| PCL.Core 源文件 | 164 个 |
| 页面代码 | 39 个 `Page*` 页面 + 4 个列表/登录项控件 |
| Release 构建 | 成功，0 warning，0 error |
| 自动化测试 | 654/654 通过 |
| 页面加载冒烟 | 39/39 通过 |
| 发布产物 | `PCL.App/bin/Release/net8.0-windows/publish/win-x64/PCL.exe` |
| 发布大小 | 225,564,063 bytes（约 215 MiB，含内嵌 MCA Selector/Payload 工具） |

## 已迁移

### 基础设施

- `PCL.CSharp.sln`、`PCL.App` WPF 壳与自包含发布配置。
- 日志与异常基础设施：`FileLogger`、AppDomain/UI/Task 未处理异常记录，日志写入 `PCL/Logs/PCL-CSharp.log`。
- Settings：`IniFile`、普通/注册表/实例级设置、类型转换、缓存、加密、重置与变更事件。
- 路径布局：`PclPathService` 统一程序目录、PCL 数据、缓存、实例、库、资源与日志路径。
- HTTP/下载：HTTP 客户端工厂、文件校验、下载进度、并行下载、缓存、源选择与测速。
- UI 基础：本地化、通知/提示队列、剪贴板、单实例、托盘、主题/DPI、窗口尺寸、导航历史、拖拽导入。
- 主题与控件兼容层：完整 PCL 色表、Button/TextBox/ComboBox/ListItem/MyCard/MyHint/MyLoading 模板、25 个旧 XAML 控件兼容实现。

### 启动与进程

- Java 扫描、Java 版本要求解析、Java 选择模式、Java 参数拆分。
- 启动计划：继承版本合并、classpath 构建、客户端 JAR、libraries、Natives 解压、旧版 `minecraftArguments`、游戏/JVM 参数、内存参数、全屏参数、服务器参数。
- 启动日志：`LaunchLogService` 实时转发 Java stdout/stderr、退出码。
- 启动状态机、启动耗时记录、游戏进程监视、窗口标题识别/修改、启动器可见性（隐藏/最小化/关闭/恢复）。
- 启动参数：窗口尺寸、窗口标题、自定义 JVM 参数、实例自定义游戏参数。

### 安装与下载

- 原版安装与基础文件修复：`VanillaInstallService`、`VanillaInstallPlanner`。
- Forge/NeoForge 安装编排：安装器下载、安装计划、注入、版本 JSON 生成。
- Fabric 安装。
- 版本清单、客户端列表、Forge/NeoForge/Fabric/LiteLoader/OptiFine 版本列表与解析。
- 下载源解析/选择/测速、下载队列、缓存、断点信息与并行下载。

### 资源市场

- Modrinth 与 CurseForge 聚合搜索：关键词、类型、游戏版本、分类、来源筛选、分页、去重与排序。
- 资源项目详情模型、版本选择、下载计划、单文件安装。
- 资源安装路径：Mods、资源包、光影、数据包。
- 资源依赖解析与批量下载核心服务已存在；当前下载页的“安装”按钮只安装单个项目，依赖/批量流程尚未接入 UI。
- CurseForge 需要在环境变量中配置 API Key，否则只使用 Modrinth。

### 实例、Mod 与世界

- 实例扫描/校验、实例目录布局、多 Minecraft 文件夹。
- 实例 `PCL/Setup.ini`：收藏、描述、分类、Logo 元数据、快捷目录。
- 实例重命名（同步 JSON/Jar/Natives/设置路径）、删除、克隆、迁移、快照与恢复。
- 实例体检、基础文件修复、启动配置导出/导入、离线整合包导出。
- Mod 扫描、搜索、启用/禁用、删除、从文件安装、批量操作、更新检查（只检查，不下载更新）。
- 世界列表、世界/存档备份与恢复、资源包/光影扫描/启用禁用/备份恢复。
- 崩溃历史、崩溃报告对比、最新日志、环境诊断、诊断包导出、诊断摘要复制。

### 登录与皮肤

- 离线登录、Authlib/Nide 账号密码登录、Microsoft 设备码登录。
- 当前登录会话持久化到 `LoginCacheJson`，启动时恢复。
- 皮肤预览：读取账号皮肤/头像、`SkinPreviewWindow`、`MySkin` 控件。
- 登录状态统一由 `LoginSessionService` 写入并通知页面。

### 工具与百宝箱

- 今日人品、内存优化、提权 helper 白名单。
- MCA Selector 2.9：内嵌资源，首次使用释放到 `PCL/McaSelector`。
- Payload：内嵌 `encode.exe` / `decode.exe`，支持打包、解包、导入为实例。
- 实例体检、Mod 检查、诊断包、崩溃分析、日志查看、世界备份、资源包备份、JVM 预设展示、实时日志、Java 包导入导出、下载源测速、进程资源采样、实例克隆/迁移/快照。
- 设置导入/导出、帮助 `Help.zip` 解析与搜索、关于页更新检查。

### UI 与导航

- 39 个 `Page*` 页面全部可加载，`--page-smoke` 39/39 通过。
- 主界面导航已切换到迁移页面：
  - 启动：`PageLaunchLeft` + `PageLaunchRight`
  - 下载：`PageDownloadLeft` + 安装/Mod/整合包/资源包/光影
  - 实例：概览/版本设置/Mod 管理/导出
  - 设置：启动/链接/界面/系统
  - 百宝箱：`PageOtherTest`
  - 帮助：`PageOtherHelp`
  - 关于：`PageOtherAbout`
- 背景音乐：WPF `MediaPlayer` 播放、播放/暂停、下一首、音量设置、播放列表随机/顺序。
- 拖拽导入：Mod 文件直接复制；整合包/资源包引导到实例页。
- 设置导入/导出、页面淡入动画、提示队列、导航历史。

## 仍未完成 / 仅部分迁移

### 1. 联机 / EasyTier

- `PageLinkMain` 仍是安全占位：创建/加入房间、邀请码、NAT/节点发现、端口转发、EasyTier 核心与联机模块 CLI 都未迁移。
- 页面会明确提示，不会伪造联机成功。

### 2. 完整整合包安装与导出

- 核心已有 `ModpackInstaller`、`ModpackInstallService`、`ModpackInstallPlanner`、`ModpackImportService`、`ModpackExportOptions`。
- UI 尚未接通：
  - `PageDownloadPack` 只做搜索，没有整合包下载/安装按钮。
  - `PageInstanceExport` 只调用离线整合包导出，未读取 XAML 中的导出选项（Mod、资源包、光影、存档、Java、PCL 配置、高级打包等）。
  - `PageDownloadModify` 没有对应的 C# 页面。
- 因此 CurseForge/Modrinth/MCBBS/HMCL/MMC/LauncherPack 的完整安装、依赖下载、覆盖文件处理仍未达到 VB 对等。

### 3. LiteLoader / OptiFine / Fabric API

- 核心已有 `LiteLoaderInstaller`、`OptiFineInstaller`，下载页也能列出可用版本。
- `PageDownloadInstall` 在真正安装时仍对 LiteLoader/OptiFine 抛出 `NotSupportedException`，需要手动安装或选择其他加载器。
- Fabric API、OptiFabric 的自动安装尚未接入。

### 4. Mod 更新

- `PageInstanceMod` 已能调用 `ModUpdateChecker` 检查 Modrinth 更新。
- 只能显示“可更新数量”，没有下载/替换/回滚 UI，也没有依赖图与版本兼容性修复。

### 5. 皮肤上传与管理

- 皮肤页目前只展示当前账号皮肤预览。
- 缺少本地上传、皮肤切换、披风、皮肤库、保存/恢复等 VB 功能。
- 微软登录需要手动设置 `PCL_MS_CLIENT_ID` 环境变量。

### 6. 多账号与 Token 刷新

- 只持久化“当前登录账号”一个 `LoginProfile`。
- 没有账号列表、账号切换、Refresh Token 持久化、自动刷新、失效重登。
- `MicrosoftLoginService.RefreshAsync` 已存在，但 UI/自动刷新流程未接入。

### 7. 背景图片与自定义主页

- `PageSetupUI` 能扫描/清理背景图片与音乐目录，并显示数量。
- 没有实际背景渲染、透明度/模糊/适配方式应用。
- 自定义主页只生成 `PCL/Custom.xaml` 教学文件，没有 XAML 加载/渲染宿主。
- 音乐已能播放，但游戏启动/退出自动暂停/恢复、音量即时联动仍未接入。

### 8. 启动前后命令

- `PageSetupLaunch` 有“启动后执行命令”等设置项与显隐逻辑。
- `LaunchService` 尚未实际执行启动前/后命令，也没有等待/超时/失败提示。

### 9. 自动更新、公告与遥测

- 已实现 GitHub 更新检查与打开网页。
- 缺少自动下载/替换更新包、启动器公告拉取/展示、遥测上报、自动反馈收集。
- 设置项 `SystemSystemTelemetry` 已注册，但没有发送逻辑。

### 10. CLI 命令缺口

当前 C# CLI 已支持 `--version`、`--list`、`--mods`、`--health`、`--diagnostics`、`--luck`、`--memory`、`--speedtest`、`--snapshot`、`--backup`、`--page-smoke`、`--install`（仅原版）、`--launch`、`--login`、`--modpack`（overrides）、`--update-check`、`--elevated`。

仍缺少 VB 版的：

- `--download <版本> [-forge|-neoforge|-fabric]`
- `--repair <实例>`
- `--mca`
- `--payload-encode` / `--payload-decode` / `--payload-import`
- `--javapkg-encode` / `--javapkg-info` / `--javapkg-decode`

### 11. 页面级占位/简化

- `PageDownloadCompDetail`：无宿主项目数据时显示迁移占位；列表页目前直接打开网页，不进入详情页。
- `PageDownloadModify`：C# 没有对应页面。
- `PageOtherTest`：
  - 世界管理目前是列表/备份/恢复，没有完整的重命名/删除/优化流程。
  - 进程监控是单次采样，不是持续监控。
  - JVM 预设只展示参数，没有一键写入实例设置。
- `PageSetupSystem` 的隐藏/调试入口（“土豆码”等）仍未迁移。
- 隐藏主题/彩蛋、部分 `ModSecret` 行为未迁移。

### 12. 图片与 UI 高级能力

- `ImageLoader` 遇到 WebP 会返回 `null`，暂不支持 WebP 解码。
- 仍缺少或仅简化的能力：阴影、窗口缩放、列表虚拟化、懒加载、剪贴板拦截、文件关联。
- 部分旧 XAML 的视觉效果仍依赖兼容控件，未逐页做像素级对齐。

## 构建、测试与发布

```powershell
& "C:\Users\neolx\.dotnet10\dotnet.exe" build "PCL.CSharp.sln" -c Release
```

测试：

```powershell
$env:DOTNET_ROLL_FORWARD = "LatestMajor"
& "C:\Users\neolx\.dotnet10\dotnet.exe" run --project "PCL.Core.Tests\PCL.Core.Tests.csproj" -c Release
```

页面加载验收：

```powershell
& "PCL.App\bin\Release\net8.0-windows\PCL.exe" --page-smoke
```

自包含发布：

```powershell
& "C:\Users\neolx\.dotnet10\dotnet.exe" publish "PCL.App\PCL.App.csproj" -c Release -p:PublishProfile=win-x64
```

> 当前开发机只安装了 .NET 10 x64 运行时，net8 测试需要 `DOTNET_ROLL_FORWARD=LatestMajor` 临时运行。
> 正式发布使用自包含产物，不依赖用户安装 .NET 8。

## 约束

- `main` 继续保留 VB 稳定版；C# 迁移在 `csharp/net8` 分支进行。
- 不修改现有用户数据格式与设置键。
- 新增功能需要同时补充核心测试与页面冒烟验证。
- 迁移优先保证可构建、可测试、可启动；未完成功能不得伪造成功结果。
