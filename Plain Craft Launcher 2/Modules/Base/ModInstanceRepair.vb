Imports PCL.Core.Minecraft
Imports PCL.Core.Mods

''' <summary>
''' 实例体检与基础文件修复。
''' </summary>
Public Module ModInstanceRepair

    Public Function Check(Instance As McInstance) As InstanceCheckResult
        Return InstanceCheckService.Check(Instance.PathVersion, McFolderSelected)
    End Function

    Public Function CheckMods(Instance As McInstance) As ModScanResult
        Return ModScanner.Scan(PathUtils.AddSlashSuffix(Instance.PathIndie) & "mods\")
    End Function

    Public Function RepairBase(Instance As McInstance) As String
        Dim Loaders As List(Of LoaderBase) = DlClientFix(Instance, CheckAssetsHash:=True, DownloadAssetIndexInBackground:=False)
        If Loaders.Count = 0 Then Return "没有需要修复的基础文件。"
        Dim Combo As New LoaderCombo(Of String)("修复实例 " & Instance.Name, Loaders)
        Combo.WaitForExit()
        If Combo.State <> LoadState.Finished Then Throw New Exception("修复未完成（状态：" & Combo.State.ToString & "）")
        McInstanceListForceRefresh = True
        Return "基础文件修复完成。"
    End Function

    Public Function FormatCheck(Result As InstanceCheckResult) As String
        Dim Lines As New List(Of String)
        For Each Issue In Result.Issues
            Lines.Add($"[{SeverityText(Issue.Severity)}] {Issue.Message}")
        Next
        For Each Issue In Result.ModIssues
            Lines.Add($"[Mod/{SeverityText(Issue.Severity)}] {Issue.FileName}：{Issue.Message}")
        Next
        If Lines.Count = 0 Then Return "未发现明显问题。"
        Dim More As Integer = Lines.Count - 30
        Dim Text As String = String.Join(vbCrLf, Lines.Take(30))
        If More > 0 Then Text &= vbCrLf & $"... 另有 {More} 条问题，请查看日志。"
        Return Text
    End Function

    Public Function FormatModScan(Result As ModScanResult) As String
        If Result.Issues.Count = 0 Then Return $"共扫描 {Result.Mods.Count} 个 Mod，未发现明显问题。"
        Dim Lines = Result.Issues.Take(30).Select(Function(Issue) $"[{SeverityText(Issue.Severity)}] {Issue.FileName}：{Issue.Message}").ToList()
        Dim Text As String = $"共扫描 {Result.Mods.Count} 个 Mod。" & vbCrLf & String.Join(vbCrLf, Lines)
        If Result.Issues.Count > 30 Then Text &= vbCrLf & $"... 另有 {Result.Issues.Count - 30} 条问题。"
        Return Text
    End Function

    Private Function SeverityText(Severity As Object) As String
        Select Case Severity.ToString()
            Case "Error"
                Return "错误"
            Case "Warning"
                Return "警告"
            Case Else
                Return "信息"
        End Select
    End Function

End Module
