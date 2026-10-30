Imports PCL.Core.Tasks

''' <summary>
''' 将现有 VB LoaderBase 适配为统一的 C# ITaskProgress，便于逐步迁移 ModLoader。
''' </summary>
Public Class LoaderProgressAdapter
    Implements ITaskControl, IDisposable

    Private ReadOnly _loader As LoaderBase
    Private _disposed As Boolean

    Public Sub New(Loader As LoaderBase)
        If Loader Is Nothing Then Throw New ArgumentNullException(NameOf(Loader))
        _loader = Loader
        AddHandler _loader.OnStateChangedThread, AddressOf OnLoaderStateChanged
        AddHandler _loader.ProgressChanged, AddressOf OnLoaderProgressChanged
    End Sub

    Public ReadOnly Property State As TaskState Implements ITaskProgress.State
        Get
            Return MapState(_loader.State)
        End Get
    End Property

    Public ReadOnly Property Progress As Double Implements ITaskProgress.Progress
        Get
            Return Math.Max(-1, Math.Min(1, _loader.Progress))
        End Get
    End Property

    Public ReadOnly Property [Error] As Exception Implements ITaskProgress.Error
        Get
            Return _loader.Error
        End Get
    End Property

    Public ReadOnly Property CancellationToken As CancellationToken Implements ITaskProgress.CancellationToken
        Get
            Return _loader.CreateCancellationToken()
        End Get
    End Property

    Public Event StateChanged As EventHandler(Of TaskStateChangedEventArgs) Implements ITaskProgress.StateChanged
    Public Event ProgressChanged As EventHandler(Of TaskProgressChangedEventArgs) Implements ITaskProgress.ProgressChanged

    Public Sub Start() Implements ITaskControl.Start
        _loader.Start()
    End Sub

    Public Sub Cancel() Implements ITaskControl.Cancel
        _loader.Cancel()
    End Sub

    Public Async Function WaitForExitAsync(Optional Ct As CancellationToken = Nothing) As Task Implements ITaskControl.WaitForExitAsync
        While State = TaskState.Loading
            Ct.ThrowIfCancellationRequested()
            Await Task.Delay(20, Ct)
        End While
        If State = TaskState.Failed AndAlso [Error] IsNot Nothing Then Throw [Error]
    End Function

    Private Sub OnLoaderStateChanged(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
        RaiseEvent StateChanged(Me, New TaskStateChangedEventArgs(MapState(OldState), MapState(NewState)))
    End Sub

    Private Sub OnLoaderProgressChanged(NewProgress As Double, OldProgress As Double)
        RaiseEvent ProgressChanged(Me, New TaskProgressChangedEventArgs(
            Math.Max(-1, Math.Min(1, OldProgress)),
            Math.Max(-1, Math.Min(1, NewProgress))))
    End Sub

    Public Shared Function MapState(State As LoadState) As TaskState
        Select Case State
            Case LoadState.Loading
                Return TaskState.Loading
            Case LoadState.Finished
                Return TaskState.Finished
            Case LoadState.Failed
                Return TaskState.Failed
            Case LoadState.Canceled
                Return TaskState.Canceled
            Case Else
                Return TaskState.Waiting
        End Select
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        RemoveHandler _loader.OnStateChangedThread, AddressOf OnLoaderStateChanged
        RemoveHandler _loader.ProgressChanged, AddressOf OnLoaderProgressChanged
    End Sub

End Class
