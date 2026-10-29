Imports System.IO
Imports MeloongCore
Imports PCL.Core.Memory

''' <summary>
''' 按需提权的内部 helper。只接受固定的任务名，不接收任意命令。
''' </summary>
Public Module ModElevatedHelper

    ''' <summary>
    ''' 尝试执行提权 helper 任务。返回 -1 表示不是 helper 调用。
    ''' </summary>
    Public Function TryRun(Args As String()) As Integer
        If Args.Length = 0 OrElse Args(0).ToLowerInvariant() <> "--elevated" Then Return -1
        If Args.Length < 2 Then Return -2

        Select Case Args(1).ToLowerInvariant()
            Case "memory"
                Return RunMemory(Function() MemoryOptimizer.OptimizeAll(True))
            Case "memory:working-sets"
                Return RunMemory(Function() MemoryOptimizer.Optimize(MemoryOptimizationKind.WorkingSets))
            Case "memory:system-working-set"
                Return RunMemory(Function() MemoryOptimizer.Optimize(MemoryOptimizationKind.SystemWorkingSet))
            Case "memory:modified-page-list"
                Return RunMemory(Function() MemoryOptimizer.Optimize(MemoryOptimizationKind.ModifiedPageList))
            Case "memory:standby-list"
                Return RunMemory(Function() MemoryOptimizer.Optimize(MemoryOptimizationKind.StandbyList))
            Case "memory:priority0-standby-list"
                Return RunMemory(Function() MemoryOptimizer.Optimize(MemoryOptimizationKind.Priority0StandbyList))
            Case "gpu"
                Return SetGpuPreference(Args)
            Case Else
                Return -2
        End Select
    End Function

    Private Function RunMemory(Operation As Func(Of MemoryOptimizationResult)) As Integer
        Try
            Dim Result = Operation()
            If Not Result.Success Then Return -1
            Dim FreedKb As Long = Result.FreedBytes \ 1024
            Return CInt(Math.Min(FreedKb, Integer.MaxValue))
        Catch ex As Exception
            Logger.Error(ex, "提权内存优化失败")
            Return -1
        End Try
    End Function

    Private Function SetGpuPreference(Args As String()) As Integer
        If Args.Length <> 3 Then Return -2

        Dim JavaExe As String = Args(2).Trim("""")
        If Not File.Exists(JavaExe) OrElse Not Path.GetExtension(JavaExe).Equals(".exe", StringComparison.OrdinalIgnoreCase) Then
            Return -2
        End If

        Try
            Const RegKey As String = "HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences"
            RegistryUtils.Write(RegKey, JavaExe, "GpuPreference=2;")
            Logger.Info("提权 helper 已调整显卡设置：" & JavaExe)
            Return 0
        Catch ex As Exception
            Logger.Error(ex, "提权 helper 调整显卡设置失败")
            Return -1
        End Try
    End Function

End Module
