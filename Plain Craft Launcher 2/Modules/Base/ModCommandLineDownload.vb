Imports Newtonsoft.Json.Linq
Imports PCLCS
Imports PCL.Core.Download
Imports PCL.Core.Net

''' <summary>
''' 命令行下载：下载原版 Minecraft，并按需安装 Forge / NeoForge / Fabric。
''' </summary>
Public Module ModCommandLineDownload

    Public Function Download(Version As String, Loader As String) As String
        If String.IsNullOrWhiteSpace(Version) Then Throw New Exception("需要指定 Minecraft 本体版本。")

        Dim McFolder As String = EnsureMcFolder()
        Dim LoaderName As String = If(Loader, "").Trim().ToLowerInvariant()
        Dim ForgeEntry As DlForgeVersionEntry = Nothing
        Dim NeoForgeEntry As DlNeoForgeListEntry = Nothing
        Dim ForgeVersion As String = Nothing
        Dim NeoForgeVersion As String = Nothing
        Dim FabricVersion As String = Nothing

        Using Fetcher As New HttpStringFetcher()
            Dim Service As New LoaderVersionService(Fetcher)
            Select Case LoaderName
                Case "", "vanilla", "none"
                    '原版
                Case "forge"
                    ForgeEntry = GetForgeEntry(Service, Version)
                    ForgeVersion = ForgeEntry.VersionName
                Case "neoforge"
                    NeoForgeEntry = GetNeoForgeEntry(Service, Version)
                    NeoForgeVersion = NeoForgeEntry.VersionName
                Case "fabric"
                    FabricVersion = GetFabricLoaderVersion(Service, Version)
                Case Else
                    Throw New Exception("未知的加载器参数：" & Loader)
            End Select
        End Using

        Dim InstanceName As String = GetInstanceName(Version, LoaderName, ForgeVersion, NeoForgeVersion, FabricVersion)
        Dim Components As New McComponents(Version, Nothing, ForgeVersion, NeoForgeVersion, FabricVersion, Nothing, Nothing, Nothing)
        Dim Request As New McInstallRequest With {
            .NewInstanceName = InstanceName,
            .InstanceFolder = McFolder & "versions\" & InstanceName & "\",
            .Components = Components,
            .MinecraftJson = Nothing,
            .ForgeEntry = ForgeEntry,
            .NeoForgeEntry = NeoForgeEntry
        }

        If FileUtils.Exists(Request.InstanceFolder & InstanceName & ".json") Then
            Throw New Exception("实例已存在：" & InstanceName)
        End If

        Dim Loaders As List(Of LoaderBase) = McInstallLoadersGet(Request)
        Dim Combo As New LoaderCombo(Of String)(InstanceName & " 安装", Loaders)
        Combo.WaitForExit(Request.InstanceFolder)
        If Combo.State <> LoadState.Finished Then
            Throw New Exception("安装未完成（状态：" & Combo.State.ToString & "）")
        End If

        McInstanceListForceRefresh = True
        Return $"已安装 {InstanceName}"
    End Function

    Public Function EnsureMcFolder() As String
        Dim Folder As String = Settings.Get(Of String)("LaunchFolderSelect")
        If String.IsNullOrWhiteSpace(Folder) Then Folder = Paths.Base & ".minecraft\"
        Folder = Folder.Replace("$", Paths.Base)
        DirectoryUtils.Create(Folder)
        McFolderSelected = Folder
        Return McFolderSelected
    End Function

    Private Function GetForgeEntry(Service As LoaderVersionService, Version As String) As DlForgeVersionEntry
        Dim Info = Service.FindForgeAsync(Version).Run()
        Return New DlForgeVersionEntry(Info.VersionName, Nothing, Version) With {
            .Category = "installer",
            .FileVersion = Info.VersionName,
            .IsRecommended = Info.IsRecommended
        }
    End Function

    Private Function GetNeoForgeEntry(Service As LoaderVersionService, Version As String) As DlNeoForgeListEntry
        Dim Info = Service.FindNeoForgeAsync(Version).Run()
        Return New DlNeoForgeListEntry(Info.VersionName)
    End Function

    Private Function GetFabricLoaderVersion(Service As LoaderVersionService, Version As String) As String
        Return Service.FindFabricAsync(Version).Run().VersionName
    End Function

    Private Function GetInstanceName(Version As String, Loader As String, ForgeVersion As String, NeoForgeVersion As String, FabricVersion As String) As String
        Select Case Loader
            Case "forge"
                Return $"{Version}-Forge_{ForgeVersion}"
            Case "neoforge"
                Return $"{Version}-NeoForge_{NeoForgeVersion.BeforeFirst("+")}"
            Case "fabric"
                Return $"{Version}-Fabric {FabricVersion.BeforeFirst("+")}"
            Case Else
                Return Version
        End Select
    End Function

End Module
