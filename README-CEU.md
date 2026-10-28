# Plain Craft Launcher (PCL) CEU 修改版

本仓库是第三方基于 **Plain Craft Launcher (PCL)** 独立修改的版本，**并非 PCL 官方作品，与官方无关**。

## 署名与许可

- 原作者：**龙腾猫跃（LTCat）**
- 原作者赞助：<https://meloong.com/afd/a/LTCat>
- 上游项目：<https://github.com/Meloong-Git/PCL>
- 使用许可：见仓库中的 `LICENCE` 文件；本改版按《PCL 存储库合理使用指南》中的**重度使用**条款处理。
- 本改版源代码：本 Git 仓库。发布时请将本仓库公开，或在此处补充公开仓库地址。

## 版本

- 版本号：`3.2.0-CEU`
- 输出文件：`PCL2-3.2.0ceu.exe`
- 程序集版本：`3.2.0.0`

## 本改版的主要调整

- 移除 Minecraft 下载相关的限制逻辑。
- 调整版本号、程序集名称与输出文件名。
- 在“关于”页面补充第三方修改声明，并保留原作者署名、赞助链接与上游源代码入口。
- 固定蓝色主色相，并隐藏与赞助解锁相关的主题入口。
- 内置 MCA Selector 2.9 区块编辑器（含 Java 运行时），可在“更多 → 百宝箱”中启动。
- 在“百宝箱”中加入“今日人品”：以当地日期对应的 Unix 时间戳为种子，每天生成固定的 0~100 人品值。

## 第三方组件

- **MCA Selector 2.9**：© 2018-2026 Querz，以 MIT License 分发。
  - 项目地址：<https://github.com/Querz/mcaselector>
  - 许可证文本随内置压缩包分发（解压后的 `LICENSE.txt`）。

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
