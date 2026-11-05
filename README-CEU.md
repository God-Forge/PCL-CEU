# Plain Craft Launcher (PCL) CEU 修改版

本仓库是第三方基于 **Plain Craft Launcher (PCL)** 独立修改的版本，**并非 PCL 官方作品，与官方无关**。

> 本文档主要描述 VB.NET/WPF 稳定版 `0.9.2-ceu`。
> C#/.NET 8 迁移版当前为 `0.24.0-ceu`，其构建、测试、已迁移功能与仍待迁移项见 [MIGRATION-CS.md](MIGRATION-CS.md)。

## 署名与许可

- 原作者：**龙腾猫跃（LTCat）**
- 原作者赞助：<https://meloong.com/afd/a/LTCat>
- 上游项目：<https://github.com/Meloong-Git/PCL>
- 使用许可：见仓库中的 `LICENCE` 文件；本改版按《PCL 存储库合理使用指南》中的**重度使用**条款处理。
- 本改版源代码：本 Git 仓库。发布时请将本仓库公开，或在此处补充公开仓库地址。

## 版本

- 版本号：`0.9.2-ceu`
- 输出文件：`PCL-0.9.2ceu.exe`
- 程序集版本：`0.9.2.0`
- 版本规则：VB → C# 迁移完成前统一使用 `0.x.y`；tag 格式为 `x.y.z-ceu`，产物为 `PCL-x.y.zceu.exe`。

## 本改版的主要调整（与开源版相比）

- 移除 Minecraft 下载相关的限制逻辑。
- 调整版本号、程序集名称与输出文件名。
- 在“关于”页面补充第三方修改声明，并保留原作者署名、赞助链接与上游源代码入口。
- 固定蓝色主色相，并隐藏与赞助解锁相关的主题入口。
- 内置 MCA Selector 2.9 区块编辑器（含 Java 运行时），可在“更多 → 百宝箱”中启动。
- 在“百宝箱”中加入“今日人品”：以当地日期对应的 Unix 时间戳为种子，每天生成固定的 0~100 人品值。
- 在“百宝箱”中加入“内存优化”：对应 RAMMap 的 Empty 功能，支持清空工作集、系统工作集、已修改页列表、待机列表、优先级 0 待机列表等。
- 新增命令行模式，支持 `--help`、`--version`、`--luck`、`--mca`、`--memory` 等命令。
- 主界面以普通权限启动；系统级内存优化、显卡设置等操作通过内部 helper 按需请求管理员权限。
- 新增 `.payload` 版本包格式与 C++ `encode.exe` / `decode.exe`，用于打包或解包单个 Minecraft 版本。
- 命令行新增 `--launch <实例>` 与 `--download <MC版本> [-forge|-neoforge|-fabric]`。
- 新增世界/存档备份与恢复；启动 MCA Selector 前自动备份当前实例存档。
- 新增实例体检、基础文件修复与 Mod 元数据/重复/缺失依赖检查；增强磁盘空间、非 ASCII 路径与 Fabric/Forge 混装检查。
- 新增崩溃诊断包导出：收集日志、崩溃报告、Mod 列表与系统信息，并脱敏访问令牌和用户路径。
- 崩溃分析新增堆栈溢出、原生库、文件访问、命令行过长、区块损坏、光影/资源包与 Mod API 兼容检查，并提供对应修复建议。
- 新增实例克隆/迁移、实例快照与恢复。
- 新增资源包/光影扫描、启用/禁用与目录打开。
- 新增 Mojang/BMCLAPI/镜像下载源测速。
- 新增崩溃历史、常见退出码识别、最新日志查看与可粘贴诊断摘要。
- 新增实例启动配置导出/导入与启动阶段耗时记录。
- 新增高级维护工具：离线整合包、实例迁移、磁盘占用、存档管理、资源包备份/恢复、Mod 自动修复与依赖图。
- 新增实时日志、崩溃报告对比、环境诊断、JVM 预设、游戏进程资源监控与下载源测速历史/自动选择。
- Payload 支持直接导入为 `versions` 下的实例。
- 新增 `.javapkg` Java 打包格式：可把本机 JDK/JRE 打包为 `<版本>.jdk.javapkg` / `<版本>.jre.javapkg`，并在其他电脑解压导入。

