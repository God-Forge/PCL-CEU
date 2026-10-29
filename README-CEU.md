# Plain Craft Launcher (PCL) CEU 修改版

本仓库是第三方基于 **Plain Craft Launcher (PCL)** 独立修改的版本，**并非 PCL 官方作品，与官方无关**。

## 署名与许可

- 原作者：**龙腾猫跃（LTCat）**
- 原作者赞助：<https://meloong.com/afd/a/LTCat>
- 上游项目：<https://github.com/Meloong-Git/PCL>
- 使用许可：见仓库中的 `LICENCE` 文件；本改版按《PCL 存储库合理使用指南》中的**重度使用**条款处理。
- 本改版源代码：本 Git 仓库。发布时请将本仓库公开，或在此处补充公开仓库地址。

## 版本

- 版本号：`3.5.0-CEU`
- 输出文件：`PCL2-3.5.0ceu.exe`
- 程序集版本：`3.5.0.0`

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

## 命令行模式

以 `{文件名} {参数}` 方式启动时，PCL 会进入命令行模式，不再显示主窗口。命令参数使用 `--{英文单词}` 形式。

```powershell
.\PCL2-3.5.0ceu.exe --help
.\PCL2-3.5.0ceu.exe --version
.\PCL2-3.5.0ceu.exe --luck
.\PCL2-3.5.0ceu.exe --mca
.\PCL2-3.5.0ceu.exe --payload-encode .\versions\1.20.1 .\1.20.1.payload
.\PCL2-3.5.0ceu.exe --payload-decode .\1.20.1.payload .\decoded
.\PCL2-3.5.0ceu.exe --memory
```

| 命令 | 说明 |
|---|---|
| `--help`、`-h`、`/?` | 显示命令行帮助 |
| `--version`、`-v` | 显示版本、版本代码与程序集版本 |
| `--luck`、`--jrrp` | 输出今日人品值与评语 |
| `--mca`、`--mca-selector` | 启动内置 MCA Selector 区块编辑器 |
| `--payload-encode <文件夹> <输出.payload> [版本名]` | 将版本文件夹打包为 Payload |
| `--payload-decode <输入.payload> <输出文件夹>` | 解包 Payload |
| `--memory` | 执行内存优化；退出码为释放的 KB（0 表示无明显变化） |
| `--update ...` | PCL 内部自动更新命令，供更新器调用（高级） |
| `--gpu <java.exe>` | PCL 内部显卡设置命令（高级） |

说明：

- `--memory` 会自动请求管理员权限，并通过内部 helper 执行完整内存优化；如果取消 UAC，则不会执行。
- `--mca` 首次运行会从内置资源解压 MCA Selector 与 Java 运行时，之后直接启动。
- `--luck` 的结果按当地日期固定，同一天多次运行结果一致。
- `--help`、`--version`、`--luck` 在独立控制台中运行时会等待按键后退出；从 `cmd`/PowerShell 等已有控制台运行时直接输出。
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
.\PCL2-3.5.0ceu.exe --payload-encode .\versions\1.20.1 .\1.20.1.payload "1.20.1"
.\PCL2-3.5.0ceu.exe --payload-decode .\1.20.1.payload .\decoded
```

也可以在“更多 → 百宝箱 → Payload 版本包”中使用“打包当前版本”与“解包 Payload”。

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

运行测试：

```powershell
& "C:\Users\neolx\.dotnet10\dotnet.exe" run --project "PCL.Core.Tests\PCL.Core.Tests.csproj" -c Release
```

下一步计划：资源模块 DTO/搜索服务、崩溃日志解析核心。
