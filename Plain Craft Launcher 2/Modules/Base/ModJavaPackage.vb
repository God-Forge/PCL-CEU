Imports PCL.Core.Java

''' <summary>
''' .javapkg Java 运行时打包与导入。
''' </summary>
Public Module ModJavaPackage

    Public ReadOnly Property PackageRoot As String
        Get
            Dim Root As String = PathPure.Value & "JavaPackages\"
            DirectoryUtils.Create(Root)
            Return Root
        End Get
    End Property

    Public Function Detect(JavaExe As String) As JavaPackageInfo
        Return JavaPackageService.Detect(JavaExe)
    End Function

    Public Function GetDefaultFileName(Info As JavaPackageInfo) As String
        Return JavaPackageService.GetDefaultFileName(Info)
    End Function

    Public Function Pack(JavaExe As String, OutputFile As String) As JavaPackageInfo
        Return JavaPackageService.CreatePackage(JavaExe, OutputFile)
    End Function

    Public Function Info(PackageFile As String) As JavaPackageInfo
        Return JavaPackageService.ReadInfo(PackageFile)
    End Function

    Public Function Extract(PackageFile As String, TargetFolder As String) As JavaPackageInfo
        Return JavaPackageService.Extract(PackageFile, TargetFolder)
    End Function

    Public Function Import(PackageFile As String) As JavaPackageInfo
        Dim Info = JavaPackageService.ReadInfo(PackageFile)
        Dim Root As String = PathUtils.AddSlashSuffix(PathPure.Value & "Java")
        DirectoryUtils.Create(Root)
        Dim Name As String = Info.JavaVersion & "-" & Info.TypeText
        Dim Target As String = Root & Name
        If DirectoryUtils.Exists(Target) Then Target = Root & Name & "-" & Date.Now.ToString("yyyyMMddHHmmss")
        Dim Result = JavaPackageService.Extract(PackageFile, Target)
        If Not ModJava.AddJavaFolder(Target) Then
            Throw New Exception($"Java 已解压到 {Target}，但无法加入 Java 列表，请手动导入。")
        End If
        Return Result
    End Function

    Public Function FormatSize(Size As Long) As String
        If Size >= 1024L * 1024 * 1024 Then Return $"{Size / 1024 / 1024 / 1024:F2} GB"
        If Size >= 1024L * 1024 Then Return $"{Size / 1024 / 1024:F1} MB"
        If Size >= 1024L Then Return $"{Size / 1024:F1} KB"
        Return $"{Size} B"
    End Function

End Module