## 命令行模式

以 `{文件名} {参数}` 方式启动时，PCL 会进入命令行模式，不再显示主窗口。命令参数使用 `--{英文单词}` 形式。

```powershell
.\PCL-0.9.2ceu.exe --help
.\PCL-0.9.2ceu.exe --version
.\PCL-0.9.2ceu.exe --luck
.\PCL-0.9.2ceu.exe --mca
.\PCL-0.9.2ceu.exe --payload-encode .\versions\1.20.1 .\1.20.1.payload
.\PCL-0.9.2ceu.exe --payload-decode .\1.20.1.payload .\decoded
.\PCL-0.9.2ceu.exe --payload-import .\1.20.1.payload
.\PCL-0.9.2ceu.exe --javapkg-encode .\jdk\bin\java.exe .\21.0.1.jdk.javapkg
.\PCL-0.9.2ceu.exe --javapkg-info .\21.0.1.jdk.javapkg
.\PCL-0.9.2ceu.exe --javapkg-decode .\21.0.1.jdk.javapkg .\java21
.\PCL-0.9.2ceu.exe --backup 1.20.1
.\PCL-0.9.2ceu.exe --mods 1.20.1
.\PCL-0.9.2ceu.exe --repair 1.20.1
.\PCL-0.9.2ceu.exe --launch 1.20.1
.\PCL-0.9.2ceu.exe --download 1.20.1 -forge
.\PCL-0.9.2ceu.exe --memory
```

| 命令 | 说明 |
|---|---|
| `--help`、`-h`、`/?` | 显示命令行帮助 |
| `--version`、`-v` | 显示版本、版本代码与程序集版本 |
| `--luck`、`--jrrp` | 输出今日人品值与评语 |
| `--mca`、`--mca-selector` | 启动内置 MCA Selector 区块编辑器 |
| `--payload-encode <文件夹> <输出.payload> [版本名]` | 将版本文件夹打包为 Payload |
| `--payload-decode <输入.payload> <输出文件夹>` | 解包 Payload |
| `--payload-import <输入.payload> [实例名]` | 导入 Payload 为实例 |
| `--javapkg-encode <java.exe> <输出.javapkg>` | 打包本机 JDK/JRE |
| `--javapkg-info <输入.javapkg>` | 查看 Java 包信息 |
| `--javapkg-decode <输入.javapkg> <输出文件夹>` | 解压 Java 包 |
| `--backup <实例>` | 备份当前实例存档 |
| `--mods <实例>` | 检查当前实例的 Mod |
| `--repair <实例>` | 修复实例基础文件 |
| `--launch <实例>` | 启动 `versions` 下指定文件夹名的实例 |
| `--download <MC版本> [-forge\|-neoforge\|-fabric]` | 下载原版并安装指定加载器 |
| `--memory` | 执行内存优化；退出码为释放的 KB（0 表示无明显变化） |
| `--update ...` | PCL 内部自动更新命令，供更新器调用（高级） |
| `--gpu <java.exe>` | PCL 内部显卡设置命令（高级） |

说明：

