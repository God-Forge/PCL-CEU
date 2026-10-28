Imports System.Diagnostics

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
    ''' 解压内置的 MCA Selector（首次启动）并打开它。
    ''' </summary>
    Public Shared Sub LaunchMcASelector()
        RunInThread(
        Sub()
            Try
                Dim ZipPath As String = PathPure.Value & "Tools\McASelector.zip"
                Dim VersionPath As String = ToolFolder & "version.txt"
                If Not FileUtils.Exists(ToolExe) OrElse Not FileUtils.Exists(ToolFolder & "runtime\bin\java.exe") OrElse Not FileUtils.Exists(ToolFolder & "app\mcaselector-2.9-all.jar") OrElse Not FileUtils.Exists(VersionPath) OrElse FileUtils.ReadAsString(VersionPath).Trim() <> McASelectorVersion Then
                    Hint("正在解压内置的 MCA Selector，首次启动可能需要一会儿……", Log:=False)
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
                StartProcess(New ProcessStartInfo With {
                    .FileName = ToolExe,
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
        Dim UnixDay As Long = New DateTimeOffset(Date.Now.Date).ToUnixTimeSeconds() \ 86400
        Dim Seed As Integer = CInt(UnixDay Mod Integer.MaxValue)
        Dim Value As Integer = New Random(Seed).Next(0, 101)
        Dim Comment As String
        Select Case Value
            Case 0 To 9
                Comment = "今天运气不太行，出门小心……"
            Case 10 To 29
                Comment = "普普通通的一天。"
            Case 30 To 59
                Comment = "还不错，保持好心情！"
            Case 60 To 89
                Comment = "运气不错，适合做点大事！"
            Case Else
                Comment = "欧皇附体！今天想做什么都可以！"
        End Select
        MyMsgBox("今日人品：" & Value & vbCrLf & vbCrLf & Comment, "今日人品", IsWarn:=Value < 10, IsWarnText:=Value < 10)
    End Sub
    Public Shared Sub RubbishClear()
        Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        If ShowHint Then Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        If ShowHint Then Hint("为便于维护，开源内容中不包含百宝箱功能……")
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
