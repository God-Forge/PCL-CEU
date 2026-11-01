Imports System.Diagnostics
Imports PCL.Core.Download
Imports PCL.Core.Memory
Imports PCL.Core.Minecraft
Imports PCL.Core.Tools

Public Class PageOtherTest

    Private Const McASelectorVersion As String = "2.9"

    Private Shared ReadOnly Property ToolFolder As String
        Get
            Return PathPure.Value & "Tools\MCASelector\"
        End Get
    End Property
    Private Shared ReadOnly Property ToolExe As String
        Get
            Return ToolFolder & "MCA Selector.exe"
        End Get
    End Property

    Private Sub BtnJrrp_Click(sender As Object, e As EventArgs) Handles BtnJrrp.Click
        Jrrp()
    End Sub

    Private Shared _MemoryBusy As Boolean
    Private Shared _PayloadBusy As Boolean
    Private Shared _MaintenanceBusy As Boolean
    Private Shared _JavaBusy As Boolean
    Private Shared _AdvancedBusy As Boolean

    Private Sub PageOtherTest_Loaded(sender As Object, e As EventArgs) Handles Me.Loaded
        RefreshMemoryStatus()
    End Sub

    Private Sub BtnMemoryKind_Click(sender As Object, e As EventArgs) Handles BtnMemoryQuick.Click, BtnMemoryDeep.Click, BtnMemoryCurrent.Click, BtnMemoryWorkingSet.Click, BtnMemorySystemWorkingSet.Click, BtnMemoryModified.Click, BtnMemoryStandby.Click, BtnMemoryPriority0.Click
        Dim Tag As String = TryCast(DirectCast(sender, MyButton).Tag, String)
        Select Case Tag
            Case "Quick"
                RunMemoryOperation(MemoryOptimizationKind.WorkingSets, "将清空所有进程的工作集，其他程序可能会短暂变慢。是否继续？")
            Case "Deep"
                RunMemoryAllOptimization(True, "深度清理会清空工作集、系统工作集、已修改页列表和待机列表，可能造成短暂卡顿。是否继续？")
            Case "CurrentProcess"
                RunMemoryOperation(MemoryOptimizationKind.CurrentProcess)
            Case "WorkingSets"
                RunMemoryOperation(MemoryOptimizationKind.WorkingSets, "将清空所有进程的工作集，其他程序可能会短暂变慢。是否继续？")
            Case "SystemWorkingSet"
                RunMemoryOperation(MemoryOptimizationKind.SystemWorkingSet, "将清空系统工作集/文件缓存，可能造成短暂卡顿。是否继续？")
            Case "ModifiedPageList"
                RunMemoryOperation(MemoryOptimizationKind.ModifiedPageList, "将把已修改页写入磁盘并清空列表，可能产生磁盘 I/O。是否继续？")
            Case "StandbyList"
                RunMemoryOperation(MemoryOptimizationKind.StandbyList, "将清空系统待机缓存，程序首次读取文件时可能变慢。是否继续？")
            Case "Priority0StandbyList"
                RunMemoryOperation(MemoryOptimizationKind.Priority0StandbyList, "将清空优先级 0 待机缓存，程序首次读取文件时可能变慢。是否继续？")
        End Select
    End Sub

    Private Sub BtnMemoryRefresh_Click(sender As Object, e As EventArgs) Handles BtnMemoryRefresh.Click
        RefreshMemoryStatus()
    End Sub

    Private Sub BtnMemoryAdmin_Click(sender As Object, e As EventArgs) Handles BtnMemoryAdmin.Click
        RunMemoryElevated("memory", "将以管理员权限执行完整内存优化（包含待机列表）。是否继续？")
    End Sub

    Private Sub RunMemoryOperation(Kind As MemoryOptimizationKind, Optional ConfirmText As String = Nothing)
        If Not MemoryOptimizer.IsAdministrator AndAlso MemoryKindRequiresAdmin(Kind) Then
            RunMemoryElevated(MemoryTaskName(Kind), ConfirmText)
            Return
        End If

        If RunMemoryConfirm(ConfirmText) Then Return
        _MemoryBusy = True
        RunInThread(
        Sub()
            Try
                Dim Result = MemoryOptimizer.Optimize(Kind)
                RunInUi(
                Sub()
                    RefreshMemoryStatus(Result.After)
                    Hint(Result.Message, If(Result.Success, HintType.Green, HintType.Red), Log:=False)
                    _MemoryBusy = False
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "内存优化失败")
                RunInUi(
                Sub()
                    Hint("内存优化失败：" & ex.Message, HintType.Red)
                    _MemoryBusy = False
                End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub RunMemoryAllOptimization(IncludeStandbyLists As Boolean, Optional ConfirmText As String = Nothing)
        If Not MemoryOptimizer.IsAdministrator Then
            RunMemoryElevated("memory", ConfirmText)
            Return
        End If

        If RunMemoryConfirm(ConfirmText) Then Return
        _MemoryBusy = True
        RunInThread(
        Sub()
            Try
                Dim Result = MemoryOptimizer.OptimizeAll(IncludeStandbyLists)
                RunInUi(
                Sub()
                    RefreshMemoryStatus(Result.After)
                    Hint(Result.Message, If(Result.Success, HintType.Green, HintType.Red), Log:=False)
                    _MemoryBusy = False
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "内存优化失败")
                RunInUi(
                Sub()
                    Hint("内存优化失败：" & ex.Message, HintType.Red)
                    _MemoryBusy = False
                End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub RunMemoryElevated(TaskName As String, Optional ConfirmText As String = Nothing)
        If RunMemoryConfirm(ConfirmText) Then Return
        _MemoryBusy = True
        RunInThread(
        Sub()
            Try
                Dim Code As Integer = RunAsAdmin("--elevated " & TaskName)
                RunInUi(
                Sub()
                    RefreshMemoryStatus()
                    If Code >= 0 Then
                        Hint($"管理员内存优化已完成，释放约 {Code} KB。", HintType.Green, Log:=False)
                    Else
                        Hint($"管理员内存优化失败（返回码 {Code}）。", HintType.Red, Log:=False)
                    End If
                    _MemoryBusy = False
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "以管理员权限执行内存优化失败")
                RunInUi(
                Sub()
                    Hint("以管理员权限执行内存优化失败：" & ex.Message, HintType.Red)
                    _MemoryBusy = False
                End Sub)
            End Try
        End Sub)
    End Sub

    Private Function MemoryKindRequiresAdmin(Kind As MemoryOptimizationKind) As Boolean
        Select Case Kind
            Case MemoryOptimizationKind.SystemWorkingSet,
                 MemoryOptimizationKind.SystemFileCache,
                 MemoryOptimizationKind.ModifiedPageList,
                 MemoryOptimizationKind.StandbyList,
                 MemoryOptimizationKind.Priority0StandbyList
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Function MemoryTaskName(Kind As MemoryOptimizationKind) As String
        Select Case Kind
            Case MemoryOptimizationKind.WorkingSets
                Return "memory:working-sets"
            Case MemoryOptimizationKind.SystemWorkingSet, MemoryOptimizationKind.SystemFileCache
                Return "memory:system-working-set"
            Case MemoryOptimizationKind.ModifiedPageList
                Return "memory:modified-page-list"
            Case MemoryOptimizationKind.StandbyList
                Return "memory:standby-list"
            Case MemoryOptimizationKind.Priority0StandbyList
                Return "memory:priority0-standby-list"
            Case Else
                Return "memory"
        End Select
    End Function

    Private Function RunMemoryConfirm(ConfirmText As String) As Boolean
        If _MemoryBusy Then
            Hint("内存优化正在进行中，请稍候……", Log:=False)
            Return True
        End If
        If String.IsNullOrEmpty(ConfirmText) Then Return False
        Return MyMsgBox(ConfirmText, "内存优化", "继续", "取消") <> 1
    End Function

    Private Sub RefreshMemoryStatus(Optional Status As MemoryStatus = Nothing)
        Try
            Dim Current As MemoryStatus = If(Status, MemoryOptimizer.GetStatus())
            RunInUi(
            Sub()
                LabMemoryStatus.Text = $"当前可用物理内存：{Current.AvailableMB:F0} MB / 共 {Current.TotalMB:F0} MB"
                If MemoryOptimizer.IsAdministrator Then
                    LabMemoryAdmin.Text = "当前权限：管理员，可执行系统级清理。"
                    BtnMemoryAdmin.Visibility = Visibility.Collapsed
                Else
                    LabMemoryAdmin.Text = "当前权限：普通用户；系统级清理会按需请求管理员权限。"
                    BtnMemoryAdmin.Visibility = Visibility.Visible
                End If
            End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "读取内存状态失败")
            RunInUi(Sub() LabMemoryStatus.Text = "当前可用物理内存：读取失败")
        End Try
    End Sub

    Private Sub BtnPayloadEncode_Click(sender As Object, e As EventArgs) Handles BtnPayloadEncode.Click
        If _PayloadBusy Then
            Hint("Payload 操作正在进行中，请稍候……", Log:=False)
            Return
        End If
        If McInstanceSelected Is Nothing Then
            Hint("请先选择一个 Minecraft 实例！", HintType.Red)
            Return
        End If

        Dim Output As String = Dialogs.SaveFile("保存 Payload 文件", McInstanceSelected.Name & ".payload", McFolderSelected & "versions", filter:={("payload", "Payload 文件")})
        If String.IsNullOrEmpty(Output) Then Return

        Dim Input As String = McInstanceSelected.PathVersion
        Dim VersionName As String = McInstanceSelected.Name
        _PayloadBusy = True
        Hint("正在打包 Payload，请稍候……", Log:=False)
        RunInThread(
        Sub()
            Try
                ModPayload.Encode(Input, Output, VersionName)
                RunInUi(
                Sub()
                    _PayloadBusy = False
                    Hint("Payload 打包完成：" & Output, HintType.Green, Log:=False)
                    OpenExplorer(IO.Path.GetDirectoryName(Output))
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "打包 Payload 失败")
                RunInUi(
                Sub()
                    _PayloadBusy = False
                    Hint("打包 Payload 失败：" & ex.Message, HintType.Red)
                End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnPayloadDecode_Click(sender As Object, e As EventArgs) Handles BtnPayloadDecode.Click
        If _PayloadBusy Then
            Hint("Payload 操作正在进行中，请稍候……", Log:=False)
            Return
        End If

        Dim Input As String = Dialogs.SelectFile("选择 Payload 文件", False, filter:={({"payload"}, "Payload 文件")}).FirstOrDefault()
        If String.IsNullOrEmpty(Input) Then Return

        Dim Output As String = Dialogs.SelectFolder("选择解包目标文件夹", False, McFolderSelected & "versions").FirstOrDefault()
        If String.IsNullOrEmpty(Output) Then Return

        _PayloadBusy = True
        Hint("正在解包 Payload，请稍候……", Log:=False)
        RunInThread(
        Sub()
            Try
                ModPayload.Decode(Input, Output)
                RunInUi(
                Sub()
                    _PayloadBusy = False
                    Hint("Payload 解包完成：" & Output, HintType.Green, Log:=False)
                    OpenExplorer(Output)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "解包 Payload 失败")
                RunInUi(
                Sub()
                    _PayloadBusy = False
                    Hint("解包 Payload 失败：" & ex.Message, HintType.Red)
                End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnPayloadImport_Click(sender As Object, e As EventArgs) Handles BtnPayloadImport.Click
        If _PayloadBusy Then
            Hint("Payload 操作正在进行中，请稍候……", Log:=False)
            Return
        End If

        Dim Input As String = Dialogs.SelectFile("选择要导入的 Payload 文件", False, filter:={({"payload"}, "Payload 文件")}).FirstOrDefault()
        If String.IsNullOrEmpty(Input) Then Return

        _PayloadBusy = True
        Hint("正在导入 Payload，请稍候……", Log:=False)
        RunInThread(
        Sub()
            Try
                Dim Message As String = ModPayload.Import(Input, McFolderSelected)
                RunInUi(
                Sub()
                    _PayloadBusy = False
                    Hint(Message, HintType.Green, Log:=False)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "导入 Payload 失败")
                RunInUi(
                Sub()
                    _PayloadBusy = False
                    Hint("导入 Payload 失败：" & ex.Message, HintType.Red)
                End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnWorldBackup_Click(sender As Object, e As EventArgs) Handles BtnWorldBackup.Click
        If McInstanceSelected Is Nothing Then
            Hint("请先选择一个 Minecraft 实例！", HintType.Red)
            Return
        End If
        If _MaintenanceBusy Then
            Hint("正在执行实例维护操作，请稍候……", Log:=False)
            Return
        End If

        _MaintenanceBusy = True
        Dim Instance As McInstance = McInstanceSelected
        Hint("正在备份当前实例存档……", Log:=False)
        RunInThread(
        Sub()
            Try
                Dim Info = ModWorldBackup.BackupSaves(Instance)
                RunInUi(
                Sub()
                    _MaintenanceBusy = False
                    Hint($"存档备份完成：{Info.Path}（{ModWorldBackup.FormatSize(Info.Size)}）", HintType.Green, Log:=False)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "备份存档失败")
                RunInUi(
                Sub()
                    _MaintenanceBusy = False
                    Hint("备份存档失败：" & ex.Message, HintType.Red)
                End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnWorldRestore_Click(sender As Object, e As EventArgs) Handles BtnWorldRestore.Click
        If McInstanceSelected Is Nothing Then
            Hint("请先选择一个 Minecraft 实例！", HintType.Red)
            Return
        End If
        If _MaintenanceBusy Then
            Hint("正在执行实例维护操作，请稍候……", Log:=False)
            Return
        End If
        If MyMsgBox("恢复最近备份会覆盖当前 saves 文件夹，恢复前会先自动备份当前存档。是否继续？", "恢复存档", "继续", "取消") <> 1 Then Return

        _MaintenanceBusy = True
        Dim Instance As McInstance = McInstanceSelected
        Hint("正在恢复存档备份……", Log:=False)
        RunInThread(
        Sub()
            Try
                Dim Info = ModWorldBackup.RestoreLatestSaves(Instance)
                RunInUi(
                Sub()
                    _MaintenanceBusy = False
                    Hint("存档恢复完成：" & Info.Path, HintType.Green, Log:=False)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "恢复存档失败")
                RunInUi(
                Sub()
                    _MaintenanceBusy = False
                    Hint("恢复存档失败：" & ex.Message, HintType.Red)
                End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnWorldBackupOpen_Click(sender As Object, e As EventArgs) Handles BtnWorldBackupOpen.Click
        Try
            OpenExplorer(ModWorldBackup.BackupRoot)
        Catch ex As Exception
            Hint("打开备份目录失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnInstanceCheck_Click(sender As Object, e As EventArgs) Handles BtnInstanceCheck.Click
        If McInstanceSelected Is Nothing Then
            Hint("请先选择一个 Minecraft 实例！", HintType.Red)
            Return
        End If
        If _MaintenanceBusy Then
            Hint("正在执行实例维护操作，请稍候……", Log:=False)
            Return
        End If

        _MaintenanceBusy = True
        Dim Instance As McInstance = McInstanceSelected
        Hint("正在检查实例……", Log:=False)
        RunInThread(
        Sub()
            Try
                Dim Result = ModInstanceRepair.Check(Instance)
                Dim Message As String = ModInstanceRepair.FormatCheck(Result)
                RunInUi(
                Sub()
                    _MaintenanceBusy = False
                    MyMsgBox(Message, "实例体检", IsWarn:=Result.HasErrors)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "检查实例失败")
                RunInUi(
                Sub()
                    _MaintenanceBusy = False
                    Hint("检查实例失败：" & ex.Message, HintType.Red)
                End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnInstanceRepair_Click(sender As Object, e As EventArgs) Handles BtnInstanceRepair.Click
        If McInstanceSelected Is Nothing Then
            Hint("请先选择一个 Minecraft 实例！", HintType.Red)
            Return
        End If
        If _MaintenanceBusy Then
            Hint("正在执行实例维护操作，请稍候……", Log:=False)
            Return
        End If
        If MyMsgBox("修复基础文件会重新下载缺失的原版 JSON、客户端 JAR、libraries 与 assets。是否继续？", "修复实例", "继续", "取消") <> 1 Then Return

        _MaintenanceBusy = True
        Dim Instance As McInstance = McInstanceSelected
        Hint("正在修复实例基础文件……", Log:=False)
        RunInThread(
        Sub()
            Try
                Dim Message As String = ModInstanceRepair.RepairBase(Instance)
                RunInUi(
                Sub()
                    _MaintenanceBusy = False
                    Hint(Message, HintType.Green, Log:=False)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "修复实例失败")
                RunInUi(
                Sub()
                    _MaintenanceBusy = False
                    Hint("修复实例失败：" & ex.Message, HintType.Red)
                End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnModsCheck_Click(sender As Object, e As EventArgs) Handles BtnModsCheck.Click
        If McInstanceSelected Is Nothing Then
            Hint("请先选择一个 Minecraft 实例！", HintType.Red)
            Return
        End If
        If _MaintenanceBusy Then
            Hint("正在执行实例维护操作，请稍候……", Log:=False)
            Return
        End If

        _MaintenanceBusy = True
        Dim Instance As McInstance = McInstanceSelected
        Hint("正在检查 Mod……", Log:=False)
        RunInThread(
        Sub()
            Try
                Dim Result = ModInstanceRepair.CheckMods(Instance)
                Dim Message As String = ModInstanceRepair.FormatModScan(Result)
                RunInUi(
                Sub()
                    _MaintenanceBusy = False
                    MyMsgBox(Message, "Mod 体检", IsWarn:=Result.HasErrors)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "检查 Mod 失败")
                RunInUi(
                Sub()
                    _MaintenanceBusy = False
                    Hint("检查 Mod 失败：" & ex.Message, HintType.Red)
                End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnJavaPack_Click(sender As Object, e As EventArgs) Handles BtnJavaPack.Click
        If _JavaBusy Then
            Hint("Java 包操作正在进行中，请稍候……", Log:=False)
            Return
        End If

        Dim JavaExe As String = Dialogs.SelectFile("选择 java.exe（通常在 bin 文件夹中）", False, filter:="java.exe|java.exe").FirstOrDefault()
        If String.IsNullOrEmpty(JavaExe) Then Return

        Try
            Dim Info = ModJavaPackage.Detect(JavaExe)
            Dim DefaultName As String = ModJavaPackage.GetDefaultFileName(Info)
            Dim Output As String = Dialogs.SaveFile("保存 Java 包", DefaultName, ModJavaPackage.PackageRoot, filter:={("javapkg", "Java 包")})
            If String.IsNullOrEmpty(Output) Then Return

            _JavaBusy = True
            Hint("正在打包 Java，文件较大时可能需要一会儿……", Log:=False)
            RunInThread(
            Sub()
                Try
                    Dim Result = ModJavaPackage.Pack(JavaExe, Output)
                    RunInUi(
                    Sub()
                        _JavaBusy = False
                        Hint($"Java 包已创建：{Result.Path}（{ModJavaPackage.FormatSize(Result.Size)}）", HintType.Green, Log:=False)
                        OpenExplorer(ModJavaPackage.PackageRoot)
                    End Sub)
                Catch ex As Exception
                    Logger.Error(ex, "打包 Java 失败")
                    RunInUi(
                    Sub()
                        _JavaBusy = False
                        Hint("打包 Java 失败：" & ex.Message, HintType.Red)
                    End Sub)
                End Try
            End Sub)
        Catch ex As Exception
            Logger.Error(ex, "识别 Java 失败")
            Hint("识别 Java 失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnJavaPackImport_Click(sender As Object, e As EventArgs) Handles BtnJavaPackImport.Click
        If _JavaBusy Then
            Hint("Java 包操作正在进行中，请稍候……", Log:=False)
            Return
        End If

        Dim Input As String = Dialogs.SelectFile("选择 Java 包", False, filter:={({"javapkg"}, "Java 包")}).FirstOrDefault()
        If String.IsNullOrEmpty(Input) Then Return

        _JavaBusy = True
        Hint("正在导入 Java 包……", Log:=False)
        RunInThread(
        Sub()
            Try
                Dim Result = ModJavaPackage.Import(Input)
                RunInUi(
                Sub()
                    _JavaBusy = False
                    Hint($"Java 已导入并加入列表：{Result.JavaHome}", HintType.Green, Log:=False)
                    OpenExplorer(Result.JavaHome)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "导入 Java 包失败")
                RunInUi(
                Sub()
                    _JavaBusy = False
                    Hint("导入 Java 包失败：" & ex.Message, HintType.Red)
                End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnJavaPackOpen_Click(sender As Object, e As EventArgs) Handles BtnJavaPackOpen.Click
        Try
            OpenExplorer(ModJavaPackage.PackageRoot)
        Catch ex As Exception
            Hint("打开 Java 包目录失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnLaunch_Click(sender As Object, e As EventArgs) Handles BtnLaunch.Click
        Dim Instance As McInstance = McInstanceSelected
        If Instance Is Nothing OrElse Not DirectoryUtils.Exists(PathUtils.AddSlashSuffix(Instance.PathIndie) & "saves\") Then
            LaunchMcASelector()
            Return
        End If
        If _MaintenanceBusy Then
            Hint("正在执行实例维护操作，请稍候……", Log:=False)
            Return
        End If

        _MaintenanceBusy = True
        Hint("启动 MCA Selector 前正在备份当前存档……", Log:=False)
        RunInThread(
        Sub()
            Try
                ModWorldBackup.BackupSaves(Instance)
                RunInUi(
                Sub()
                    _MaintenanceBusy = False
                    LaunchMcASelector()
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "MCA Selector 前置备份失败")
                RunInUi(
                Sub()
                    _MaintenanceBusy = False
                    If MyMsgBox("存档备份失败：" & ex.Message & vbCrLf & "仍要启动 MCA Selector 吗？", "MCA Selector", "仍要启动", "取消") = 1 Then
                        LaunchMcASelector()
                    End If
                End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnSaves_Click(sender As Object, e As EventArgs) Handles BtnSaves.Click
        If McInstanceSelected Is Nothing Then
            Hint("请先选择一个 Minecraft 实例！", HintType.Red)
            Return
        End If
        Try
            Dim SavesPath As String = McInstanceSelected.PathIndie & "saves\"
            DirectoryUtils.Create(SavesPath)
            OpenExplorer(SavesPath)
        Catch ex As Exception
            Logger.Error(ex, "打开当前实例存档文件夹失败")
            Hint("打开存档文件夹失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnChooseWorld_Click(sender As Object, e As EventArgs) Handles BtnChooseWorld.Click
        Try
            Dim Folder As String = Dialogs.SelectFolder("选择 Minecraft 世界文件夹", False).FirstOrDefault()
            If String.IsNullOrWhiteSpace(Folder) Then Return
            ClipboardSet(Folder, False)
            LaunchMcASelector()
            Hint("已复制世界文件夹路径，请在 MCA Selector 中打开该文件夹。", Log:=False)
        Catch ex As Exception
            Logger.Error(ex, "选择世界文件夹失败")
            Hint("选择世界文件夹失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnHome_Click(sender As Object, e As EventArgs) Handles BtnHome.Click
        OpenWebsite("https://github.com/Querz/mcaselector")
    End Sub

    ''' <summary>
    ''' 解压内置的 MCA Selector（如果需要），并返回可执行文件路径。
    ''' </summary>
    Public Shared Function PrepareMcASelector(Optional ShowHint As Boolean = False) As String
        Dim ZipPath As String = PathPure.Value & "Tools\McASelector.zip"
        Dim VersionPath As String = ToolFolder & "version.txt"
        If Not FileUtils.Exists(ToolExe) OrElse Not FileUtils.Exists(ToolFolder & "runtime\bin\java.exe") OrElse Not FileUtils.Exists(ToolFolder & "app\mcaselector-2.9-all.jar") OrElse Not FileUtils.Exists(VersionPath) OrElse FileUtils.ReadAsString(VersionPath).Trim() <> McASelectorVersion Then
            If ShowHint Then Hint("正在解压内置的 MCA Selector，首次启动可能需要一会儿……", Log:=False)
            DirectoryUtils.Create(PathPure.Value & "Tools\")
            If DirectoryUtils.Exists(ToolFolder) Then DirectoryUtils.Delete(ToolFolder)
            ExtractResources(ZipPath, "McASelector")
            FileUtils.ExtractToDirectory(ZipPath, ToolFolder)
            FileUtils.Write(VersionPath, McASelectorVersion)
            Try
                FileUtils.Delete(ZipPath)
            Catch ex As Exception
                Logger.Warn(ex, "删除临时 MCA Selector 压缩包失败")
            End Try
            Logger.Info("已解压内置 MCA Selector " & McASelectorVersion)
        End If
        If Not FileUtils.Exists(ToolExe) Then Throw New Exception("未找到 MCA Selector.exe")
        Return ToolExe
    End Function

    ''' <summary>
    ''' 解压内置的 MCA Selector（首次启动）并打开它。
    ''' </summary>
    Public Shared Sub LaunchMcASelector()
        RunInThread(
        Sub()
            Try
                Dim ExePath = PrepareMcASelector(True)
                StartProcess(New ProcessStartInfo With {
                    .FileName = ExePath,
                    .WorkingDirectory = ToolFolder,
                    .UseShellExecute = True
                })
            Catch ex As Exception
                Logger.Error(ex, "启动 MCA Selector 失败")
                Hint("启动 MCA Selector 失败：" & ex.Message, HintType.Red)
            End Try
        End Sub)
    End Sub

    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    ''' <summary>
    ''' 今日人品：以当地日期的 Unix 时间戳（天）作为种子，生成 0~100 的人品值。
    ''' </summary>
    Public Shared Sub Jrrp()
        Dim Result = DailyLuck.GetToday()
        MyMsgBox("今日人品：" & Result.Value & vbCrLf & vbCrLf & Result.Comment, "今日人品", IsWarn:=Result.Value < 10, IsWarnText:=Result.Value < 10)
    End Sub
    Public Shared Sub RubbishClear()
        Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        Try
            Dim Result = MemoryOptimizer.Optimize(MemoryOptimizationKind.WorkingSets)
            If ShowHint Then Hint(Result.Message, If(Result.Success, HintType.Green, HintType.Red), Log:=False)
        Catch ex As Exception
            Logger.Error(ex, "内存优化失败")
            If ShowHint Then Hint("内存优化失败：" & ex.Message, HintType.Red)
        End Try
    End Sub
    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        Dim Result = MemoryOptimizer.OptimizeAll(True)
        If ShowHint Then Hint(Result.Message, If(Result.Success, HintType.Green, HintType.Red), Log:=False)
    End Sub
    Public Shared Function GetRandomCave() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function
    Public Shared Function GetRandomHint() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function
    Public Shared Function GetRandomPresetHint() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function

    Private Function AdvancedInstance() As McInstance
        If McInstanceSelected Is Nothing Then
            Hint("请先选择一个 Minecraft 实例！", HintType.Red)
            Return Nothing
        End If
        If _AdvancedBusy Then
            Hint("实例高级操作正在进行中，请稍候……", Log:=False)
            Return Nothing
        End If
        Return McInstanceSelected
    End Function

    Private Sub BtnCrashDiagnostic_Click(sender As Object, e As EventArgs) Handles BtnCrashDiagnostic.Click
        Dim Instance = AdvancedInstance()
        If Instance Is Nothing Then Return
        Dim Output = Dialogs.SaveFile("保存崩溃诊断包", "诊断包-" & Instance.Name & "-" & Date.Now.ToString("yyyyMMdd-HHmmss") & ".zip", McFolderSelected, filter:={("zip", "诊断包")})
        If String.IsNullOrEmpty(Output) Then Return
        _AdvancedBusy = True
        Hint("正在生成诊断包……", Log:=False)
        RunInThread(
        Sub()
            Try
                Dim Result = DiagnosticPackageService.Create(Instance.PathVersion, McFolderSelected, Output)
                RunInUi(
                Sub()
                    _AdvancedBusy = False
                    Hint($"诊断包已生成：{Result.Path}", HintType.Green, Log:=False)
                    OpenExplorer(IO.Path.GetDirectoryName(Result.Path))
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "生成诊断包失败")
                RunInUi(Sub()
                            _AdvancedBusy = False
                            Hint("生成诊断包失败：" & ex.Message, HintType.Red)
                        End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnCloneInstance_Click(sender As Object, e As EventArgs) Handles BtnCloneInstance.Click
        Dim Instance = AdvancedInstance()
        If Instance Is Nothing Then Return
        Dim TargetRoot = Dialogs.SelectFolder("选择克隆到的 Minecraft 文件夹", False, McFolderSelected).FirstOrDefault()
        If String.IsNullOrEmpty(TargetRoot) Then Return
        Dim IncludeSaves = MyMsgBox("是否同时复制存档？", "克隆实例", "包含存档", "不包含存档") = 1
        Dim TargetName = Instance.Name & "-Copy"
        Dim TargetPath = IO.Path.Combine(TargetRoot, "versions", TargetName)
        Dim Index = 2
        While Directory.Exists(TargetPath)
            TargetName = Instance.Name & "-Copy" & Index
            TargetPath = IO.Path.Combine(TargetRoot, "versions", TargetName)
            Index += 1
        End While
        _AdvancedBusy = True
        Hint("正在克隆实例……", Log:=False)
        RunInThread(
        Sub()
            Try
                InstanceCloneService.Clone(Instance.PathVersion, TargetPath,
                    New InstanceCloneOptions With {.IncludeSaves = IncludeSaves})
                RunInUi(
                Sub()
                    _AdvancedBusy = False
                    McInstanceListForceRefresh = True
                    Hint($"实例已克隆到：{TargetPath}", HintType.Green, Log:=False)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "克隆实例失败")
                RunInUi(Sub()
                            _AdvancedBusy = False
                            Hint("克隆实例失败：" & ex.Message, HintType.Red)
                        End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnSnapshot_Click(sender As Object, e As EventArgs) Handles BtnSnapshot.Click
        Dim Instance = AdvancedInstance()
        If Instance Is Nothing Then Return
        _AdvancedBusy = True
        Hint("正在创建实例快照……", Log:=False)
        RunInThread(
        Sub()
            Try
                Dim Info = InstanceSnapshotService.Create(Instance.PathVersion, PathPure.Value & "InstanceSnapshots\")
                RunInUi(
                Sub()
                    _AdvancedBusy = False
                    Hint($"快照已创建：{Info.Name}", HintType.Green, Log:=False)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "创建实例快照失败")
                RunInUi(Sub()
                            _AdvancedBusy = False
                            Hint("创建实例快照失败：" & ex.Message, HintType.Red)
                        End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnSnapshotRestore_Click(sender As Object, e As EventArgs) Handles BtnSnapshotRestore.Click
        Dim Instance = AdvancedInstance()
        If Instance Is Nothing Then Return
        Dim Snapshots = InstanceSnapshotService.List(Instance.PathVersion, PathPure.Value & "InstanceSnapshots\")
        If Snapshots.Count = 0 Then
            Hint("当前实例没有可用快照。", HintType.Red)
            Return
        End If
        Dim Latest = Snapshots.First
        If MyMsgBox($"恢复快照 {Latest.Name} 会覆盖当前 Mod 与配置，是否继续？", "恢复实例快照", "继续", "取消") <> 1 Then Return
        _AdvancedBusy = True
        Hint("正在恢复实例快照……", Log:=False)
        RunInThread(
        Sub()
            Try
                InstanceSnapshotService.Restore(Latest.Path, Instance.PathVersion)
                RunInUi(
                Sub()
                    _AdvancedBusy = False
                    Hint("实例快照已恢复。", HintType.Green, Log:=False)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "恢复实例快照失败")
                RunInUi(Sub()
                            _AdvancedBusy = False
                            Hint("恢复实例快照失败：" & ex.Message, HintType.Red)
                        End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnResourceScan_Click(sender As Object, e As EventArgs) Handles BtnResourceScan.Click
        Dim Instance = AdvancedInstance()
        If Instance Is Nothing Then Return
        _AdvancedBusy = True
        RunInThread(
        Sub()
            Try
                Dim Result = ResourcePackService.Scan(Instance.PathVersion)
                RunInUi(
                Sub()
                    _AdvancedBusy = False
                    MyMsgBox(ResourcePackService.Format(Result), "资源包/光影", IsWarn:=Result.Issues.Count > 0)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "扫描资源包失败")
                RunInUi(Sub()
                            _AdvancedBusy = False
                            Hint("扫描资源包失败：" & ex.Message, HintType.Red)
                        End Sub)
            End Try
        End Sub)
    End Sub

    Private Sub BtnResourceToggle_Click(sender As Object, e As EventArgs) Handles BtnResourceToggle.Click
        Dim Instance = AdvancedInstance()
        If Instance Is Nothing Then Return
        Dim FilePath = Dialogs.SelectFile("选择要启用/禁用的资源包或光影", False, Instance.PathVersion,
            filter:={({"zip", "zip.disabled"}, "资源包或光影")}).FirstOrDefault()
        If String.IsNullOrEmpty(FilePath) Then Return
        Try
            Dim Result = ResourcePackService.Toggle(FilePath)
            Hint("已切换资源包状态：" & IO.Path.GetFileName(Result), HintType.Green)
        Catch ex As Exception
            Hint("切换资源包失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnResourceOpen_Click(sender As Object, e As EventArgs) Handles BtnResourceOpen.Click
        Dim Instance = AdvancedInstance()
        If Instance Is Nothing Then Return
        Try
            OpenExplorer(Instance.PathVersion & "resourcepacks")
        Catch ex As Exception
            Hint("打开资源包目录失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnSpeedTest_Click(sender As Object, e As EventArgs) Handles BtnSpeedTest.Click
        If _AdvancedBusy Then
            Hint("实例高级操作正在进行中，请稍候……", Log:=False)
            Return
        End If
        _AdvancedBusy = True
        Hint("正在测试下载源……", Log:=False)
        RunInThread(
        Sub()
            Try
                Dim Results = New DownloadSourceSpeedTester().TestAllAsync(DownloadSourceSpeedTester.DefaultTargets()).Run()
                RunInUi(
                Sub()
                    _AdvancedBusy = False
                    MyMsgBox(DownloadSourceSpeedTester.Format(Results), "下载源测速")
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "测试下载源失败")
                RunInUi(Sub()
                            _AdvancedBusy = False
                            Hint("测试下载源失败：" & ex.Message, HintType.Red)
                        End Sub)
            End Try
        End Sub)
    End Sub

End Class
