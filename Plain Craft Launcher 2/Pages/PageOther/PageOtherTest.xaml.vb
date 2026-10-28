Imports System.Diagnostics
Imports PCL.Core.Memory
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
        If MyMsgBox("将以管理员权限启动一个新的 PCL 进程执行完整内存优化（包含待机列表）。是否继续？", "内存优化", "继续", "取消") <> 1 Then Return
        RunInThread(
        Sub()
            Try
                Dim Code As Integer = RunAsAdmin("--memory")
                RunInUi(
                Sub()
                    RefreshMemoryStatus()
                    Hint("管理员内存优化已完成（返回码 " & Code & "）。", HintType.Green, Log:=False)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "以管理员权限执行内存优化失败")
                RunInUi(Sub() Hint("以管理员权限执行内存优化失败：" & ex.Message, HintType.Red))
            End Try
        End Sub)
    End Sub

    Private Sub RunMemoryOperation(Kind As MemoryOptimizationKind, Optional ConfirmText As String = Nothing)
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
                    LabMemoryAdmin.Text = "当前权限：普通用户，系统级清理需要管理员权限。"
                    BtnMemoryAdmin.Visibility = Visibility.Visible
                End If
            End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "读取内存状态失败")
            RunInUi(Sub() LabMemoryStatus.Text = "当前可用物理内存：读取失败")
        End Try
    End Sub

    Private Sub BtnLaunch_Click(sender As Object, e As EventArgs) Handles BtnLaunch.Click
        LaunchMcASelector()
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

End Class
