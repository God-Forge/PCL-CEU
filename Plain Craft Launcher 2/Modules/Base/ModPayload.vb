Imports System.Diagnostics
Imports System.IO
Imports System.Text
Imports Newtonsoft.Json.Linq

Public Module ModPayload

    Private Const ToolsVersion As String = "2"
    Private ReadOnly EnsureLock As New Object

    ''' <summary>
    ''' 确保 Payload 编解码工具已释放到 ./runtime，并返回工具路径。
    ''' </summary>
    Public Function EnsureRuntimeTools() As (Encode As String, Decode As String)
        SyncLock EnsureLock
            Dim RuntimeFolder As String = PathUtils.AddSlashSuffix(Paths.Base) & "runtime\"
            DirectoryUtils.Create(RuntimeFolder)
            Dim EncodeExe As String = RuntimeFolder & "encode.exe"
            Dim DecodeExe As String = RuntimeFolder & "decode.exe"
            Dim VersionFile As String = RuntimeFolder & "payload-tools.version"
            If Not FileUtils.Exists(EncodeExe) OrElse Not FileUtils.Exists(DecodeExe) OrElse
               Not FileUtils.Exists(VersionFile) OrElse FileUtils.ReadAsString(VersionFile).Trim() <> ToolsVersion Then
                ExtractResources(EncodeExe, "PayloadEncode")
                ExtractResources(DecodeExe, "PayloadDecode")
                FileUtils.Write(VersionFile, ToolsVersion)
                Logger.Info("已释放 Payload 编解码工具到 runtime")
            End If
            Return (EncodeExe, DecodeExe)
        End SyncLock
    End Function

    ''' <summary>
    ''' 将指定文件夹打包为 .payload。
    ''' </summary>
    Public Function Encode(InputFolder As String, OutputFile As String, Optional Name As String = Nothing) As String
        If Not DirectoryUtils.Exists(InputFolder) Then Throw New Exception("输入文件夹不存在：" & InputFolder)
        Dim Tools = EnsureRuntimeTools()
        Dim Arguments As String = Quote(InputFolder.TrimEnd("\"c)) & " " & Quote(OutputFile)
        If Not String.IsNullOrWhiteSpace(Name) Then
            Arguments &= " --name " & Quote(Name.Replace("""", ""))
        End If
        Return RunTool(Tools.Encode, Arguments, "打包")
    End Function

    ''' <summary>
    ''' 将 .payload 解包到指定文件夹。
    ''' </summary>
    Public Function Decode(InputFile As String, OutputFolder As String) As String
        If Not FileUtils.Exists(InputFile) Then Throw New Exception("Payload 文件不存在：" & InputFile)
        Dim Tools = EnsureRuntimeTools()
        DirectoryUtils.Create(OutputFolder)
        Return RunTool(Tools.Decode, Quote(InputFile) & " " & Quote(OutputFolder.TrimEnd("\"c)), "解包")
    End Function

    ''' <summary>
    ''' 将 .payload 解包并导入为 versions 下的实例。
    ''' </summary>
    Public Function Import(PayloadFile As String, McFolder As String, Optional Name As String = Nothing) As String
        If Not FileUtils.Exists(PayloadFile) Then Throw New Exception("Payload 文件不存在：" & PayloadFile)
        Dim Tools = EnsureRuntimeTools()
        Dim Temp As String = PathTemp & "PayloadImport\" & Guid.NewGuid().ToString("N") & "\"
        Dim Target As String = Nothing
        Try
            DirectoryUtils.Create(Temp)
            RunTool(Tools.Decode, Quote(PayloadFile) & " " & Quote(Temp.TrimEnd("\"c)), "解包")

            Dim JsonFile As String = Nothing
            Dim Json As JObject = Nothing
            For Each Candidate In DirectoryUtils.EnumerateFiles(Temp, includeSubDirectories:=False, searchPattern:="*.json")
                Try
                    Dim Current = TryCast(FileUtils.ReadAsJson(Candidate), JObject)
                    If Current IsNot Nothing AndAlso Current("id") IsNot Nothing Then
                        JsonFile = Candidate
                        Json = Current
                        Exit For
                    End If
                Catch
                End Try
            Next
            If JsonFile Is Nothing Then Throw New Exception("Payload 中没有找到有效的版本 JSON。")

            Dim Id As String = If(Json?("id")?.ToString(), "")
            Dim InstanceName As String = If(String.IsNullOrWhiteSpace(Name), If(Not String.IsNullOrWhiteSpace(Id), Id, PathUtils.GetFileNameWithoutExtension(PayloadFile)), Name).Trim()
            If String.IsNullOrWhiteSpace(InstanceName) OrElse InstanceName = "." OrElse InstanceName = ".." OrElse InstanceName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 Then
                Throw New Exception("实例名无效：" & InstanceName)
            End If

            Target = PathUtils.AddSlashSuffix(McFolder) & "versions\" & InstanceName & "\"
            If DirectoryUtils.Exists(Target) Then Throw New Exception("实例已存在：" & InstanceName)
            DirectoryUtils.Copy(Temp, Target)

            Dim OldJsonName As String = Path.GetFileName(JsonFile)
            Dim OldBase As String = PathUtils.GetFileNameWithoutExtension(JsonFile)
            If Not String.Equals(OldJsonName, InstanceName & ".json", StringComparison.OrdinalIgnoreCase) Then
                Dim OldJson As String = Target & OldJsonName
                If FileUtils.Exists(OldJson) Then FileUtils.Move(OldJson, Target & InstanceName & ".json")
            End If
            If Not String.Equals(OldBase, InstanceName, StringComparison.OrdinalIgnoreCase) Then
                Dim OldJar As String = Target & OldBase & ".jar"
                If FileUtils.Exists(OldJar) Then FileUtils.Move(OldJar, Target & InstanceName & ".jar")
            End If

            McInstanceListForceRefresh = True
            Return $"已导入实例：{InstanceName}"
        Catch
            If Target IsNot Nothing Then
                Try
                    If DirectoryUtils.Exists(Target) Then DirectoryUtils.Delete(Target)
                Catch
                End Try
            End If
            Throw
        Finally
            Try
                If DirectoryUtils.Exists(Temp) Then DirectoryUtils.Delete(Temp)
            Catch
            End Try
        End Try
    End Function

    Private Function RunTool(ExePath As String, Arguments As String, Action As String) As String
        Dim Info As New ProcessStartInfo(ExePath) With {
            .Arguments = Arguments,
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .StandardOutputEncoding = Encoding.UTF8,
            .StandardErrorEncoding = Encoding.UTF8
        }
        Using Proc As Process = StartProcess(Info)
            Dim StdOut As String = Proc.StandardOutput.ReadToEnd()
            Dim StdErr As String = Proc.StandardError.ReadToEnd()
            Proc.WaitForExit()
            Dim Output As String = (StdOut & vbCrLf & StdErr).Trim()
            If Proc.ExitCode <> 0 Then
                Throw New Exception($"{Action}失败（退出码 {Proc.ExitCode}）：{Output}")
            End If
            Return Output
        End Using
    End Function

    Private Function Quote(Value As String) As String
        Return """" & Value.Replace("""", "") & """"
    End Function

End Module
