Imports Newtonsoft.Json.Linq
Imports PCLCS

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

        Select Case LoaderName
            Case "", "vanilla", "none"
                '原版
            Case "forge"
                ForgeEntry = GetForgeEntry(Version)
                ForgeVersion = ForgeEntry.VersionName
            Case "neoforge"
                NeoForgeEntry = GetNeoForgeEntry(Version)
                NeoForgeVersion = NeoForgeEntry.VersionName
            Case "fabric"
                FabricVersion = GetFabricLoaderVersion(Version)
            Case Else
                Throw New Exception("未知的加载器参数：" & Loader)
        End Select

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

    Private Function GetForgeEntry(Version As String) As DlForgeVersionEntry
        Dim ListLoader As New LoaderTask(Of String, List(Of DlForgeVersionEntry))("Forge Version List", AddressOf DlForgeVersionMain)
        ListLoader.WaitForExit(Version)
        If ListLoader.State <> LoadState.Finished OrElse ListLoader.Output Is Nothing Then
            Throw New Exception("获取 Forge 版本列表失败。")
        End If

        Dim Candidates = ListLoader.Output.
            Where(Function(Candidate) Candidate.Inherit = Version AndAlso Candidate.Category <> "universal" AndAlso Candidate.Category <> "client").
            ToList()
        If Not Candidates.Any() Then Throw New Exception($"没有找到适用于 Minecraft {Version} 的 Forge 版本。")

        Dim Selected = Candidates.FirstOrDefault(Function(Item) Item.IsRecommended)
        If Selected Is Nothing Then Selected = Candidates.OrderByDescending(Function(Item) Item.Version).First()
        Return Selected
    End Function

    Private Function GetNeoForgeEntry(Version As String) As DlNeoForgeListEntry
        DlNeoForgeListLoader.WaitForExit(0)
        If DlNeoForgeListLoader.State <> LoadState.Finished OrElse DlNeoForgeListLoader.Output.Value Is Nothing Then
            Throw New Exception("获取 NeoForge 版本列表失败。")
        End If

        Dim Candidates = DlNeoForgeListLoader.Output.Value.
            Where(Function(Candidate) Candidate.Inherit = Version).
            ToList()
        If Not Candidates.Any() Then Throw New Exception($"没有找到适用于 Minecraft {Version} 的 NeoForge 版本。")

        Dim Selected = Candidates.Where(Function(Item) Not Item.IsBeta).OrderByDescending(Function(Item) Item.Version).FirstOrDefault()
        If Selected Is Nothing Then Selected = Candidates.OrderByDescending(Function(Item) Item.Version).First()
        Return Selected
    End Function

    Private Function GetFabricLoaderVersion(Version As String) As String
        DlFabricListLoader.WaitForExit(0)
        If DlFabricListLoader.State <> LoadState.Finished OrElse DlFabricListLoader.Output.Value Is Nothing Then
            Throw New Exception("获取 Fabric 版本列表失败。")
        End If

        Dim Value As JObject = DlFabricListLoader.Output.Value
        Dim GameVersions = Value("game")?.ToObject(Of List(Of JObject))()
        If GameVersions Is Nothing OrElse Not GameVersions.Any(Function(Item) Item("version")?.ToString = Version) Then
            Throw New Exception($"Fabric 列表中没有 Minecraft {Version}。")
        End If

        Dim Loaders = Value("loader")?.ToObject(Of List(Of JObject))()
        If Loaders Is Nothing OrElse Loaders.Count = 0 Then Throw New Exception("Fabric 列表中没有可用的 Loader。")

        Dim Entry = Loaders.FirstOrDefault(Function(Item) Item("stable")?.ToObject(Of Boolean)() = True)
        If Entry Is Nothing Then Entry = Loaders.First()
        Dim LoaderVersion As String = Entry("version")?.ToString
        If String.IsNullOrEmpty(LoaderVersion) Then Throw New Exception("Fabric Loader 版本解析失败。")
        Return LoaderVersion
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
