Imports System.Diagnostics
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports PCL.Core.Tools

''' <summary>
''' PCL 命令行模式。仅在启动参数以 -- 开头时启用。
''' </summary>
Public Module ModCommandLine

    Private Declare Function AttachConsole Lib "kernel32" (dwProcessId As Integer) As Boolean
    Private Declare Function AllocConsole Lib "kernel32" () As Boolean

    Private _ConsoleInitialized As Boolean
    Private _OwnConsole As Boolean

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
        Console.WriteLine("  --memory               执行内存优化；退出码为释放的 KB（0 表示无明显变化）")
        Console.WriteLine("  --update ...           PCL 内部更新命令（高级）")
        Console.WriteLine("  --gpu <java.exe>       PCL 内部显卡设置命令（高级）")
        Console.WriteLine()
        Console.WriteLine("示例：")
        Console.WriteLine("  PCL2-3.4.0ceu.exe --help")
        Console.WriteLine("  PCL2-3.4.0ceu.exe --luck")
        Console.WriteLine("  PCL2-3.4.0ceu.exe --mca")
        Console.WriteLine("  PCL2-3.4.0ceu.exe --memory")
    End Sub

End Module