- `--memory` 会自动请求管理员权限，并通过内部 helper 执行完整内存优化；如果取消 UAC，则不会执行。
- `--mca` 首次运行会从内置资源解压 MCA Selector 与 Java 运行时，之后直接启动。
- `--luck` 的结果按当地日期固定，同一天多次运行结果一致。
- `--help`、`--version`、`--luck` 在独立控制台中运行时会等待按键后退出；从 `cmd`/PowerShell 等已有控制台运行时直接输出。
- `--launch <实例>`：实例名以 `versions` 下的文件夹名为准；使用当前 PCL 的账号、Java、内存与启动设置。主窗口会隐藏，PCL 进程会保留以维持启动/日志监听。
- `--download <MC版本> [-forge|-neoforge|-fabric]`：自动选择适用于该 MC 版本的加载器版本；原版实例名通常为 `1.20.1`，加载器实例名形如 `1.20.1-Forge_47.4.8`、`1.20.1-NeoForge_...`、`1.20.1-Fabric 0.16.9`。完成后进程退出。
- `--launch` / `--download` 使用现有 PCL 下载与启动流程，因此会读取当前下载源、Java、账号等设置；失败时会显示 PCL 窗口以便查看错误。
- `--backup <实例>` 备份到 `PCL\Backups\Worlds\<实例>\`，保留最近 10 份；`--mods <实例>` 只读取 Mod 元数据，不执行 JAR；`--repair <实例>` 会重新下载缺失的原版基础文件。
- `--javapkg-encode` 自动识别 JDK/JRE 与 Java 版本，默认文件名形如 `21.0.1.jdk.javapkg`；`--javapkg-decode` 只解压，不自动加入 Java 列表，界面“导入 Java 包”会解压并加入列表。
- `--payload-import` 会解包到临时目录，校验版本 JSON 后移动到 `versions`，不会覆盖已有实例。
- 百宝箱还提供“世界备份”“实例体检”卡片：可手动备份/恢复存档、检查实例、修复基础文件、检查 Mod；启动 MCA Selector 前会自动备份当前实例存档。
- 主界面、`--help`、`--version`、`--luck`、`--mca` 默认以普通权限运行，不会触发 UAC；只有 `--memory` 等需要系统权限的操作会按需提权。

## 第三方组件

- **MCA Selector 2.9**：© 2018-2026 Querz，以 MIT License 分发。
  - 项目地址：<https://github.com/Querz/mcaselector>
  - 许可证文本随内置压缩包分发（解压后的 `LICENSE.txt`）。

## Payload 版本包

`.payload` 是一种“带特殊头部的 zip”，用于打包单个 Minecraft 版本目录（版本 JSON、客户端 JAR、mods、config 等）。

格式：

- 固定 128 字节头部
- 随后是 UTF-8 元数据 JSON
- 随后是 ZIP 数据
- ZIP 使用存储模式（method 0），不压缩，便于 C++ 独立实现和跨工具读取
- 头部包含 ZIP 偏移、ZIP 大小、元数据大小、CRC32、版本名、创建时间等字段

C++ 工具：

- `tools/payload/encode.exe`：把版本文件夹打包为 `.payload`
- `tools/payload/decode.exe`：读取头部并解包
- 首次使用时由 PCL 释放到 `./runtime/encode.exe` 与 `./runtime/decode.exe`
- 支持中文路径；解包时会拒绝绝对路径、盘符和 `..` 路径穿越

命令行：

```powershell
.\PCL-0.9.2ceu.exe --payload-encode .\versions\1.20.1 .\1.20.1.payload "1.20.1"
.\PCL-0.9.2ceu.exe --payload-decode .\1.20.1.payload .\decoded
```

也可以在“更多 → 百宝箱 → Payload 版本包”中使用“打包当前版本”与“解包 Payload”。

## Java 打包（.javapkg）

`.javapkg` 是包含 `javapkg.json` 清单的 Java 主目录 ZIP。

- 选择 `java.exe` 后自动读取 `release` 信息，识别 Java 版本、JDK/JRE、架构与厂商。
- JDK 默认命名为 `<Java版本>.jdk.javapkg`，JRE 为 `<Java版本>.jre.javapkg`。
- 包内包含完整 Java 主目录（`bin`、`lib`、`conf`、`jmods` 等）。
- 解压时会校验 `bin/java.exe` 和路径安全。
- 百宝箱“导入 Java 包”会解压到 `PCL\Java\<版本>-<类型>` 并加入 Java 列表。
- 命令行 `--javapkg-decode` 只解压到指定文件夹，不自动修改 Java 列表。

## 世界备份与实例体检

- 百宝箱 → 世界备份：备份当前实例的 `saves`、恢复最近备份、打开备份目录。
- 备份位置：`PCL\Backups\Worlds\<实例>\`，默认保留最近 10 份。
- 启动 MCA Selector 前会自动备份当前实例存档；备份失败时会询问是否仍要启动。
- 百宝箱 → 实例体检：检查版本 JSON、客户端 JAR、libraries、assets 与 Mod。
- “修复基础文件”会通过现有下载流程重新获取缺失的原版基础文件。
- “检查 Mod”只读取 JAR 元数据，不执行 JAR；可发现重复 Mod ID、重复文件、缺失前置依赖等问题。
- Payload 可直接导入为 `versions` 下的实例，不会覆盖已有实例。

## 构建

需要：

- .NET Framework 4.8 目标包（或 NuGet 的 `Microsoft.NETFramework.ReferenceAssemblies.net48`）
- 支持 C# 13 的 .NET SDK（本仓库使用 .NET 10 SDK 验证）
- Visual Studio 的 MSBuild（用于编译 WPF/VB.NET 主工程）

构建主程序时请使用 VS MSBuild，而不是 `dotnet build`：

```powershell
$env:MSBuildSDKsPath = "C:\Users\neolx\.dotnet10\sdk\10.0.403\Sdks"
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  "Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj" `
  /restore /p:Configuration=Release `
  /p:TargetFrameworkRootPath="$env:USERPROFILE\.nuget\packages\microsoft.netframework.referenceassemblies.net48\1.0.3\build\" `
  /p:MSBuildEnableWorkloadResolver=false
