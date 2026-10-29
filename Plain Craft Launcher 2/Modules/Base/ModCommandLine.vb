Imports System.Diagnostics
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows
Imports PCL.Core.Tools

''' <summary>
''' PCL 命令行模式。仅在启动参数以 -- 开头时启用。
''' </summary>
Public Module ModCommandLine

    Private Declare Function AttachConsole Lib "kernel32" (dwProcessId As Integer) As Boolean
    Private Declare Function AllocConsole Lib "kernel32" () As Boolean

    Private _ConsoleInitialized As Boolean
    Private _OwnConsole As Boolean

    Public PendingLaunchInstance As String = Nothing
    Public PendingDownloadVersion As String = Nothing
    Public PendingDownloadLoader As String = Nothing

    Public Function HasPendingAction() As Boolean
        Return PendingLaunchInstance IsNot Nothing OrElse PendingDownloadVersion IsNot Nothing
    End Function

    ''' <summary>
    ''' 在主窗口加载完成后执行 --launch / --download。
    ''' </summary>
    Public Sub ExecutePendingAction()
        If PendingLaunchInstance IsNot Nothing Then
            ExecuteLaunch()
        ElseIf PendingDownloadVersion IsNot Nothing Then
            ExecuteDownload()
        End If
    End Sub

    Private Sub ExecuteLaunch()
        Dim InstanceName As String = PendingLaunchInstance
        PendingLaunchInstance = Nothing
        Try
            FrmMain.Visibility = Visibility.Hidden
            FrmMain.ShowInTaskbar = False

            Dim Folder As String = ModCommandLineDownload.EnsureMcFolder() & "versions\" & InstanceName.TrimEnd("\"c)
            If Not DirectoryUtils.Exists(Folder) Then Throw New Exception("versions 下不存在实例：" & InstanceName)

            Dim Instance As McInstance = New McInstance(Folder).Load()
            If Instance Is Nothing OrElse Instance.State = McInstance.McInstanceState.Error Then
                Throw New Exception("无法加载实例：" & InstanceName)
            End If

            McInstanceSelected = Instance
            If Not McLaunchStart(New McLaunchOptions With {.Instance = Instance}) Then
                Throw New Exception("启动未开始，请查看 PCL 窗口。")
            End If
            WriteLine("已开始启动：" & InstanceName)
        Catch ex As Exception
            Logger.Error(ex, "命令行启动失败")
            FrmMain.Visibility = Visibility.Visible
            FrmMain.ShowInTaskbar = True
            WriteLine("启动失败：" & ex.Message)
            Environment.Exit(1)
        End Try
    End Sub

    Private Sub ExecuteDownload()
        Dim Version As String = PendingDownloadVersion
        Dim Loader As String = PendingDownloadLoader
        PendingDownloadVersion = Nothing
        PendingDownloadLoader = Nothing

        FrmMain.Visibility = Visibility.Hidden
        FrmMain.ShowInTaskbar = False
        WriteLine($"开始下载 Minecraft {Version}（{If(String.IsNullOrEmpty(Loader), "原版", Loader)}）……")

        RunInThread(
        Sub()
            Try
                Dim Message As String = ModCommandLineDownload.Download(Version, Loader)
                RunInUi(
                Sub()
                    WriteLine(Message)
                    Environment.Exit(0)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "命令行下载失败")
                RunInUi(
                Sub()
                    WriteLine("下载失败：" & ex.Message)
                    Environment.Exit(1)
                End Sub)
            End Try
        End Sub)
    End Sub

    ''' <summary>
    ''' 执行命令行命令。返回 -1 表示不是命令行调用，应继续正常启动。
    ''' </summary>
    Public Function Run(Args As String()) As Integer
        If Args.Length = 0 Then Return -1
        Dim Command As String = Args(0).ToLowerInvariant()
        Select Case Command
            Case "--help", "-h", "/?"
                InitConsole(True)
                WriteHelp()
                WaitForExit()
                Return 0

            Case "--version", "-v"
                InitConsole(True)
                Console.WriteLine("Plain Craft Launcher (PCL) CEU 修改版")
                Console.WriteLine($"版本：{VersionDisplay}")
                Console.WriteLine($"版本代码：{VersionCode}")
                Console.WriteLine($"程序集版本：{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}")
                WaitForExit()
                Return 0

            Case "--luck", "--jrrp"
                InitConsole(True)
                Dim Result = DailyLuck.GetToday()
                Console.WriteLine($"今日人品：{Result.Value}")
                Console.WriteLine(Result.Comment)
                WaitForExit()
                Return 0

            Case "--mca", "--mca-selector"
                InitConsole(False)
                Try
                    Console.WriteLine("正在准备内置 MCA Selector……")
                    Dim ExePath As String = PageOtherTest.PrepareMcASelector(False)
                    StartProcess(New ProcessStartInfo With {
                        .FileName = ExePath,
                        .WorkingDirectory = Path.GetDirectoryName(ExePath),
                        .UseShellExecute = True
                    })
                    Console.WriteLine("MCA Selector 已启动。")
                    Return 0
                Catch ex As Exception
                    Console.WriteLine("启动 MCA Selector 失败：" & ex.Message)
                    Return 1
                End Try

            Case "--payload-encode"
                InitConsole(True)
                If Args.Length < 3 Then
                    Console.WriteLine("用法：--payload-encode <版本文件夹> <输出.payload> [版本名]")
                    WaitForExit()
                    Return 1
                End If
                Try
                    Console.WriteLine(ModPayload.Encode(Args(1), Args(2), If(Args.Length >= 4, Args(3), Nothing)))
                    WaitForExit()
                    Return 0
                Catch ex As Exception
                    Console.WriteLine("打包 Payload 失败：" & ex.Message)
                    WaitForExit()
                    Return 1
                End Try

            Case "--payload-decode"
                InitConsole(True)
                If Args.Length < 3 Then
                    Console.WriteLine("用法：--payload-decode <输入.payload> <输出文件夹>")
                    WaitForExit()
                    Return 1
                End If
                Try
                    Console.WriteLine(ModPayload.Decode(Args(1), Args(2)))
                    WaitForExit()
                    Return 0
                Catch ex As Exception
                    Console.WriteLine("解包 Payload 失败：" & ex.Message)
                    WaitForExit()
                    Return 1
                End Try

            Case Else
                If Command.StartsWith("--") Then
                    InitConsole(True)
                    Console.WriteLine($"未知命令：{Args(0)}")
                    Console.WriteLine()
                    WriteHelp()
                    WaitForExit()
                    Return 1
                End If
                Return -1
        End Select
    End Function

    ''' <summary>
    ''' 尝试附加到父进程控制台；没有控制台时可按需创建。
    ''' </summary>
    Public Sub InitConsole(AllocateIfMissing As Boolean)
        If _ConsoleInitialized Then Return
        If AttachConsole(-1) Then
            RebindConsole()
            _ConsoleInitialized = True
            Return
        End If
        If AllocateIfMissing AndAlso AllocConsole() Then
            _OwnConsole = True
            RebindConsole()
            _ConsoleInitialized = True
        End If
    End Sub

    Public Sub WriteLine(Text As String)
        Console.WriteLine(Text)
    End Sub

    Private Sub RebindConsole()
        Try
            Console.SetOut(New StreamWriter(Console.OpenStandardOutput(), New UTF8Encoding(False)) With {.AutoFlush = True})
            Console.SetError(New StreamWriter(Console.OpenStandardError(), New UTF8Encoding(False)) With {.AutoFlush = True})
            Console.OutputEncoding = New UTF8Encoding(False)
        Catch
        End Try
    End Sub

    Private Sub WaitForExit()
        If Not _OwnConsole Then Return
        Console.WriteLine()
        Console.WriteLine("按任意键退出……")
        Try
            Console.ReadKey(True)
        Catch
        End Try
    End Sub

    Private Sub WriteHelp()
        Console.WriteLine("Plain Craft Launcher (PCL) CEU 修改版 命令行模式")
        Console.WriteLine()
        Console.WriteLine("用法：")
        Console.WriteLine("  PCL2-<版本>ceu.exe --<命令>")
        Console.WriteLine()
        Console.WriteLine("命令：")
        Console.WriteLine("  --help, -h             显示此帮助")
        Console.WriteLine("  --version, -v          显示版本信息")
        Console.WriteLine("  --luck, --jrrp         输出今日人品")
        Console.WriteLine("  --mca, --mca-selector  启动内置 MCA Selector 区块编辑器")
        Console.WriteLine("  --payload-encode <文件夹> <输出.payload> [版本名]  打包版本为 Payload")
        Console.WriteLine("  --payload-decode <输入.payload> <输出文件夹>      解包 Payload")
        Console.WriteLine("  --launch <实例>        启动 versions 下指定文件夹名的实例")
        Console.WriteLine("  --download <MC版本> [-forge|-neoforge|-fabric]  下载原版并安装加载器")
        Console.WriteLine("  --memory               执行内存优化；退出码为释放的 KB（0 表示无明显变化）")
        Console.WriteLine("  --update ...           PCL 内部更新命令（高级）")
        Console.WriteLine("  --gpu <java.exe>       PCL 内部显卡设置命令（高级）")
        Console.WriteLine()
        Console.WriteLine("示例：")
        Console.WriteLine("  PCL2-3.6.0ceu.exe --help")
        Console.WriteLine("  PCL2-3.6.0ceu.exe --luck")
        Console.WriteLine("  PCL2-3.6.0ceu.exe --mca")
        Console.WriteLine("  PCL2-3.6.0ceu.exe --payload-encode .\\versions\\1.20.1 .\\1.20.1.payload")
        Console.WriteLine("  PCL2-3.6.0ceu.exe --payload-decode .\\1.20.1.payload .\\decoded")
        Console.WriteLine("  PCL2-3.6.0ceu.exe --launch 1.20.1")
        Console.WriteLine("  PCL2-3.6.0ceu.exe --download 1.20.1 -forge")
        Console.WriteLine("  PCL2-3.6.0ceu.exe --memory")
    End Sub

End Module
