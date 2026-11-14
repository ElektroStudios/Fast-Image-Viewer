Option Strict On
Option Explicit On
Option Infer Off

Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Forms

Imports Microsoft.VisualBasic.FileIO
Imports Microsoft.Win32

Imports Ookii.Dialogs.WinForms


' Contains the form's files coordination.
Partial Public NotInheritable Class Form1

    Private Sub LoadSingleImageFile(filePath As String)
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return

        Dim parentFolder As String = Path.GetDirectoryName(filePath)
        If String.IsNullOrWhiteSpace(parentFolder) OrElse Not Directory.Exists(parentFolder) Then Return

        Me.StopWorkerAndClearCache()
        Me.FolderPath = parentFolder

        ' Perform native folder scan using the current folder scanning mechanism
        Dim allFiles As String() = Directory.GetFiles(Me.FolderPath)
        Dim imageList As New List(Of String)()

        For Each fileItem As String In allFiles
            If ImageFileCatalog.IsSupported(fileItem) Then imageList.Add(fileItem)
#If DEBUG Then
            Thread.CurrentThread.Join(0) ' Prevents ContextSwitchDeadlock on long-running loops
#End If
        Next

        ' Sort consistent with the Natural Sort Order routine
        imageList.Sort(New Comparison(Of String)(Function(x As String, y As String) StrCmpLogicalW(x, y)))
        Me._imageFiles = imageList.ToArray()

        ' Match current file position index
        Dim targetIndex As Integer = -1
        For i As Integer = 0 To Me._imageFiles.Length - 1
            If Me._imageFiles(i).Equals(filePath, StringComparison.OrdinalIgnoreCase) Then
                targetIndex = i
                Exit For
            End If
        Next

        Me._currentIndex = If(targetIndex <> -1, targetIndex, 0)

        Me._lastWorkerCenter = -1
        Me.StartWorker()
        Me.DisplayCurrentImage()
        Me.UpdateNavigationButtons()
    End Sub

    Private Sub LoadDirectory(Optional anchorFilePath As String = Nothing)
        If String.IsNullOrWhiteSpace(Me.FolderPath) Then
            Me.WindowState = FormWindowState.Normal
            Exit Sub
        End If

        Dim extendedFolder As String = PathHelper.GetExtendedPath(Me.FolderPath)
        If Not Directory.Exists(extendedFolder) Then
            MessageBox.Show($"Directory not found:{Environment.NewLine}{Me.FolderPath}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Dim allFiles As String() = Directory.GetFiles(extendedFolder)

        Dim imageList As New List(Of String)()

        For Each filePath As String In allFiles
            Dim cleanPath As String = PathHelper.GetNormalPath(filePath)
            If ImageFileCatalog.IsSupported(cleanPath) Then imageList.Add(cleanPath)
#If DEBUG Then
            Thread.CurrentThread.Join(0) ' Prevents ContextSwitchDeadlock on long-running loops
#End If
        Next

        imageList.Sort(New Comparison(Of String)(Function(x As String, y As String) StrCmpLogicalW(x, y)))

        Dim newFiles As String() = imageList.ToArray()

        If newFiles.Length = 0 Then
            SyncLock Me._indexLock
                Me._imageFiles = newFiles
                Me._fileIndexMap = Nothing
                Me._currentIndex = 0
                Me._lastWorkerCenter = -1
            End SyncLock

            Me.PicBox.Image = Nothing
            Me.lblInfo.Text = "No images found in directory."
            Me.UpdateNavigationButtons()
            Return
        End If

        Dim newIndex As Integer = 0
        If Not String.IsNullOrWhiteSpace(anchorFilePath) Then
            newIndex = ImageFileCatalog.GetInsertionIndex(newFiles, anchorFilePath, Comparer(Of String).Create(AddressOf StrCmpLogicalW))
        End If

        Dim newIndexMap As Dictionary(Of String, Integer) = ImageFileCatalog.CreateIndex(newFiles)

        SyncLock Me._indexLock
            Me._imageFiles = newFiles
            Me._fileIndexMap = newIndexMap
            Me._currentIndex = newIndex
            Me._lastWorkerCenter = -1
        End SyncLock

        Me.StartWorker()
        Me.DisplayCurrentImage()
        Me.UpdateNavigationButtons()
    End Sub

    Private Sub DeleteCurrentImage(permanentDeletion As Boolean)
        If Me._imageFiles Is Nothing OrElse Me._imageFiles.Length = 0 Then
            Return
        End If

        Dim deletedIndex As Integer
        Dim deletedFilePath As String

        SyncLock Me._indexLock
            If Me._currentIndex < 0 OrElse Me._currentIndex >= Me._imageFiles.Length Then
                Return
            End If

            deletedIndex = Me._currentIndex
            deletedFilePath = Me._imageFiles(deletedIndex)
        End SyncLock

        Dim deletionOption As RecycleOption = If(permanentDeletion,
                                             RecycleOption.DeletePermanently,
                                             RecycleOption.SendToRecycleBin)

        Try
            FileSystem.DeleteFile(deletedFilePath, UIOption.AllDialogs, deletionOption, UICancelOption.DoNothing)
        Catch ex As Exception
            Dim errorType As String = If(Not My.Settings.DelKeyMovesImageToCustomFolder, "delete", "move")
            MessageBox.Show($"Could not {errorType} file:{Environment.NewLine}{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try

        Me.RefreshAfterSourceFileRemoval(deletedFilePath)
    End Sub

    Private Sub MoveCurrentImageToCustomDir()
        If Me._imageFiles Is Nothing OrElse Me._imageFiles.Length = 0 Then
            Return
        End If

        Dim currentIndex As Integer
        Dim filePath As String
        Dim fileName As String

        SyncLock Me._indexLock
            If Me._currentIndex < 0 OrElse Me._currentIndex >= Me._imageFiles.Length Then
                Return
            End If

            currentIndex = Me._currentIndex
            filePath = Me._imageFiles(Me._currentIndex)
            fileName = Path.GetFileName(filePath)
        End SyncLock

        Dim messageText As String = $"Move image to custom folder?{Environment.NewLine}{Environment.NewLine}{fileName}"
        Dim messageTitle As String = "Move image"
        Dim messageIcon As MessageBoxIcon = MessageBoxIcon.Exclamation

        Dim result As DialogResult =
            MessageBox.Show(messageText, messageTitle, MessageBoxButtons.OKCancel, messageIcon, MessageBoxDefaultButton.Button1)

        If result <> DialogResult.OK Then
            Exit Sub
        End If

        Try
            Dim parentDirectory As DirectoryInfo = Directory.GetParent(filePath)
            If parentDirectory Is Nothing Then
                Throw New IOException($"Could not retrieve the parent directory of file path: {filePath}")
            End If

            If String.IsNullOrWhiteSpace(My.Settings.CustomFolderPath) Then
                My.Settings.CustomFolderPath = ".\Moved Images"
            End If

            Dim destinationPath As String = $"{My.Settings.CustomFolderPath}\{fileName}"

            Directory.CreateDirectory(My.Settings.CustomFolderPath)
            FileSystem.MoveFile(filePath, destinationPath, overwrite:=False)
        Catch ex As Exception
            MessageBox.Show($"Could not move file:{Environment.NewLine}{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End Try

        Me.RefreshAfterSourceFileRemoval(filePath)
    End Sub

    Private Sub RefreshAfterSourceFileRemoval(removedFilePath As String)

        Dim removedImage As CachedImage = Nothing
        If Me._cache.TryRemove(removedFilePath, removedImage) Then
            removedImage.Bitmap?.Dispose()
        End If

        Me.PicBox.Image = Nothing

        Me.LoadDirectory(removedFilePath)

        SyncLock Me._indexLock
            Me._cacheWindowVersion += 1

            If Me._currentIndex >= Me._imageFiles.Length Then
                Me._currentIndex = Math.Max(0, Me._imageFiles.Length - 1)
            End If
        End SyncLock

        Me.DisplayCurrentImage()
    End Sub

    Private Sub EnsureFileIndexMap(files As String())
        If files Is Nothing OrElse files.Length = 0 Then
            SyncLock Me._indexLock
                Me._fileIndexMap = Nothing
                Me._fileIndexMapSource = Nothing
            End SyncLock
            Exit Sub
        End If

        SyncLock Me._indexLock
            If Me._fileIndexMap IsNot Nothing AndAlso Object.ReferenceEquals(Me._fileIndexMapSource, files) Then
                Exit Sub
            End If

            Dim map As Dictionary(Of String, Integer) = ImageFileCatalog.CreateIndex(files)

            Me._fileIndexMap = map
            Me._fileIndexMapSource = files
        End SyncLock
    End Sub
End Class
