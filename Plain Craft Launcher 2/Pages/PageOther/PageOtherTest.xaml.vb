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
    Public Shared Sub Jrrp()
        Hint("为便于维护，开源内容中不包含百宝箱功能……")
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
