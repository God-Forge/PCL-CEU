Imports PCL.Core.Worlds

''' <summary>
''' 世界/存档备份与恢复。
''' </summary>
Public Module ModWorldBackup

    Public ReadOnly Property BackupRoot As String
        Get
            Dim Root As String = PathPure.Value & "Backups\Worlds\"
            DirectoryUtils.Create(Root)
            Return Root
        End Get
    End Property

    Public Function BackupSaves(Instance As McInstance) As WorldBackupInfo
        Dim Saves As String = PathUtils.AddSlashSuffix(Instance.PathIndie) & "saves\"
        If Not DirectoryUtils.Exists(Saves) Then Throw New Exception("当前实例没有 saves 文件夹。")
        Dim Service As New WorldBackupService()
        Dim Info = Service.CreateBackup(Saves, BackupRoot, Instance.Name, "saves", WorldBackupKind.Saves)
        Service.Prune(BackupRoot, Instance.Name, 10)
        Return Info
    End Function

    Public Function ListSavesBackups(Instance As McInstance) As IReadOnlyList(Of WorldBackupInfo)
        Return New WorldBackupService().ListBackups(BackupRoot, Instance.Name).
            Where(Function(Item) Item.Kind = WorldBackupKind.Saves).
            ToList()
    End Function

    Public Function RestoreLatestSaves(Instance As McInstance) As WorldBackupInfo
        Dim Latest = ListSavesBackups(Instance).FirstOrDefault()
        If Latest Is Nothing Then Throw New Exception("当前实例还没有存档备份。")
        Dim Target As String = PathUtils.AddSlashSuffix(Instance.PathIndie) & "saves\"
        Return New WorldBackupService().Restore(Latest.Path, Target, BackupRoot, Instance.Name)
    End Function

    Public Function FormatSize(Size As Long) As String
        If Size >= 1024L * 1024 * 1024 Then Return $"{Size / 1024 / 1024 / 1024:F2} GB"
        If Size >= 1024L * 1024 Then Return $"{Size / 1024 / 1024:F1} MB"
        If Size >= 1024L Then Return $"{Size / 1024:F1} KB"
        Return $"{Size} B"
    End Function

End Module