```

`MeloongCore` 是 Git 子模块；首次构建前执行：

```powershell
git submodule update --init --recursive
```

## C# 重构进度

Phase 1 已开始：

- 新增 `PCL.Core` C# 类库（net48，C# 13），用于承载逐步从 VB.NET 迁移的业务逻辑。
- `ModValidate` 的校验规则已迁移到 `PCL.Core/Validation/Validation.cs`，VB 侧仅保留兼容入口。
- “今日人品”的计算已迁移到 `PCL.Core/Tools/DailyLuck.cs`。
- 内存优化核心已放在 `PCL.Core/Memory/MemoryOptimizer.cs`。
- 相关 XAML 已改用 `xmlns:core="clr-namespace:PCL;assembly=PCL.Core"`。
- 新增 `PCL.Core.Tests`，覆盖输入校验、今日人品与内存状态。

Phase 2 已开始：

- 整合包类型识别已迁移到 `PCL.Core/Modpack/ModpackInspector.cs`。
- `ModModpack.ModpackInstall` 已改为调用 C# 识别器，支持 CurseForge、HMCL、MMC、MCBBS、Modrinth、启动器整合包与普通压缩包。
- 新增测试覆盖根目录/一级目录识别、基础目录与未知压缩包。
- Java 版本要求规则已迁移到 `PCL.Core/Java/JavaRequirementResolver.cs`，`ModJava.GetJavaRequirement` 只负责构建上下文与记录日志。
- 新增测试覆盖原版版本、OptiFine、Forge、NeoForge、Fabric、LiteLoader、统一通行证、手动覆盖与 Mojang 推荐 Java。
- 下一步：ResourceVersion 数据层、ModNet/ModLoader 的接口与任务迁移。

Phase 3 已开始：

- 新增 `PCL.Core/Net/IStringFetcher` 与 `HttpStringFetcher`，作为可替换的字符串下载接口。
- 新增 `PCL.Core/Download/LoaderVersionService`，从 Forge/NeoForge/Fabric 元数据中选择兼容版本。
- 命令行 `--download` 的加载器版本选择已从 VB LoaderTask 迁移到 C# 服务，减少对 UI/Loader 框架的依赖。
- 新增测试覆盖 Forge 推荐/最高版本、NeoForge 稳定版与 Legacy、Fabric stable Loader。
- 新增 `PCL.Core/Net/IDownloadService`、`DownloadRequest`、`DownloadProgress`、`DownloadResult` 与 `HttpDownloadService`。
- `HttpDownloadService` 支持多源回退、断点续传、重试、超时、限速、SHA-256 校验和进度回调。
- `ModNet.NetDownloadByClient` 的单文件下载已迁移到 C# 下载服务；ModNet 的多线程下载引擎仍保留。
- 新增本地 HTTP 测试服务器，覆盖普通下载、多源回退、断点续传、哈希失败。
- 新增 `PCL.Core/Tasks`：`TaskState`、`ITaskProgress`、`TaskBase`、`ProgressTask`、`TaskCombo`，统一任务状态、进度、失败与取消。
- `HttpDownloadService` 增加使用 `ProgressTask` 的重载，下载进度可直接映射到统一任务状态。
- 新增任务测试覆盖完成、失败、取消、组合任务与下载进度联动。
- 新增 `LoaderProgressAdapter`，把 VB `LoaderBase` 的 `LoadState`、进度、错误与取消令牌适配为 C# `ITaskProgress`。
- `ModCommandLineDownload` 的安装流程已通过适配器接入统一任务接口。
- 新增 C# `LoaderTask<TIn,TOut>` 与 `LoaderCombo`：支持输入/输出、重载超时、强制重启、取消、进度与顺序组合。
- 新增测试覆盖 LoaderTask 输入输出、重载超时、强制重启、取消、失败与 LoaderCombo 顺序执行。
- 新增 `ITaskControl`，并让 `LoaderProgressAdapter` 同时实现控制接口；C# `LoaderCombo` 可直接组合 VB LoaderBase 适配器。
- `ModCommandLineDownload` 与 `ModInstanceRepair` 已切换到 C# `LoaderCombo`，其余 ModLoader 调用方仍保留 VB 实现。
- 新增 C# `ParallelDownloadService`，支持多分片、Range 探测、失败退化为单线程、进度汇总与哈希校验。
- `ModNet.NetDownloadByLoader` 已从 VB `LoaderDownload`/`NetFile` 切换为 C# 多分片下载；NetFile 的完整任务引擎仍未迁移。
- 新增 `PCL.Core/Download/VersionManifestService`，通过 C# 获取 Minecraft 版本 JSON（Mojang 优先，BMCLAPI 回退）。
- 命令行 `--download` 已改为使用 C# 版本 JSON 服务，不再依赖 `DlClientListLoader`。
- `ModDownload.DlClientListGet` 也已迁移到 `VersionManifestService`，不再使用 VB 版本列表 Loader。
- 新增 `PCL.Core.Minecraft.MinecraftVersionScanner`，只读扫描 `versions` 目录与版本 JSON。
- 新增 CLI `--versions`，直接列出 `versions` 下的版本信息。
- 新增 C# `RuleEvaluator`，支持 Minecraft 版本 JSON 的 os / features 规则。
- 新增 C# `LibraryResolver`，解析 artifact、classifiers、natives、Maven 根地址与规则过滤。
- `InstanceCheckService` 的 libraries 检查已改为使用 `LibraryResolver`。
- `ModMinecraft.McLibListGetWithJson` 的 artifact/natives/classifiers/rules 解析已改为调用 C# `LibraryResolver`，仅保留 VB 侧去重逻辑。
- 新增 C# `NetFileTask` / `NetLoader` 多文件任务引擎：并发、逐文件多源、断点续传、并行分片、进度聚合、取消与失败隔离。
- `NetDownloadByLoader` 已改用 C# `NetLoader` 引擎。
- 新增 C# `VanillaInstallPlanner`，从版本 JSON 与资源索引生成客户端 JAR、libraries、asset index、assets 下载任务。
- 新增 C# `VanillaInstallService`，完成原版版本 JSON、资源索引、文件下载与版本 JSON 写入。
- CLI `--download <版本>` 的原版分支已切换到 C# `VanillaInstallService`。
- 新增 C# `FabricInstallService`，完成 Fabric profile 获取、依赖下载与版本 JSON 写入；CLI `--download <版本> -fabric` 已切换到该服务。
- 新增 C# `ForgelikeInstallPlanner`，接管 Forge/NeoForge 安装器的 `install_profile.json` / `version.json` 合并、Mappings 补充与主库剔除。
- 新增 C# `NativesExtractor`，接管启动前的 DLL 解压、大小相同跳过、损坏 jar 删除与多余文件清理。
- 新增 C# `JavaArgumentParser`，接管启动参数拆分、转义引号处理与 JVM/游戏参数去重。
- 新增 C# `ForgelikeInjectorRunner`，接管 Forge/NeoForge 安装器进程、输出进度解析、取消与成功判定。
- 新增 C# `PclRuleEvaluator` 与 `LibraryResolver.GetLocalPath`，接管 ModMinecraft 的规则判断与 Maven 本地路径生成。
- 新增 C# `ForgeVersionParser`，接管 BMCLAPI Forge 版本列表的安装类型、Hash、推荐版本与发布时间解析。
- `ModNet.NetRequestByLoader` 已改为直接使用 C# `NetLoader`，移除 VB `LoaderDownload` 调用。
- 新增 C# `MultiRequestCoordinator`，接管 ModNet 多请求错峰竞速，替代手写线程与 Interrupt。
- 新增 C# `ClasspathBuilder`，接管启动 classpath 拼接与 OptiFine 插入规则。
- 新增 C# `LaunchArgumentFormatter`，接管启动参数替换、单个参数格式化与特殊字符引号处理。
- 新增 C# `GameArgumentCollector`，接管版本 JSON `arguments.game` 的规则过滤与参数收集。
- 新增 C# `LegacyGameArgumentBuilder`，接管旧版 `minecraftArguments` 的分辨率参数追加。
- 新增 C# `FullscreenArgumentBuilder`，接管全屏游戏参数生成。
- 移除已无调用方的 VB `DlSourceLoader` 源切换逻辑，统一由 C# 服务处理源顺序与回退。
- 移除已无调用方的 VB Forge 版本源加载方法，Forge 版本列表统一由 C# `ForgeVersionService` 获取。
- 移除已无调用方的 VB OptiFine 官方源加载器，OptiFine 列表统一由 C# `OptiFineListService` 获取。
- 移除已无调用方的 VB NeoForge/LiteLoader/Fabric 源加载器，版本列表统一由 C# 服务获取。
- OptiFine BMCLAPI 加载器已委托给 C# `OptiFineListService`。
- Minecraft 客户端列表的 Mojang/BMCLAPI 分加载器已委托给 C# `ClientListService`。
- 新增 C# `ClientJarInfoResolver`，接管版本 JSON `downloads.client` 的 URL、SHA1 与大小解析。
- 新增 C# `AssetIndexInfoResolver`，接管资源索引的 id 与 url 读取。
- 新增 C# `NativesFolderResolver`，接管 Natives 目录的 ASCII 回退与提前创建。
- 新增 C# `DownloadSpeedTracker`，接管 ModNet 加权下载速度与速度下限计算。
- 新增 C# `DownloadProgressCalculator`，接管下载任务加权总进度计算。
- 新增 C# `DownloadFileValidator`，接管下载 URL 与本地文件路径校验。
- 增强崩溃分析：新增 C# `CrashPatternAnalyzer`，识别堆栈溢出、原生库/文件访问失败、命令行过长、区块损坏、光影/资源包错误与 Mod API 不兼容。
- 新增 C# `JvmArgumentCollector`，接管版本 JSON `arguments.jvm` 的规则过滤与参数收集。
- 新增 C# `AssetIndexResolver`，接管 assets objects 的普通、virtual 与 map_to_resources 路径解析，以及 assetIndex/assets 名称回退和索引对象解析。
- 新增 C# `DownloadSourceResolver`，接管 Mojang、Assets、Libraries 与 Modrinth/CurseForge 下载镜像地址生成，以及 Mod 请求源顺序。
- 新增 C# `ServerArgumentBuilder`，接管自动进入服务器的 QuickPlay 与旧版 host/port 参数。
- 新增 C# `LaunchTokenBuilder`，接管启动替换标记、窗口尺寸与空 version_type 处理。
- 新增 C# `NeoForgeVersionParser`，接管 NeoForge Maven 版本名提取与不可下载版本过滤。
- 新增 C# `LiteLoaderVersionParser`，接管 LiteLoader versions.json 的版本、文件名、预览状态与 MD5 解析。
- 新增 C# `OptiFineVersionParser`，接管 OptiFine 官方 HTML 与 BMCLAPI 版本列表、Forge 兼容与实例名生成。
- 新增 C# `ForgeListParser`，接管 Forge Minecraft 版本列表的官方 HTML 与 BMCLAPI 解析。
- `ForgeVersionParser` 新增官方 HTML 解析，Forge 版本列表的官方源与 BMCLAPI 源均已迁移。
- 新增 C# `ForgeVersionService`，接管 Forge 版本列表的源顺序、超时与回退；`DlForgeVersionMain` 不再创建本地 VB LoaderTask。
- 新增 C# `NeoForgeListService`，接管 NeoForge 版本列表的源顺序、超时与回退。
- 新增 C# `LiteLoaderListService`，接管 LiteLoader 版本列表的源顺序、超时与回退。
- 新增 C# `OptiFineListService`，接管 OptiFine 版本列表的源顺序、超时与回退。
- 新增 C# `ForgeListService`，接管 Forge Minecraft 版本列表的源顺序、超时与回退。
- 新增 C# `FabricListService`，接管 Fabric 版本列表的源顺序、超时与回退。
- 新增 C# `ClientListService`，接管 Minecraft 客户端版本列表的源顺序、超时、BMCLAPI 缓存合并与必需版本校验。
- 新增 C# `JavaProcessLauncher`，接管游戏 Java 进程的环境变量、工作目录、编码与重定向设置。
- 新增 C# `JvmInjectionArgumentBuilder`，接管 Nide/Authlib-Injector 的 JVM 注入参数。
- 新增 C# `JvmWrapperArgumentBuilder`，接管 JLW 与 LUA 的 JVM 参数。
- 新增 C# `JvmMemoryArgumentBuilder`，接管 `-Xmx`、G1GC/ZGC 选择与旧 GC 参数清理。
- 新增 C# `JvmEncodingArgumentBuilder`，接管 Java stdout/stderr 编码与 `file.encoding=COMPAT` 参数。
- 新增 C# `McFolderListParser`，接管自定义 Minecraft 文件夹设置的解析、格式化与无效项识别。
- 新增 C# `LibraryDeduplicator`，接管支持库去重、同名不同版本保留与较新版本选择。
- 新增 C# `LauncherProfilesService`，接管 `launcher_profiles.json` 的创建、登录信息合并与损坏重建；`McFolderLauncherProfilesJsonCreate` 已委托给该服务。
- 新增 C# `OptionsTxtUpdater`，接管 `options.txt` 的语言兼容与全屏设置。
- 下一步：将完整启动参数构建与登录参数迁移到 C#。

运行测试：

```powershell
& "C:\Users\neolx\.dotnet10\dotnet.exe" run --project "PCL.Core.Tests\PCL.Core.Tests.csproj" -c Release
```





