
#Region " Option Statements "

Option Strict On
Option Explicit On
Option Infer Off

#End Region

#Region " Imports "

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

#End Region

#Region " Form1 "

Public NotInheritable Class Form1 : Inherits Form

#Region " Fields "

    Private FolderPath As String

    Private _isZoomedMode As Boolean = False
    Private _currentZoomFactor As Double = 1.0
    Private _baseZoomFactor As Double = 1.0

    ' Evicted bitmaps from the worker that have not yet been disposed
    Private ReadOnly _pendingDisposal As New ConcurrentQueue(Of Bitmap)()

    Private _lastValidCustomFolderPath As String = String.Empty
    Private _isUpdatingCustomFolderTextBox As Boolean = False

    Private _fileIndexMap As Dictionary(Of String, Integer)
    Private _fileIndexMapSource As String()

    Dim iscurrentimagedisplayed As Boolean
    Private _isImageMissing As Boolean

    Private _screenWidth As Integer = SystemInformation.PrimaryMonitorSize.Width
    Private _screenHeight As Integer = SystemInformation.PrimaryMonitorSize.Height

    Private _renderedIndex As Integer = -1

    Private _rotationAngle As Integer = 0

    ' 0 = no hay BeginInvoke de progreso pendiente en la cola; 1 = hay uno.
    ' Evita que el WorkerLoop inunde la cola de mensajes con PostMessage durante
    ' la carga del radius cache, lo que retrasaría WM_TIMER y causaría saltos de imagen.
    Private _pendingProgressInvoke As Integer = 0

    '  CONTROLS
    ' ═══════════════════════════════════════════════════════════════
    Private WithEvents AboutBox As New AboutBox1

    Private WithEvents PicBox As PictureBox
    Private WithEvents BtnPrev As Button
    Private WithEvents BtnNext As Button
    Private lblInfo As Label
    Private pnlBottom As Panel
    Private progressBar As ProgressBar
    Private lblLoading As Label
    Private WithEvents PanScroll As Panel
    Private tblMain As TableLayoutPanel

    ' Menu strip — File menu and its items
    Private WithEvents MainMenuStrip1 As MenuStrip
    Private WithEvents MenuItemOpenFile As ToolStripMenuItem
    Private WithEvents MenuItemCopyFileTo As ToolStripMenuItem
    Private WithEvents MenuItemSaveAs As ToolStripMenuItem
    Private WithEvents MenuItemOpenDirectory As ToolStripMenuItem
    Private WithEvents MenuItemCloseCurrentDirectory As ToolStripMenuItem
    Private WithEvents MenuItemSendAllToRecycleBin As ToolStripMenuItem
    Private WithEvents MenuItemShowInExplorer As ToolStripMenuItem
    Private WithEvents MenuItemExit As ToolStripMenuItem

    ' Image Menu Items
    Private WithEvents MenuItemZoomIn As New ToolStripMenuItem("Zoom &In")
    Private WithEvents MenuItemZoomOut As New ToolStripMenuItem("Zoom &Out")
    Private WithEvents MenuItemResetZoom As New ToolStripMenuItem("Reset &Zoom")
    Private WithEvents MenuItemRotateLeft As New ToolStripMenuItem("Rotate &Left")
    Private WithEvents MenuItemRotateRight As New ToolStripMenuItem("Rotate &Right")
    Private WithEvents MenuItemResetOrientation As New ToolStripMenuItem("Reset &Orientation")
    Private WithEvents MenuItemSetWallpaper As ToolStripMenuItem
    Private WithEvents MenuItemWallpaperFill As ToolStripMenuItem
    Private WithEvents MenuItemWallpaperFit As ToolStripMenuItem
    Private WithEvents MenuItemWallpaperStretch As ToolStripMenuItem
    Private WithEvents MenuItemWallpaperTile As ToolStripMenuItem
    Private WithEvents MenuItemWallpaperCenter As ToolStripMenuItem
    Private WithEvents MenuItemWallpaperSpan As ToolStripMenuItem

    ' Options Menu Items
    Private WithEvents MenuItemShowCacheLabel As ToolStripMenuItem
    Private WithEvents MenuItemShowProgressBar As ToolStripMenuItem
    Private WithEvents MenuItemConfigureRadius As ToolStripMenuItem
    Private WithEvents MenuItemCacheMenu As ToolStripMenuItem
    Private WithEvents MenuItemAskForDirectoryPathAtStartup As ToolStripMenuItem
    Private WithEvents MenuItemDelKeyBehavior As ToolStripMenuItem
    Private WithEvents MenuItemCustomFolderName As ToolStripMenuItem
    Private WithEvents CustomFolderTextBox As ToolStripTextBox

    Private NumForwardHost As ToolStripControlHost
    Private NumBackHost As ToolStripControlHost
    Private NumForwardCtrl As NumericUpDown
    Private NumBackCtrl As NumericUpDown

    '  STATE — navigation
    ' ═══════════════════════════════════════════════════════════════
    Private _imageFiles As String() = Array.Empty(Of String)()
    Private _currentIndex As Integer = 0

    Private ReadOnly _cache As New ConcurrentDictionary(Of String, CachedImage)()
    Private _workerThread As Thread
    Private _cancelWorker As Boolean = False
    Private ReadOnly _wakeSignal As New AutoResetEvent(False)
    Private _lastWorkerCenter As Integer = -1
    Private _cacheWindowVersion As Integer = 0
    Private _lastWorkerCacheVersion As Integer = -1
    Private ReadOnly _indexLock As New Object()

    '  STATE — throttled navigation & async display
    ' ═══════════════════════════════════════════════════════════════

    ''' <summary>
    ''' Timer that coalesces rapid key presses / key-repeat events
    ''' into a single DisplayCurrentImage call.
    ''' </summary>
    Private WithEvents NavTimer As System.Windows.Forms.Timer

    ''' <summary>
    ''' Generation counter incremented every time we start a new
    ''' display request.  Stale background loads check this before
    ''' writing to the PictureBox so they never overwrite a newer image.
    ''' </summary>
    Private _displayGeneration As Integer = 0

    '  STATE — view modes
    ' ═══════════════════════════════════════════════════════════════
    Private _isFullscreen As Boolean = False
    Private _isActualSize As Boolean = False

    Private _savedBounds As Rectangle
    Private _savedWin32Style As Integer

    Private _actualBitmap As Bitmap = Nothing

    Private _dragActive As Boolean = False
    Private _dragStart As Point = Point.Empty
    Private _scrollStart As Point = Point.Empty

#End Region

#Region " Properties "

    ''' <summary>
    ''' Number of images to keep cached BEHIND the current position.
    ''' </summary>
    Private ReadOnly Property CACHE_RADIUS_BACK As Integer
        Get
            Return My.Settings.CacheRadiusBack
        End Get
    End Property

    ''' <summary>
    ''' Number of images to keep cached AHEAD of the current position.
    ''' </summary>
    Private ReadOnly Property CACHE_RADIUS_FORWARD As Integer
        Get
            Return My.Settings.CacheRadiusForward
        End Get
    End Property

    Private ReadOnly Property SHOW_CACHE_LABEL As Boolean
        Get
            Return My.Settings.ShowCacheLabel
        End Get
    End Property

    Private ReadOnly Property SHOW_PROGRESS_BAR As Boolean
        Get
            Return My.Settings.ShowProgressBar
        End Get
    End Property

    Private ReadOnly Property ASK_FOR_DIRECTORY_PATH_AT_STARTUP As Boolean
        Get
            Return My.Settings.AskForDirectoryPathAtStartup
        End Get
    End Property

    Private ReadOnly Property DEL_KEY_MOVES_IMAGE_TO_CUSTOM_FOLDER As Boolean
        Get
            Return My.Settings.DelKeyMovesImageToCustomFolder
        End Get
    End Property

    Private ReadOnly Property CUSTOM_FOLDER_NAME As String
        Get
            Return My.Settings.CustomFolderPath
        End Get
    End Property

#End Region

#Region " Constructor "

    Public Sub New()
        Me.InitializeComponent()
        Me.BuildUI()

        Me.Opacity = 0
    End Sub

#End Region

#Region " Event Invocators "

    Protected Overrides Sub OnLoad(e As System.EventArgs)
        MyBase.OnLoad(e)
        Me.WindowState = FormWindowState.Maximized
        Me.LoadDirectory()
    End Sub

    Private Sub OnWindowLoaded()
        If Me.SHOW_PROGRESS_BAR Then Me.progressBar.Visible = False
        If Me.SHOW_CACHE_LABEL Then Me.lblLoading.Visible = False
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        If Me._isActualSize Then
            MyBase.OnMouseWheel(e)
            Return
        End If
        Dim scrollingUp As Boolean = (e.Delta > 0)
        If MOUSE_WHEEL_INVERTED Then scrollingUp = Not scrollingUp
        If scrollingUp Then Me.NavigatePrev() Else Me.NavigateNext()
        MyBase.OnMouseWheel(e)
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        Me._cancelWorker = True
        Me._wakeSignal.Set()

        If Me.NavTimer IsNot Nothing Then
            Me.NavTimer.Stop()
            Me.NavTimer.Dispose()
        End If

        If Me._workerThread IsNot Nothing AndAlso Me._workerThread.IsAlive Then
            Me._workerThread.Join(1000)
        End If

        Me.PicBox.Image = Nothing ' release before draining

        If Me._actualBitmap IsNot Nothing Then
            Me._actualBitmap.Dispose()
            Me._actualBitmap = Nothing
        End If

        For Each kvp As KeyValuePair(Of String, CachedImage) In Me._cache
            kvp.Value.Bitmap?.Dispose()
        Next
        Me._cache.Clear()

        ' flush pending disposals
        Dim b As Bitmap = Nothing
        Do While Me._pendingDisposal.TryDequeue(b)
            b?.Dispose()
        Loop

        MyBase.OnFormClosing(e)
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)

        Select Case e.KeyCode

            Case Keys.PageDown
                Me.NavigateNext()
                e.Handled = True

            Case Keys.PageUp
                Me.NavigatePrev()
                e.Handled = True

            Case Keys.Home
                Me.NavigateFirst()
                e.Handled = True

            Case Keys.End
                Me.NavigateLast()
                e.Handled = True

            Case Keys.Escape
                If Me._isActualSize Then
                    Me.ExitActualSize()
                ElseIf Me._isFullscreen Then
                    Me.ExitFullscreen()
                End If
                e.Handled = True

            Case Keys.Back
                Me.NavigatePrev()
                e.Handled = True

            Case Keys.Delete
                Dim permanentDeletion As Boolean = e.Shift

                If permanentDeletion Then
                    Me.DeleteCurrentImage(permanentDeletion:=True)

                ElseIf My.Settings.DelKeyMovesImageToCustomFolder Then
                    Me.MoveCurrentImageToCustomDir()

                Else
                    Me.DeleteCurrentImage(permanentDeletion:=False)
                End If

                e.Handled = True

            Case Keys.L
                Me.RotateImage(False)

            Case Keys.R
                Me.RotateImage(True)

        End Select

        MyBase.OnKeyDown(e)
    End Sub

#End Region

#Region " Event Handlers "

#Region " Form "

    Private Sub Form1_HandleCreated(sender As Object, e As EventArgs) Handles MyBase.HandleCreated

        DwmSetWindowAttribute(Me.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, 1, Marshal.SizeOf(1))
    End Sub

    Private Sub Form1_Shown(sender As Object, e As EventArgs) Handles MyBase.Shown

        Me.BeginInvoke(Sub() Me.Opacity = 1)
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        Dim sourcePath As String =
            If(Environment.GetCommandLineArgs().Length > 1,
               Environment.GetCommandLineArgs()(1),
               If(My.Settings.AskForDirectoryPathAtStartup,
                  Interaction.InputBox(Prompt:="Enter a directory path:", Title:="Directory", DefaultResponse:=""),
                  ""))

        If Not String.IsNullOrWhiteSpace(sourcePath) Then
            If Directory.Exists(sourcePath) Then
                Me.FolderPath = sourcePath.TrimEnd("\"c)
            ElseIf File.Exists(sourcePath) Then
                Dim parentFolder As String = Path.GetDirectoryName(sourcePath)
                If String.IsNullOrWhiteSpace(parentFolder) Then
                    parentFolder = ".\"
                End If
                Me.FolderPath = parentFolder
                Me.BeginInvoke(Sub() Me.LoadSingleImageFile(sourcePath))
            Else
                MessageBox.Show(Me, $"The directory or file does not exist: {sourcePath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        End If

    End Sub

    Private Sub Form1_DragEnter(sender As Object, e As DragEventArgs) Handles MyBase.DragEnter

        If Not e.Data.GetDataPresent(DataFormats.FileDrop) Then
            e.Effect = DragDropEffects.None
            Return
        End If

        Dim paths As String() = CType(e.Data.GetData(DataFormats.FileDrop), String())

        If paths.Length <> 1 Then
            e.Effect = DragDropEffects.None
            Return
        End If

        Dim droppedPath As String = paths(0)

        If Directory.Exists(droppedPath) Then
            e.Effect = DragDropEffects.Copy
            Return
        End If

        If File.Exists(droppedPath) Then
            Dim fileExt As String = Path.GetExtension(droppedPath).ToLowerInvariant()
            For Each supportedExt As String In AppGlobals.SupportedExtensions
                If fileExt = supportedExt Then
                    e.Effect = DragDropEffects.Copy
                    Return
                End If
            Next
        End If

        e.Effect = DragDropEffects.None
    End Sub

    Private Sub Form1_DragDrop(sender As Object, e As DragEventArgs) Handles MyBase.DragDrop

        If Not e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Return
        End If

        Dim paths As String() = CType(e.Data.GetData(DataFormats.FileDrop), String())

        If paths.Length <> 1 Then
            Return
        End If

        Dim droppedPath As String = paths(0)

        If Directory.Exists(droppedPath) Then
            Me.StopWorkerAndClearCache()
            Me._imageFiles = Array.Empty(Of String)()
            Me.FolderPath = droppedPath
            Me._currentIndex = 0
            Me._lastWorkerCenter = -1
            Me.PicBox.Image = Nothing
            Me.lblInfo.Text = "0 / 0"
            Me.LoadDirectory()
        ElseIf File.Exists(droppedPath) Then
            Me.LoadSingleImageFile(droppedPath)
        End If
    End Sub

#End Region

#Region " Buttons "

    Private Sub BtnNext_Click(sender As Object, e As EventArgs) Handles BtnNext.Click
        Me.NavigateNext()
    End Sub

    Private Sub BtnPrev_Click(sender As Object, e As EventArgs) Handles BtnPrev.Click
        Me.NavigatePrev()
    End Sub

#End Region

#Region " Menus "

    Private Sub MenuStrip1_MenuActivate(sender As Object, e As EventArgs) Handles MainMenuStrip1.MenuActivate
        Dim hasImage As Boolean = Me.PicBox.Image IsNot Nothing

        Me.MenuItemCopyFileTo.Enabled = hasImage
        Me.MenuItemSaveAs.Enabled = hasImage
        Me.MenuItemCloseCurrentDirectory.Enabled = hasImage
        Me.MenuItemSendAllToRecycleBin.Enabled = hasImage
        Me.MenuItemShowInExplorer.Enabled = hasImage
        Me.MenuItemSetWallpaper.Enabled = hasImage

        Me.MenuItemZoomIn.Enabled = hasImage
        Me.MenuItemZoomOut.Enabled = hasImage
        Me.MenuItemResetZoom.Enabled = hasImage

        Me.MenuItemResetOrientation.Enabled = Me._rotationAngle <> 0
    End Sub

    ''' <summary>
    ''' Opens a modern Vista-style folder browser (OokiiDialogs) and
    ''' reloads the viewer with the chosen directory.
    ''' </summary>
    Private Sub MenuItemOpenDirectory_Click(sender As Object, e As System.EventArgs) Handles MenuItemOpenDirectory.Click
        Using dlg As New VistaFolderBrowserDialog()
            dlg.Description = "Select an image directory"
            dlg.UseDescriptionForTitle = True
            dlg.ShowNewFolderButton = False

            If Not String.IsNullOrWhiteSpace(Me.FolderPath) AndAlso
               Directory.Exists(Me.FolderPath) Then
                dlg.SelectedPath = Me.FolderPath
            End If

            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return

            Dim chosen As String = dlg.SelectedPath
            If String.IsNullOrWhiteSpace(chosen) OrElse Not Directory.Exists(chosen) Then Return

            Me.StopWorkerAndClearCache()
            Me.FolderPath = chosen
            Me._currentIndex = 0
            Me._lastWorkerCenter = -1
            Me.PicBox.Image = Nothing
            Me.lblInfo.Text = "0 / 0  |  No more images."

            Me.LoadDirectory()
        End Using
    End Sub

    Private Sub MenuItemCloseCurrentDirectory_Click(sender As Object, e As System.EventArgs) Handles MenuItemCloseCurrentDirectory.Click
        If Me._imageFiles.Length = 0 Then
            MessageBox.Show(Me, "No files are currently loaded.",
                            "Close current directory",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Me.StopWorkerAndClearCache()
        Me.FolderPath = ""
        Me._lastWorkerCenter = -1
        Me.PicBox.Image = Nothing
        Me._imageFiles = Array.Empty(Of String)()
        Me._currentIndex = 0
        Me.lblInfo.Text = "0 / 0  |  No more images."
        Me.UpdateNavigationButtons()
    End Sub

    ''' <summary>
    ''' Sends every currently loaded file in <see cref="_imageFiles"/> to
    ''' the Windows Recycle Bin after the user confirms via a dialog.
    ''' </summary>
    Private Sub MenuItemSendAllToRecycleBin_Click(sender As Object, e As System.EventArgs) Handles MenuItemSendAllToRecycleBin.Click
        If Me._imageFiles.Length = 0 Then
            MessageBox.Show(Me, "No files are currently loaded.",
                            "Send to recycle bin",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim confirm As DialogResult = MessageBox.Show(
            Me,
            $"Send all {Me._imageFiles.Length} loaded image file(s) to the Recycle Bin?",
            "Send all to recycle bin",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1)

        If confirm <> DialogResult.OK Then Return

        ' Stop cache worker and release all bitmaps before deleting files.
        Me.StopWorkerAndClearCache()
        Me.PicBox.Image = Nothing

        Dim errors As New System.Text.StringBuilder()

        For Each filePath As String In Me._imageFiles
            Try
                If File.Exists(filePath) Then
                    FileSystem.DeleteFile(filePath,
                                          UIOption.OnlyErrorDialogs,
                                          RecycleOption.SendToRecycleBin)
                End If
            Catch ex As Exception
                errors.AppendLine($"{Path.GetFileName(filePath)}: {ex.Message}")
                Exit For
            End Try
#If DEBUG Then
        Thread.CurrentThread.Join(0) ' Prevents ContextSwitchDeadlock on long-running loops
#End If
        Next

        Me._imageFiles = Array.Empty(Of String)()
        Me._currentIndex = 0
        Me.lblInfo.Text = "0 / 0  |  No more images."
        Me.UpdateNavigationButtons()

        If errors.Length > 0 Then
            MessageBox.Show(Me,
                            $"Some files could not be recycled:{Environment.NewLine}{errors}",
                            "Partial error",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub

    ''' <summary>
    ''' Opens a standard file dialog to load a single image file, 
    ''' automatically pointing the viewer context to its parent folder.
    ''' </summary>
    Private Sub MenuItemOpenFile_Click(sender As Object, e As EventArgs) Handles MenuItemOpenFile.Click
        Using dlg As New OpenFileDialog()
            Dim filterBuilder As New System.Text.StringBuilder("Image Files|")
            For i As Integer = 0 To AppGlobals.SupportedExtensions.Length - 1
                Dim ext As String = AppGlobals.SupportedExtensions(i).Replace(".", "")
                filterBuilder.Append($"*.{ext}")
                If i < AppGlobals.SupportedExtensions.Length - 1 Then
                    filterBuilder.Append(";")
                End If
            Next
            filterBuilder.Append("|All Files (*.*)|*.*")

            dlg.Filter = filterBuilder.ToString()
            dlg.Title = "Select an image file"

            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return

            Dim chosenFile As String = dlg.FileName
            If String.IsNullOrWhiteSpace(chosenFile) OrElse Not File.Exists(chosenFile) Then Return

            Me.LoadSingleImageFile(chosenFile)
        End Using
    End Sub

    Private Sub MenuItemCopyFileTo_Click(sender As Object, e As EventArgs) Handles MenuItemCopyFileTo.Click

        Dim getcachedimg As CachedImage = Nothing
        If Not Me._cache.TryGetValue(Me._imageFiles(Me._currentIndex), getcachedimg) Then
            MessageBox.Show("No image loaded.", "Save Image", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        Using saveDialog As New SaveFileDialog()

            Dim filepath As String = Path.GetFullPath(Me._imageFiles(Me._currentIndex))
            Dim dirpath As String = Path.GetDirectoryName(filepath)
            Dim filename As String = Path.GetFileName(filepath)
            Dim fileExt As String = Path.GetExtension(filename).ToLowerInvariant()

            saveDialog.Filter = "All files|*"
            saveDialog.Title = "Save Current Image"
            saveDialog.FileName = filename
            saveDialog.DefaultExt = fileExt
            saveDialog.FilterIndex = 1
            saveDialog.AddExtension = True
            saveDialog.OverwritePrompt = True
            saveDialog.ValidateNames = True
            saveDialog.SupportMultiDottedExtensions = False
            saveDialog.InitialDirectory = dirpath

            Dim filterIndex As Integer = 1
            Select Case fileExt
                Case ".jpg", ".jpeg"
                    saveDialog.Filter &= "|JPEG Image (*.jpg; *.jpeg)|*.jpg;*.jpeg"
                    saveDialog.FilterIndex = 2

                Case ".png"
                    saveDialog.Filter &= "|PNG Image (*.PNG)|*.png"
                    saveDialog.FilterIndex = 2

                Case ".bmp"
                    saveDialog.Filter &= "|Bitmap Image (*.bmp)|*.bmp"
                    saveDialog.FilterIndex = 2

                Case ".tif", ".tiff"
                    saveDialog.Filter &= "|TIFF Image (*.tif; *.tiff)|*.tif;*.tiff"
                    saveDialog.FilterIndex = 2

                Case Else
                    saveDialog.FilterIndex = 1
            End Select

            If saveDialog.ShowDialog() = DialogResult.OK Then
                Try
                    File.Copy(filepath, saveDialog.FileName, True)
                    MessageBox.Show($"File successfully copied to: {saveDialog.FileName}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

                Catch ex As Exception
                    MessageBox.Show($"An error occurred while copying the file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using

    End Sub

    Private Sub MenuItemSaveAs_Click(sender As Object, e As EventArgs) Handles MenuItemSaveAs.Click

        Dim getcachedimg As CachedImage = Nothing
        If Not Me._cache.TryGetValue(Me._imageFiles(Me._currentIndex), getcachedimg) Then
            MessageBox.Show("No image loaded.", "Save Image", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        Using saveDialog As New SaveFileDialog()

            Dim filepath As String = Path.GetFullPath(Me._imageFiles(Me._currentIndex))
            Dim dirpath As String = Path.GetDirectoryName(filepath)
            Dim filename As String = Path.GetFileName(filepath)
            Dim fileExt As String = Path.GetExtension(filename).ToLowerInvariant()

            saveDialog.Filter = "JPEG Image (*.jpg; *.jpeg)|*.jpg;*.jpeg|PNG Image (*.png)|*.png|Bitmap Image (*.bmp)|*.bmp|TIFF Image (*.tif; *.tiff)|*.tif;*.tiff"
            saveDialog.Title = "Save Current Image"
            saveDialog.FileName = filename
            saveDialog.DefaultExt = fileExt
            saveDialog.FilterIndex = 1
            saveDialog.AddExtension = True
            saveDialog.OverwritePrompt = True
            saveDialog.ValidateNames = True
            saveDialog.SupportMultiDottedExtensions = False
            saveDialog.InitialDirectory = dirpath

            Dim filterIndex As Integer = 1
            Select Case fileExt
                Case ".jpg", ".jpeg"
                    saveDialog.FilterIndex = 1

                Case ".png"
                    saveDialog.FilterIndex = 2

                Case ".bmp"
                    saveDialog.FilterIndex = 3

                Case ".tif", ".tiff"
                    saveDialog.FilterIndex = 4

                Case Else
                    saveDialog.FilterIndex = 1 ' default jpeg
            End Select

            If saveDialog.ShowDialog() = DialogResult.OK Then
                Try
                    Dim selectedExtension As String = Path.GetExtension(saveDialog.FileName).ToLowerInvariant()
                    Dim imageFormatToSave As ImageFormat = ImageFormat.Png

                    Select Case saveDialog.FilterIndex
                        Case 1 '".jpg", ".jpeg"
                            imageFormatToSave = ImageFormat.Jpeg
                        Case 2 '".png"
                            imageFormatToSave = ImageFormat.Png
                        Case 3 '".bmp"
                            imageFormatToSave = ImageFormat.Bmp
                        Case 4 '".tif", ".tiff"
                            imageFormatToSave = ImageFormat.Tiff
                    End Select

                    Dim imageToSave As Image = getcachedimg.Bitmap

                    If imageToSave IsNot Nothing Then
                        imageToSave.Save(saveDialog.FileName, imageFormatToSave)
                        MessageBox.Show($"Image successfully saved to: {saveDialog.FileName}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Else
                        MessageBox.Show("The cached image object does not contain valid image data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    End If

                Catch ex As Exception
                    MessageBox.Show($"An error occurred while saving the image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using

    End Sub

    Private Sub MenuItemShowInExplorer_Click(sender As Object, e As EventArgs) Handles MenuItemShowInExplorer.Click

        Dim filePath As String = Me._imageFiles(Me._currentIndex)

        If Not File.Exists(filePath) Then
            Throw New FileNotFoundException("The current image file no longer exists.", filePath)
        End If

        Dim startInfo As New ProcessStartInfo("explorer.exe", $"/select,""{filePath}""") With {
            .UseShellExecute = True
        }
        Process.Start(startInfo)
    End Sub

    Private Sub MenuItemWallpaperFill_Click(sender As Object, e As EventArgs) Handles MenuItemWallpaperFill.Click
        Me.ApplyWallpaperFromMenu(DesktopWallpaperStyle.Fill)
    End Sub

    Private Sub MenuItemWallpaperFit_Click(sender As Object, e As EventArgs) Handles MenuItemWallpaperFit.Click
        Me.ApplyWallpaperFromMenu(DesktopWallpaperStyle.Fit)
    End Sub

    Private Sub MenuItemWallpaperStretch_Click(sender As Object, e As EventArgs) Handles MenuItemWallpaperStretch.Click
        Me.ApplyWallpaperFromMenu(DesktopWallpaperStyle.Stretch)
    End Sub

    Private Sub MenuItemWallpaperTile_Click(sender As Object, e As EventArgs) Handles MenuItemWallpaperTile.Click
        Me.ApplyWallpaperFromMenu(DesktopWallpaperStyle.Tile)
    End Sub

    Private Sub MenuItemWallpaperCenter_Click(sender As Object, e As EventArgs) Handles MenuItemWallpaperCenter.Click
        Me.ApplyWallpaperFromMenu(DesktopWallpaperStyle.Center)
    End Sub

    Private Sub MenuItemWallpaperSpan_Click(sender As Object, e As EventArgs) Handles MenuItemWallpaperSpan.Click
        Me.ApplyWallpaperFromMenu(DesktopWallpaperStyle.Span)
    End Sub

    Private Sub MenuItemExit_Click(sender As Object, e As EventArgs) Handles MenuItemExit.Click

        Me.Close()
    End Sub

    Private Sub MenuItemShowCacheLabel_Click(sender As Object, e As EventArgs) Handles MenuItemShowCacheLabel.Click
        My.Settings.ShowCacheLabel = Me.MenuItemShowCacheLabel.Checked
        My.Settings.Save()

        If Not Me.SHOW_CACHE_LABEL Then
            Me.lblLoading.Visible = False
        End If
    End Sub

    Private Sub MenuItemShowProgressBar_Click(sender As Object, e As EventArgs) Handles MenuItemShowProgressBar.Click
        My.Settings.ShowProgressBar = Me.MenuItemShowProgressBar.Checked
        My.Settings.Save()

        If Not Me.SHOW_PROGRESS_BAR Then
            Me.progressBar.Visible = False
        End If
    End Sub

    Private Sub MenuItemAskForDirectoryPathAtStartup_Click(sender As Object, e As EventArgs) Handles MenuItemAskForDirectoryPathAtStartup.Click
        My.Settings.AskForDirectoryPathAtStartup = Me.MenuItemAskForDirectoryPathAtStartup.Checked
        My.Settings.Save()
    End Sub

    Private Sub MenuItemDelKeyBehavior_Click(sender As Object, e As EventArgs) Handles MenuItemDelKeyBehavior.Click
        My.Settings.DelKeyMovesImageToCustomFolder = Me.MenuItemDelKeyBehavior.Checked
        My.Settings.Save()
    End Sub

    Private Sub CustomFolderTextBox_TextChanged(sender As Object, e As EventArgs) Handles CustomFolderTextBox.TextChanged
        If Me._isUpdatingCustomFolderTextBox Then Return

        Dim rawText As String = Me.CustomFolderTextBox.Text

        If rawText.Length = 0 Then
            Me._isUpdatingCustomFolderTextBox = True
            Me.CustomFolderTextBox.Text = Me._lastValidCustomFolderPath
            Me.CustomFolderTextBox.SelectionStart = Me.CustomFolderTextBox.TextLength
            Me._isUpdatingCustomFolderTextBox = False
            Return
        End If

        ' GetInvalidPathChars() SI permite ":" y "\" a diferencia de GetInvalidFileNameChars()
        Dim invalidChars As Char() = Path.GetInvalidPathChars()
        Dim builder As New System.Text.StringBuilder(rawText.Length)

        For Each ch As Char In rawText
            ' Evitamos caracteres de control destructivos pero permitimos el resto de la ruta
            If Array.IndexOf(invalidChars, ch) < 0 Then
                builder.Append(ch)
            End If
        Next

        Dim sanitizedText As String = builder.ToString()

        If sanitizedText.Length = 0 Then
            Me._isUpdatingCustomFolderTextBox = True
            Me.CustomFolderTextBox.Text = Me._lastValidCustomFolderPath
            Me.CustomFolderTextBox.SelectionStart = Me.CustomFolderTextBox.TextLength
            Me._isUpdatingCustomFolderTextBox = False
            Return
        End If

        If sanitizedText <> rawText Then
            Me._isUpdatingCustomFolderTextBox = True
            Dim caretPosition As Integer = Me.CustomFolderTextBox.SelectionStart
            Me.CustomFolderTextBox.Text = sanitizedText
            Me.CustomFolderTextBox.SelectionStart = Math.Min(caretPosition, Me.CustomFolderTextBox.TextLength)
            Me._isUpdatingCustomFolderTextBox = False
        End If

        Me._lastValidCustomFolderPath = sanitizedText
        My.Settings.CustomFolderPath = sanitizedText
    End Sub

    Private Sub CustomFolderTextBox_Validating(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles CustomFolderTextBox.Validating
        Dim finalValue As String = Me.CustomFolderTextBox.Text.Trim()

        ' Validación final para asegurar que la estructura de la ruta sea coherente en Windows
        Dim isValidPath As Boolean = False
        Try
            If Not String.IsNullOrWhiteSpace(finalValue) Then
                ' Check path structure without hitting the disk
                Dim fullPath As String = Path.GetFullPath(finalValue)
                isValidPath = True
            End If
        Catch ex As Exception
            isValidPath = False
        End Try

        If Not isValidPath Then
            Me._isUpdatingCustomFolderTextBox = True
            Me.CustomFolderTextBox.Text = Me._lastValidCustomFolderPath
            Me.CustomFolderTextBox.SelectionStart = Me.CustomFolderTextBox.TextLength
            Me._isUpdatingCustomFolderTextBox = False
            e.Cancel = True
            Return
        End If

        Me._lastValidCustomFolderPath = finalValue
        Me.CustomFolderTextBox.Text = finalValue
        My.Settings.CustomFolderPath = finalValue
        My.Settings.Save()
    End Sub

    Private Sub CacheRadius_ValueChanged(sender As Object, e As EventArgs)
        Dim newForward As Integer = CInt(Me.NumForwardCtrl.Value)
        Dim newBack As Integer = CInt(Me.NumBackCtrl.Value)

        My.Settings.CacheRadiusForward = newForward
        My.Settings.CacheRadiusBack = newBack
        My.Settings.Save()

        Interlocked.Increment(Me._cacheWindowVersion)

        Me._lastWorkerCenter = -1
        Me._wakeSignal.Set()
    End Sub

#End Region

#Region " Picture Box "

    Private Sub PicBox_Paint(sender As Object, e As PaintEventArgs)
        If Me._imageFiles Is Nothing OrElse Me._imageFiles.Length = 0 Then
            Return
        End If

        Dim idx As Integer
        SyncLock Me._indexLock
            idx = Me._currentIndex
        End SyncLock

        If Me.Text.Contains("(loading…)") Then
            Dim g As Graphics = e.Graphics

            ' 1. Darker background overlay 
            Using dimBrush As New SolidBrush(Color.FromArgb(200, 0, 0, 0))
                g.FillRectangle(dimBrush, Me.PicBox.ClientRectangle)
            End Using

            ' 2. Layout calculations for dots and text
            Dim centerX As Integer = Me.PicBox.ClientRectangle.Width \ 2
            Dim centerY As Integer = Me.PicBox.ClientRectangle.Height \ 2
            Dim dotSize As Integer = 20
            Dim dotGap As Integer = 24

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias

            ' Draw the three dots
            Using dotBrush As New SolidBrush(Color.FromArgb(230, 240, 240, 240))
                g.FillEllipse(dotBrush, centerX - (dotSize \ 2) - dotGap, centerY - (dotSize \ 2), dotSize, dotSize)
                g.FillEllipse(dotBrush, centerX - (dotSize \ 2), centerY - (dotSize \ 2), dotSize, dotSize)
                g.FillEllipse(dotBrush, centerX - (dotSize \ 2) + dotGap, centerY - (dotSize \ 2), dotSize, dotSize)
            End Using

            ' 3. Draw the loading status text below the dots
            Dim loadingText As String
            If File.Exists(Me._imageFiles(idx)) Then
                loadingText = $"Loading image {idx + 1} …"
                Using textFont As New Font("Segoe UI", 24, FontStyle.Regular)
                    Using textBrush As New SolidBrush(Color.FromArgb(220, 240, 240, 240))
                        Dim textSize As SizeF = g.MeasureString(loadingText, textFont)
                        ' Positioned 25 pixels below the center vertical axis
                        Dim textX As Single = Convert.ToSingle(centerX) - (textSize.Width / 2.0F)
                        Dim textY As Single = Convert.ToSingle(centerY) + 25.0F

                        g.DrawString(loadingText, textFont, textBrush, textX, textY)
                    End Using
                End Using
            Else
                g.Clear(Color.Black)
                loadingText = $"Image {idx + 1} does not exist in disk anymore!"
                Using errorFont As New Font("Segoe UI Semibold", 22, FontStyle.Regular)

                    Dim textSize As SizeF = g.MeasureString(loadingText, errorFont)

                    Dim textX As Single = centerX - (textSize.Width / 2.0F)
                    Dim textY As Single = centerY + 25.0F

                    Using shadowBrush As New SolidBrush(Color.FromArgb(140, 0, 0, 0))
                        g.DrawString(loadingText, errorFont, shadowBrush, textX + 2, textY + 2)
                    End Using

                    Using textBrush As New SolidBrush(Color.FromArgb(240, 255, 90, 90))
                        g.DrawString(loadingText, errorFont, textBrush, textX, textY)
                    End Using

                End Using

                Dim size As Single = 60.0F
                Dim half As Single = size / 2.0F

                Dim x1 As Single = centerX - half
                Dim y1 As Single = centerY - half
                Dim x2 As Single = centerX + half
                Dim y2 As Single = centerY + half

                Using penShadow As New Pen(Color.FromArgb(120, 0, 0, 0), 8.0F)
                    penShadow.StartCap = Drawing2D.LineCap.Round
                    penShadow.EndCap = Drawing2D.LineCap.Round

                    g.DrawLine(penShadow, x1 + 2, y1 + 2, x2 + 2, y2 + 2)
                    g.DrawLine(penShadow, x1 + 2, y2 + 2, x2 + 2, y1 + 2)
                End Using

                Using penError As New Pen(Color.FromArgb(230, 255, 80, 80), 6.0F)
                    penError.StartCap = Drawing2D.LineCap.Round
                    penError.EndCap = Drawing2D.LineCap.Round

                    g.DrawLine(penError, x1, y1, x2, y2)
                    g.DrawLine(penError, x1, y2, x2, y1)
                End Using
                iscurrentimagedisplayed = True
            End If

        End If

        If Me._isImageMissing Then
            Dim g As Graphics = e.Graphics

            ' 1. Darker background overlay 
            Using dimBrush As New SolidBrush(Color.FromArgb(200, 0, 0, 0))
                g.FillRectangle(dimBrush, Me.PicBox.ClientRectangle)
            End Using

            ' 2. Layout calculations for dots and text
            Dim centerX As Integer = Me.PicBox.ClientRectangle.Width \ 2
            Dim centerY As Integer = Me.PicBox.ClientRectangle.Height \ 2

            Dim loadingText As String = $"Image {idx + 1} does not exist in disk anymore!"
            Using errorFont As New Font("Segoe UI Semibold", 22, FontStyle.Regular)

                Dim textSize As SizeF = g.MeasureString(loadingText, errorFont)

                Dim textX As Single = centerX - (textSize.Width / 2.0F)
                Dim textY As Single = centerY + 25.0F

                Using shadowBrush As New SolidBrush(Color.FromArgb(140, 0, 0, 0))
                    g.DrawString(loadingText, errorFont, shadowBrush, textX + 2, textY + 2)
                End Using

                Using textBrush As New SolidBrush(Color.FromArgb(240, 255, 90, 90))
                    g.DrawString(loadingText, errorFont, textBrush, textX, textY)
                End Using

            End Using

            Dim size As Single = 60.0F
            Dim half As Single = size / 2.0F

            Dim x1 As Single = centerX - half
            Dim y1 As Single = centerY - half
            Dim x2 As Single = centerX + half
            Dim y2 As Single = centerY + half

            Using penShadow As New Pen(Color.FromArgb(120, 0, 0, 0), 8.0F)
                penShadow.StartCap = Drawing2D.LineCap.Round
                penShadow.EndCap = Drawing2D.LineCap.Round

                g.DrawLine(penShadow, x1 + 2, y1 + 2, x2 + 2, y2 + 2)
                g.DrawLine(penShadow, x1 + 2, y2 + 2, x2 + 2, y1 + 2)
            End Using

            Using penError As New Pen(Color.FromArgb(230, 255, 80, 80), 6.0F)
                penError.StartCap = Drawing2D.LineCap.Round
                penError.EndCap = Drawing2D.LineCap.Round

                g.DrawLine(penError, x1, y1, x2, y2)
                g.DrawLine(penError, x1, y2, x2, y1)
            End Using
            iscurrentimagedisplayed = True
        End If
    End Sub

    Private Sub ActualPicBox_MouseDown(sender As Object, e As MouseEventArgs)

        If e.Button = MouseButtons.Middle Then
            If Me._isFullscreen Then
                Me.ExitFullscreen()
            Else
                Me.EnterFullscreen()
            End If
            Return
        End If

        If e.Button = MouseButtons.Left Then
            Me._dragActive = True
            Me._dragStart = Control.MousePosition
            Me._scrollStart = New Point(-Me.PanScroll.AutoScrollPosition.X,
                                     -Me.PanScroll.AutoScrollPosition.Y)
            CType(sender, PictureBox).Capture = True
        End If
    End Sub

    Private Sub ActualPicBox_MouseMove(sender As Object, e As MouseEventArgs)
        If Not Me._dragActive Then Return
        Dim cur As Point = Control.MousePosition
        Dim dx As Integer = cur.X - Me._dragStart.X
        Dim dy As Integer = cur.Y - Me._dragStart.Y
        Me.PanScroll.AutoScrollPosition = New Point(Me._scrollStart.X - dx, Me._scrollStart.Y - dy)
    End Sub

    Private Sub ActualPicBox_MouseUp(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then
            Me._dragActive = False
            CType(sender, PictureBox).Capture = False
        End If
    End Sub

    Private Sub ActualPicBox_MouseDoubleClick(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then Me.ExitActualSize()
    End Sub

    Private Sub PicBox_MouseClick(sender As Object, e As MouseEventArgs) Handles PicBox.MouseClick
        If e.Button = MouseButtons.Middle Then Me.ToggleFullscreen()
    End Sub

    Private Sub PicBox_MouseDoubleClick(sender As Object, e As MouseEventArgs) Handles PicBox.MouseDoubleClick
        If e.Button = MouseButtons.Left Then Me.ToggleActualSize()
    End Sub

#End Region

#Region " Navigation Timer "

    Private Sub NavTimer_Tick(sender As Object, e As EventArgs) Handles NavTimer.Tick
        Me.NavTimer.Stop()
        Me.DrainPendingDisposal()   ' Dispose evicted bitmaps that are no longer displayed
        Me.DisplayCurrentImage()
        Me.DrainPendingDisposal()   ' Catch any that became disposable after the image swap
        Me._wakeSignal.Set()
    End Sub

#End Region

#Region " AboutBox "

    Private Sub AboutBox_HandleCreated(sender As Object, e As EventArgs) Handles AboutBox.HandleCreated

        Me.AboutBox.BackColor = Color.FromArgb(255, 30, 30, 30)
        Me.AboutBox.ForeColor = Me.ForeColor
        Me.AboutBox.TextBoxDescription.BackColor = Color.FromArgb(255, 50, 50, 50)
        Me.AboutBox.TextBoxDescription.ForeColor = Me.ForeColor

        DwmSetWindowAttribute(Me.AboutBox.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, 1, Marshal.SizeOf(1))
    End Sub

#End Region

#End Region

#Region " Overriden Methods "

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean

        Select Case keyData

            Case Keys.Left
                If Me._isActualSize Then Me.ScrollActualSize(-PAN_KEY_STEP, 0) Else Me.NavigatePrev()
                Return True

            Case Keys.Right
                If Me._isActualSize Then Me.ScrollActualSize(PAN_KEY_STEP, 0) Else Me.NavigateNext()
                Return True

            Case Keys.Up
                If Me._isActualSize Then
                    Me.ScrollActualSize(0, -PAN_KEY_STEP)
                    Return True
                End If

            Case Keys.Down
                If Me._isActualSize Then
                    Me.ScrollActualSize(0, PAN_KEY_STEP)
                    Return True
                End If

            Case Keys.Add, Keys.Oemplus
                Me.AdjustZoom(0.1)
                Return True

            Case Keys.Subtract, Keys.OemMinus
                Me.AdjustZoom(-0.1)
                Return True

        End Select

        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

#End Region

#Region " Private Methods "

    Private Sub BuildUI()
        Me.Text = $"{My.Application.Info.Title} {My.Application.Info.Version.ToString(fieldCount:=3)}"
        Me.BackColor = Color.FromArgb(10, 10, 10)
        Me.ForeColor = Color.WhiteSmoke
        Me.KeyPreview = True

        Me.NavTimer = New System.Windows.Forms.Timer With {
            .Interval = NAV_THROTTLE_MS,
            .Enabled = False
        }

        ' ── Menu strip ──────────────────────────────────────────────
        Me.MenuItemOpenFile = New ToolStripMenuItem("Open &file...") With {
            .Image = My.Resources.Resource1.openfile
        }

        Me.MenuItemCopyFileTo = New ToolStripMenuItem("&Copy file to...") With {
            .Image = My.Resources.Resource1.copyto
        }

        Me.MenuItemSaveAs = New ToolStripMenuItem("&Save file as...") With {
            .Image = My.Resources.Resource1.saveas
        }

        Me.MenuItemOpenDirectory = New ToolStripMenuItem("Open &directory") With {
            .Image = My.Resources.Resource1.openfolder
        }

        Me.MenuItemCloseCurrentDirectory = New ToolStripMenuItem("Close c&urrent directory") With {
            .Image = My.Resources.Resource1.closefolder
        }

        Me.MenuItemSendAllToRecycleBin = New ToolStripMenuItem("Send all loaded files to &recycle bin") With {
            .Image = My.Resources.Resource1.recycle
        }

        Me.MenuItemShowInExplorer = New ToolStripMenuItem("Show file in &Explorer...") With {
            .Image = Nothing
        }

        Me.MenuItemOpenFile = New ToolStripMenuItem("Open &file...") With {
            .Image = My.Resources.Resource1.openfile
        }

        Me.MenuItemExit = New ToolStripMenuItem("Close &application") With {
            .Image = My.Resources.Resource1._exit
        }

        Dim fileMenu As New ToolStripMenuItem("&File")
        fileMenu.DropDownItems.Add(Me.MenuItemOpenFile)
        fileMenu.DropDownItems.Add(Me.MenuItemCopyFileTo)
        fileMenu.DropDownItems.Add(Me.MenuItemSaveAs)
        fileMenu.DropDownItems.Add(New ToolStripSeparator())
        fileMenu.DropDownItems.Add(Me.MenuItemOpenDirectory)
        fileMenu.DropDownItems.Add(Me.MenuItemCloseCurrentDirectory)
        fileMenu.DropDownItems.Add(New ToolStripSeparator())
        fileMenu.DropDownItems.Add(Me.MenuItemShowInExplorer)
        fileMenu.DropDownItems.Add(New ToolStripSeparator())
        fileMenu.DropDownItems.Add(Me.MenuItemSendAllToRecycleBin)
        fileMenu.DropDownItems.Add(New ToolStripSeparator())
        fileMenu.DropDownItems.Add(Me.MenuItemExit)

        ' ── Menu strip: Options Menu ────────────────────────────────
        Me.MenuItemShowCacheLabel = New ToolStripMenuItem("Show Cache &Label") With {
            .CheckOnClick = True,
            .Checked = Me.SHOW_CACHE_LABEL
        }
        Me.MenuItemShowProgressBar = New ToolStripMenuItem("Show Cache &Progress Bar") With {
            .CheckOnClick = True,
            .Checked = Me.SHOW_PROGRESS_BAR
        }

        ' Create NumericUpDown for Forward Radius
        Me.NumForwardCtrl = New NumericUpDown With {
            .Minimum = 1,
            .Maximum = 5000,
            .Value = Me.CACHE_RADIUS_FORWARD,
            .Width = 80
        }
        Me.NumForwardHost = New ToolStripControlHost(Me.NumForwardCtrl)

        ' Create SubMenu Item for Forward
        Dim menuForwardGroup As New ToolStripMenuItem("&Forward Radius: ")
        menuForwardGroup.DropDownItems.Add(Me.NumForwardHost)

        ' Create NumericUpDown for Back Radius
        Me.NumBackCtrl = New NumericUpDown With {
            .Minimum = 1,
            .Maximum = 5000,
            .Value = Me.CACHE_RADIUS_BACK,
            .Width = 80
        }
        Me.NumBackHost = New ToolStripControlHost(Me.NumBackCtrl)

        ' Create SubMenu Item for Back
        Dim menuBackGroup As New ToolStripMenuItem("&Backward Radius: ")
        menuBackGroup.DropDownItems.Add(Me.NumBackHost)

        ' Submenu Container for Cache Settings
        Me.MenuItemCacheMenu = New ToolStripMenuItem("Configure Cache &Radius")
        Me.MenuItemCacheMenu.DropDownItems.Add(menuForwardGroup)
        Me.MenuItemCacheMenu.DropDownItems.Add(menuBackGroup)

        Me.MenuItemAskForDirectoryPathAtStartup = New ToolStripMenuItem("&Ask for directory path at program startup") With {
            .CheckOnClick = True,
            .Checked = Me.ASK_FOR_DIRECTORY_PATH_AT_STARTUP
        }

        Me.MenuItemDelKeyBehavior = New ToolStripMenuItem("'Del' key &moves the current image to custom folder") With {
            .CheckOnClick = True,
            .Checked = Me.DEL_KEY_MOVES_IMAGE_TO_CUSTOM_FOLDER
        }

        Dim customFolderMenu As New ToolStripMenuItem("Custom &folder path")

        Me.CustomFolderTextBox = New ToolStripTextBox With {
            .Text = My.Settings.CustomFolderPath,
            .AutoSize = False,
            .Width = 250,
            .Height = 100
        }

        Dim internalTextBox As TextBox = Me.CustomFolderTextBox.TextBox
        If internalTextBox IsNot Nothing Then
            internalTextBox.Multiline = True
            internalTextBox.WordWrap = True
            internalTextBox.ScrollBars = ScrollBars.Vertical
        End If

        ' Assemble Image Menu
        Me.MenuItemSetWallpaper = New ToolStripMenuItem("Set as Desktop &Wallpaper...") With {
            .Image = Nothing
        }
        Me.MenuItemWallpaperFill = New ToolStripMenuItem("&Fill")
        Me.MenuItemWallpaperFit = New ToolStripMenuItem("F&it")
        Me.MenuItemWallpaperStretch = New ToolStripMenuItem("S&tretch")
        Me.MenuItemWallpaperTile = New ToolStripMenuItem("&Tile")
        Me.MenuItemWallpaperCenter = New ToolStripMenuItem("&Center")
        Me.MenuItemWallpaperSpan = New ToolStripMenuItem("S&pan")

        Me.MenuItemSetWallpaper.DropDownItems.Add(Me.MenuItemWallpaperFill)
        Me.MenuItemSetWallpaper.DropDownItems.Add(Me.MenuItemWallpaperFit)
        Me.MenuItemSetWallpaper.DropDownItems.Add(Me.MenuItemWallpaperStretch)
        Me.MenuItemSetWallpaper.DropDownItems.Add(Me.MenuItemWallpaperTile)
        Me.MenuItemSetWallpaper.DropDownItems.Add(Me.MenuItemWallpaperCenter)
        Me.MenuItemSetWallpaper.DropDownItems.Add(Me.MenuItemWallpaperSpan)

        Dim imageMenu As New ToolStripMenuItem("&Image")
        imageMenu.DropDownItems.Add(Me.MenuItemZoomIn)
        imageMenu.DropDownItems.Add(Me.MenuItemZoomOut)
        imageMenu.DropDownItems.Add(Me.MenuItemResetZoom)
        imageMenu.DropDownItems.Add(New ToolStripSeparator())
        imageMenu.DropDownItems.Add(Me.MenuItemRotateLeft)
        imageMenu.DropDownItems.Add(Me.MenuItemRotateRight)
        imageMenu.DropDownItems.Add(Me.MenuItemResetOrientation)
        imageMenu.DropDownItems.Add(New ToolStripSeparator())
        imageMenu.DropDownItems.Add(Me.MenuItemSetWallpaper)
        AddHandler Me.MenuItemZoomIn.Click, Sub(sender As Object, e As EventArgs) Me.AdjustZoom(0.1)
        AddHandler Me.MenuItemZoomOut.Click, Sub(sender As Object, e As EventArgs) Me.AdjustZoom(-0.1)
        AddHandler Me.MenuItemResetZoom.Click, Sub(sender As Object, e As EventArgs) Me.ResetZoom()
        AddHandler Me.MenuItemRotateLeft.Click, Sub(sender As Object, e As EventArgs) Me.RotateImage(False)
        AddHandler Me.MenuItemRotateRight.Click, Sub(sender As Object, e As EventArgs) Me.RotateImage(True)
        AddHandler Me.MenuItemResetOrientation.Click, Sub(sender As Object, e As EventArgs)
                                                          Me._rotationAngle = 90
                                                          Me.RotateImage(False)
                                                      End Sub

        ' Assemble Options Menu
        Dim optionsMenu As New ToolStripMenuItem("&Options")
        optionsMenu.DropDownItems.Add(Me.MenuItemShowCacheLabel)
        optionsMenu.DropDownItems.Add(Me.MenuItemShowProgressBar)
        optionsMenu.DropDownItems.Add(Me.MenuItemCacheMenu)
        optionsMenu.DropDownItems.Add(New ToolStripSeparator())
        optionsMenu.DropDownItems.Add(Me.MenuItemAskForDirectoryPathAtStartup)
        optionsMenu.DropDownItems.Add(New ToolStripSeparator())
        optionsMenu.DropDownItems.Add(Me.MenuItemDelKeyBehavior)

        customFolderMenu.DropDownItems.Add(Me.CustomFolderTextBox)
        optionsMenu.DropDownItems.Add(customFolderMenu)

        Dim aboutMenu As New ToolStripMenuItem("&About")
        AddHandler aboutMenu.Click, Sub() Me.AboutBox.ShowDialog()

        ' Wire up change events for the numbers
        AddHandler Me.NumForwardCtrl.ValueChanged, AddressOf Me.CacheRadius_ValueChanged
        AddHandler Me.NumBackCtrl.ValueChanged, AddressOf Me.CacheRadius_ValueChanged

        ' ── Assemble MenuStrip ──────────────────────────────────────
        Me.MainMenuStrip1 = New MenuStrip()
        Me.MainMenuStrip1.Items.Add(fileMenu)
        Me.MainMenuStrip1.Items.Add(imageMenu)
        Me.MainMenuStrip1.Items.Add(optionsMenu)
        Me.MainMenuStrip1.Items.Add(aboutMenu)
        Me.MainMenuStrip1.BackColor = Color.FromArgb(30, 30, 30)
        Me.MainMenuStrip1.ForeColor = SystemColors.ControlDark

        Me.MainMenuStrip = Me.MainMenuStrip1
        ' ────────────────────────────────────────────────────────────

        Me.PicBox = New SafePictureBox With {
            .Dock = DockStyle.Fill,
            .SizeMode = PictureBoxSizeMode.Zoom,
            .BackColor = Color.Black
        }
        AddHandler Me.PicBox.Paint, AddressOf Me.PicBox_Paint

        Me.PanScroll = New Panel With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .BackColor = Color.Black,
            .Visible = False
        }

        Dim pnlContent As New Panel With {
            .Dock = DockStyle.Fill,
            .Margin = Padding.Empty,
            .Padding = Padding.Empty
        }
        pnlContent.Controls.Add(Me.PicBox)
        pnlContent.Controls.Add(Me.PanScroll)

        Me.pnlBottom = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(20, 20, 20)
        }

        Me.BtnPrev = Me.MakeButton("◀  &Prev", New Point(12, 10))
        Me.BtnPrev.Enabled = False
        Me.BtnPrev.Width = 100
        Me.pnlBottom.Controls.Add(Me.BtnPrev)

        Me.BtnNext = Me.MakeButton("&Next  ▶", New Point(Me.BtnPrev.Bounds.Right + 10, Me.BtnPrev.Top))
        Me.BtnNext.Enabled = False
        Me.BtnNext.Width = 100
        Me.pnlBottom.Controls.Add(Me.BtnNext)

        Me.lblInfo = New Label With {
            .ForeColor = Color.FromArgb(220, 220, 220),
            .Font = New Font("Segoe UI", 10.6F),
            .AutoSize = False,
            .Size = New Size(800, Me.pnlBottom.Bounds.Height),
            .Location = New Point(Me.BtnNext.Bounds.Right + 10, Me.BtnNext.Bounds.Top),
            .TextAlign = ContentAlignment.TopLeft
        }
        Me.pnlBottom.Controls.Add(Me.lblInfo)

        Me.lblLoading = New Label With {
            .ForeColor = Color.FromArgb(220, 220, 220),
            .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold),
            .AutoSize = True,
            .BackColor = Color.Transparent,
            .Location = New Point(20, 20),
            .Visible = False
        }
        Me.PicBox.Controls.Add(Me.lblLoading)

        Me.progressBar = New ProgressBar With {
            .Style = Windows.Forms.ProgressBarStyle.Continuous,
            .Height = 4,
            .Dock = DockStyle.Top,
            .Visible = False
        }
        Me.pnlBottom.Controls.Add(Me.progressBar)

        Me.tblMain = New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .RowCount = 3,
            .ColumnCount = 1,
            .Margin = Padding.Empty,
            .Padding = Padding.Empty
        }
        Me.tblMain.RowStyles.Add(New RowStyle())
        Me.tblMain.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Me.tblMain.RowStyles.Add(New RowStyle(SizeType.Absolute, 56.0F))
        Me.tblMain.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        Me.tblMain.Controls.Add(Me.MainMenuStrip1, 0, 0)
        Me.tblMain.Controls.Add(pnlContent, 0, 1)
        Me.tblMain.Controls.Add(Me.pnlBottom, 0, 2)

        Me.Controls.Add(Me.tblMain)
    End Sub

    Private Function MakeButton(text As String, loc As Point) As Button
        Dim b As New Button With {
            .Text = text,
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = Color.FromArgb(50, 50, 50),
            .FlatStyle = FlatStyle.Flat
        }
        b.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80)
        b.Size = New Size(120, 36)
        b.Location = loc
        b.Cursor = Cursors.Hand
        Return b
    End Function

    ''' <summary>
    ''' Standardizes loading an independent single image file and switches 
    ''' the navigation index automatically to that file within its folder structure.
    ''' </summary>
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
            Dim ext As String = Path.GetExtension(fileItem).ToLowerInvariant()
            For Each supported As String In AppGlobals.SupportedExtensions
                If ext = supported Then
                    imageList.Add(fileItem)
                    Exit For
                End If
            Next
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

    Private Function LoadFast(filePath As String) As Bitmap
        Dim src As Bitmap
        Try
            src = New Bitmap(filePath)
        Catch
            Return Nothing
        End Try

        Dim srcW As Integer = src.Width
        Dim srcH As Integer = src.Height
        Dim scale As Double = Math.Min(Me._screenWidth / srcW,
                                       Me._screenHeight / srcH)

        If scale >= 1.0 Then
            Dim native As New Bitmap(srcW, srcH, Imaging.PixelFormat.Format32bppPArgb)
            Using g As Graphics = Graphics.FromImage(native)
                g.DrawImage(src, 0, 0, srcW, srcH)
            End Using
            src.Dispose()
            Return native
        End If

        Dim dstW As Integer = CInt(Math.Floor(srcW * scale))
        Dim dstH As Integer = CInt(Math.Floor(srcH * scale))
        Dim scaled As New Bitmap(dstW, dstH, PixelFormat.Format32bppPArgb)
        Using g As Graphics = Graphics.FromImage(scaled)
            g.InterpolationMode = Drawing2D.InterpolationMode.Low
            g.DrawImage(src, 0, 0, dstW, dstH)
        End Using
        src.Dispose()
        Return scaled
    End Function

    Private Function LoadOriginal(filePath As String) As Bitmap
        Dim src As Bitmap
        Try
            src = New Bitmap(filePath)
        Catch
            Return Nothing
        End Try
        Dim result As New Bitmap(src.Width, src.Height, Imaging.PixelFormat.Format32bppPArgb)
        Using g As Graphics = Graphics.FromImage(result)
            g.DrawImage(src, 0, 0, src.Width, src.Height)
        End Using
        src.Dispose()
        Return result
    End Function

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
            Dim ext As String = Path.GetExtension(cleanPath).ToLowerInvariant()
            For Each supported As String In AppGlobals.SupportedExtensions
                If ext = supported Then
                    imageList.Add(cleanPath)
                    Exit For
                End If
            Next
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
            newIndex = Me.GetInsertionIndex(newFiles, anchorFilePath)
        End If

        Dim newIndexMap As New Dictionary(Of String, Integer)(newFiles.Length, StringComparer.OrdinalIgnoreCase)
        For i As Integer = 0 To newFiles.Length - 1
            newIndexMap(newFiles(i)) = i
        Next

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

    Private Sub ApplyCurrentImageAsWallpaper(style As DesktopWallpaperStyle)
        Dim filePath As String = Me._imageFiles(Me._currentIndex)

        If Not File.Exists(filePath) Then
            Throw New FileNotFoundException("The current image file no longer exists.", filePath)
        End If

        Me.UpdateWallpaperRegistry(style)

        Dim bitmapPath As String = Me.CreateWallpaperBitmap(filePath)
        Dim ok As Boolean = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0UI, bitmapPath, SPIF_UPDATEINIFILE Or SPIF_SENDCHANGE)

        If Not ok Then
            Throw New InvalidOperationException($"SystemParametersInfo(SPI_SETDESKWALLPAPER) failed with Win32 error {Marshal.GetLastWin32Error()}.")
        End If
    End Sub

    Private Sub ApplyWallpaperFromMenu(style As DesktopWallpaperStyle)
        Try
            Me.ApplyCurrentImageAsWallpaper(style)
        Catch ex As Exception
            MessageBox.Show(Me,
                            $"Could not set the wallpaper:{Environment.NewLine}{ex.Message}",
                            "Wallpaper",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub UpdateWallpaperRegistry(style As DesktopWallpaperStyle)
        Dim wallpaperStyleValue As String
        Dim tileWallpaperValue As String

        Select Case style

            Case DesktopWallpaperStyle.Tile
                wallpaperStyleValue = "0"
                tileWallpaperValue = "1"

            Case DesktopWallpaperStyle.Center
                wallpaperStyleValue = "0"
                tileWallpaperValue = "0"

            Case DesktopWallpaperStyle.Stretch
                wallpaperStyleValue = "2"
                tileWallpaperValue = "0"

            Case DesktopWallpaperStyle.Fit
                wallpaperStyleValue = "6"
                tileWallpaperValue = "0"

            Case DesktopWallpaperStyle.Fill
                wallpaperStyleValue = "10"
                tileWallpaperValue = "0"

            Case DesktopWallpaperStyle.Span
                wallpaperStyleValue = "22"
                tileWallpaperValue = "0"

            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(style), style, "Unsupported wallpaper style.")
        End Select

        Registry.SetValue("HKEY_CURRENT_USER\Control Panel\Desktop", "WallpaperStyle", wallpaperStyleValue, RegistryValueKind.String)
        Registry.SetValue("HKEY_CURRENT_USER\Control Panel\Desktop", "TileWallpaper", tileWallpaperValue, RegistryValueKind.String)
    End Sub

    Private Function CreateWallpaperBitmap(filePath As String) As String
        Dim tempFolder As String = Path.Combine(Path.GetTempPath(), $"{My.Application.Info.AssemblyName}")
        Directory.CreateDirectory(tempFolder)

        Dim tempBitmapPath As String = Path.Combine(tempFolder, "CurrentWallpaper.bmp")

        Using sourceImage As Image = Image.FromFile(filePath)
            Using bitmap As New Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format24bppRgb)
                Using g As Graphics = Graphics.FromImage(bitmap)
                    g.DrawImage(sourceImage, 0, 0, sourceImage.Width, sourceImage.Height)
                End Using
                bitmap.Save(tempBitmapPath, ImageFormat.Bmp)
            End Using
        End Using

        Return tempBitmapPath
    End Function

    Private Sub InitializeCustomFolderName(initialValue As String)
        Dim value As String = If(initialValue, String.Empty)

        If String.IsNullOrWhiteSpace(value) Then
            Throw New InvalidOperationException("Custom folder path must start with a valid non-empty value.")
        End If

        Me._lastValidCustomFolderPath = value
        Me.CustomFolderTextBox.Text = value
    End Sub

    Private Sub ReportProgress(pct As Integer, loaded As Integer, total As Integer)
        If Me.SHOW_PROGRESS_BAR Then
            Me.progressBar.Style = Windows.Forms.ProgressBarStyle.Continuous
            Me.progressBar.Minimum = 0
            Me.progressBar.Maximum = 100
            Me.progressBar.Value = Math.Min(pct, 100)
            Me.progressBar.Visible = True
        End If
        If Me.SHOW_CACHE_LABEL Then
            Me.lblLoading.Text = $"Caching images… {loaded}/{total - 1}"
            Me.lblLoading.Visible = True
        End If
    End Sub

    Private Sub ToggleFullscreen()
        If Me._isActualSize Then Me.ExitActualSize()
        If Me._isFullscreen Then Me.ExitFullscreen() Else Me.EnterFullscreen()
    End Sub

    Private Sub EnterFullscreen()
        Me.ResetZoom()

        Me.tblMain?.SuspendLayout()

        Me._savedWin32Style = GetWindowLong(Me.Handle, GWL_STYLE)
        Me._savedBounds = Me.Bounds

        Dim newStyle As Integer = Me._savedWin32Style And Not (WS_CAPTION Or WS_THICKFRAME)
        SetWindowLong(Me.Handle, GWL_STYLE, newStyle)

        Dim scr As Screen = Screen.FromControl(Me)
        SetWindowPos(Me.Handle, IntPtr.Zero,
                     scr.Bounds.X, scr.Bounds.Y,
                     scr.Bounds.Width, scr.Bounds.Height,
                     SWP_FRAMECHANGED Or SWP_NOZORDER Or SWP_NOACTIVATE)

        Me.tblMain.RowStyles(2).SizeType = SizeType.Absolute
        Me.tblMain.RowStyles(2).Height = 0.0F
        Me.pnlBottom.Visible = False
        Me.MainMenuStrip1.Visible = False

        Me.PanScroll.Visible = False
        Me.PicBox.Visible = True
        Me.PicBox.Dock = DockStyle.Fill

        Me.tblMain?.ResumeLayout(True)

        Me._isFullscreen = True
        Me.Text = $"{My.Application.Info.Title} [FULLSCREEN] — {Path.GetFileName(Me._imageFiles(Me._currentIndex))}"
    End Sub

    Private Sub ExitFullscreen()
        Me.ResetZoom()
        Me.tblMain?.SuspendLayout()

        SetWindowLong(Me.Handle, GWL_STYLE, Me._savedWin32Style)
        SetWindowPos(Me.Handle, IntPtr.Zero,
                     Me._savedBounds.X, Me._savedBounds.Y,
                     Me._savedBounds.Width, Me._savedBounds.Height,
                     SWP_FRAMECHANGED Or SWP_NOZORDER Or SWP_NOACTIVATE)

        Me.tblMain.RowStyles(2).SizeType = SizeType.Absolute
        Me.tblMain.RowStyles(2).Height = 56.0F
        Me.pnlBottom.Visible = True

        Me.MainMenuStrip1.Visible = True

        Me.tblMain?.ResumeLayout(True)

        Me._isFullscreen = False
        Me.Text = $"{My.Application.Info.Title} — {Path.GetFileName(Me._imageFiles(Me._currentIndex))}"
    End Sub

    Private Sub ToggleActualSize()
        If Me._isFullscreen Then Me.ExitFullscreen()
        If Me._isActualSize Then Me.ExitActualSize() Else Me.EnterActualSize()
    End Sub

    Private Sub EnterActualSize()
        Me.ResetZoom()
        If Me._imageFiles.Length = 0 Then Return

        Dim filePath As String = Me._imageFiles(Me._currentIndex)

        Cursor.Current = Cursors.WaitCursor
        Dim bmp As Bitmap = Me.LoadOriginal(filePath)
        Cursor.Current = Cursors.Default

        If bmp Is Nothing Then Return
        Me._actualBitmap = bmp

        Dim pbActual As New PictureBox With {
            .SizeMode = PictureBoxSizeMode.StretchImage,
            .Size = New Size(bmp.Width, bmp.Height),
            .Location = New Point(0, 0),
            .Image = bmp,
            .BackColor = Color.Black,
            .Cursor = Cursors.SizeAll,
            .TabStop = False
        }

        AddHandler pbActual.MouseDown, AddressOf Me.ActualPicBox_MouseDown
        AddHandler pbActual.MouseMove, AddressOf Me.ActualPicBox_MouseMove
        AddHandler pbActual.MouseUp, AddressOf Me.ActualPicBox_MouseUp
        AddHandler pbActual.MouseDoubleClick, AddressOf Me.ActualPicBox_MouseDoubleClick

        Me.PanScroll.Controls.Clear()
        Me.PanScroll.AutoScrollMinSize = New Size(bmp.Width, bmp.Height)
        Me.PanScroll.Controls.Add(pbActual)

        Me.PicBox.Visible = False
        Me.PanScroll.Visible = True
        Me.PanScroll.Update()

        Dim scrollX As Integer = Math.Max(0, (bmp.Width - Me.PanScroll.ClientSize.Width) \ 2)
        Dim scrollY As Integer = Math.Max(0, (bmp.Height - Me.PanScroll.ClientSize.Height) \ 2)
        Me.PanScroll.AutoScrollPosition = New Point(scrollX, scrollY)

        Me.PanScroll.Focus()
        Me._isActualSize = True
        Me.Text = $"{My.Application.Info.Title} [1:1] — {Path.GetFileName(filePath)}"
    End Sub

    Private Sub ExitActualSize()
        Me.PanScroll.AutoScrollMinSize = Size.Empty
        Me.PanScroll.Visible = False
        Me.PanScroll.Controls.Clear()

        If Me._actualBitmap IsNot Nothing Then
            Me._actualBitmap.Dispose()
            Me._actualBitmap = Nothing
        End If

        Me.PicBox.Visible = True
        Me._isActualSize = False
        Me._dragActive = False

        If Me._imageFiles.Length > 0 Then
            Me.Text = $"{My.Application.Info.Title} — {Path.GetFileName(Me._imageFiles(Me._currentIndex))}"
        End If

        Me.ResetZoom()
    End Sub

    Private Sub ScrollActualSize(dx As Integer, dy As Integer)
        Dim cur As New Point(-Me.PanScroll.AutoScrollPosition.X,
                                     -Me.PanScroll.AutoScrollPosition.Y)
        Me.PanScroll.AutoScrollPosition = New Point(cur.X + dx, cur.Y + dy)
    End Sub

    Private Sub AdjustZoom(delta As Double)
        If Me._imageFiles Is Nothing OrElse Me._imageFiles.Length = 0 Then Return
        If Me.PicBox.Image Is Nothing AndAlso Me._actualBitmap Is Nothing Then Return

        If Me._isFullscreen Then
            Me.ExitFullscreen()
        End If

        Dim baseBmp As Bitmap = If(Me._isActualSize, Me._actualBitmap, DirectCast(Me.PicBox.Image, Bitmap))
        If baseBmp Is Nothing Then Return

        Dim baseWidth As Integer = baseBmp.Width
        Dim baseHeight As Integer = baseBmp.Height

        If Not Me._isZoomedMode Then
            If Me._isActualSize Then
                Me._baseZoomFactor = 1.0
            Else
                Dim pnlWidth As Double = Convert.ToDouble(Me.PicBox.ClientSize.Width)
                Dim pnlHeight As Double = Convert.ToDouble(Me.PicBox.ClientSize.Height)
                Dim scaleX As Double = pnlWidth / Convert.ToDouble(baseWidth)
                Dim scaleY As Double = pnlHeight / Convert.ToDouble(baseHeight)
                Me._baseZoomFactor = Math.Min(scaleX, scaleY)
            End If
            Me._currentZoomFactor = Me._baseZoomFactor
            Me._isZoomedMode = True
        End If

        Dim newZoom As Double = Me._currentZoomFactor + delta

        If newZoom < 0.1 Then newZoom = 0.1
        If newZoom > 15.0 Then newZoom = 15.0

        ' Exit zoom mode cleanly if we return to the exact starting zoom factor
        If Math.Abs(newZoom - Me._baseZoomFactor) < 0.05 Then
            Me.ResetZoom()
            Return
        End If

        Me._currentZoomFactor = newZoom

        Dim newWidth As Integer = Convert.ToInt32(Math.Round(Convert.ToDouble(baseWidth) * Me._currentZoomFactor))
        Dim newHeight As Integer = Convert.ToInt32(Math.Round(Convert.ToDouble(baseHeight) * Me._currentZoomFactor))

        Dim pbScroll As PictureBox = Nothing
        If Me.PanScroll.Controls.Count > 0 Then
            pbScroll = DirectCast(Me.PanScroll.Controls(0), PictureBox)
        Else
            pbScroll = New PictureBox With {
                .SizeMode = PictureBoxSizeMode.StretchImage,
                .BackColor = Color.Black,
                .Cursor = Cursors.SizeAll,
                .TabStop = False
            }
            AddHandler pbScroll.MouseDown, AddressOf Me.ActualPicBox_MouseDown
            AddHandler pbScroll.MouseMove, AddressOf Me.ActualPicBox_MouseMove
            AddHandler pbScroll.MouseUp, AddressOf Me.ActualPicBox_MouseUp
            AddHandler pbScroll.MouseDoubleClick, AddressOf Me.ActualPicBox_MouseDoubleClick
            Me.PanScroll.Controls.Add(pbScroll)
        End If

        ' We must ensure the panel is visible before calculating ClientSize for centering
        If Me.PicBox.Visible Then
            Me.PicBox.Visible = False
            Me.PanScroll.Visible = True
        End If

        pbScroll.Image = baseBmp
        pbScroll.Size = New Size(newWidth, newHeight)
        Me.PanScroll.AutoScrollMinSize = pbScroll.Size

        ' ═══════════════════════════════════════════════════════════════
        ' DYNAMIC CENTERING ALGORITHM
        ' ═══════════════════════════════════════════════════════════════
        Dim pnlClientW As Integer = Me.PanScroll.ClientSize.Width
        Dim pnlClientH As Integer = Me.PanScroll.ClientSize.Height

        Dim targetX As Integer = pbScroll.Location.X
        Dim targetY As Integer = pbScroll.Location.Y

        ' Center horizontally if smaller than viewport, else reset to 0 to let AutoScroll handle it
        If newWidth < pnlClientW Then
            targetX = (pnlClientW - newWidth) \ 2
        ElseIf pbScroll.Location.X > 0 Then
            targetX = 0
        End If

        ' Center vertically if smaller than viewport, else reset to 0 to let AutoScroll handle it
        If newHeight < pnlClientH Then
            targetY = (pnlClientH - newHeight) \ 2
        ElseIf pbScroll.Location.Y > 0 Then
            targetY = 0
        End If

        ' Only apply layout changes if the coordinates actually shifted, to prevent visual jitter
        If pbScroll.Location.X <> targetX OrElse pbScroll.Location.Y <> targetY Then
            pbScroll.Location = New Point(targetX, targetY)
        End If
    End Sub

    Private Sub ResetZoom()
        Me._isZoomedMode = False
        Me._currentZoomFactor = 1.0
        Me._baseZoomFactor = 1.0

        ' If we are zooming from Fit mode (not ActualSize), 
        ' we must destroy the dynamic zoom box and restore the standard PicBox.
        If Not Me._isActualSize AndAlso Me.PanScroll.Visible Then
            Me.PanScroll.AutoScrollMinSize = Size.Empty
            Me.PanScroll.Controls.Clear()
            Me.PanScroll.Visible = False
            Me.PicBox.Visible = True
        End If
    End Sub

    Private Sub NavigateNext()
        If Not iscurrentimagedisplayed Then
            Return
        End If

        If Me._imageFiles.Length = 0 Then
            Return
        End If

        If Me._currentIndex >= Me._imageFiles.Length - 1 Then
            Return
        End If

        SyncLock Me._indexLock
            Me._currentIndex += 1
        End SyncLock
        Me.iscurrentimagedisplayed = False
        Me.ScheduleDisplay()
    End Sub

    Private Sub NavigatePrev()
        If Not iscurrentimagedisplayed Then
            Return
        End If

        If Me._imageFiles.Length = 0 Then
            Return
        End If

        If Me._currentIndex <= 0 Then
            Return
        End If

        SyncLock Me._indexLock
            Me._currentIndex -= 1
        End SyncLock
        Me.iscurrentimagedisplayed = False
        Me.ScheduleDisplay()
    End Sub

    Private Sub NavigateFirst()
        If Me._imageFiles.Length = 0 Then Return
        If Me._currentIndex = 0 Then Return
        SyncLock Me._indexLock : Me._currentIndex = 0 : End SyncLock
        Me.ScheduleDisplay()
    End Sub

    Private Sub NavigateLast()
        If Me._imageFiles.Length = 0 Then Return
        If Me._currentIndex = Me._imageFiles.Length - 1 Then Return
        SyncLock Me._indexLock : Me._currentIndex = Me._imageFiles.Length - 1 : End SyncLock
        Me.ScheduleDisplay()
    End Sub

    Private Sub UpdateNavigationButtons()
        Me.BtnPrev.Enabled = (Me._currentIndex > 0)
        Me.BtnNext.Enabled = (Me._currentIndex < Me._imageFiles.Length - 1)
    End Sub

    Private Sub RotateImage(clockwise As Boolean)
        If Me._imageFiles Is Nothing OrElse Me._imageFiles.Length = 0 Then
            Return
        End If

        Dim filePath As String

        SyncLock Me._indexLock
            If Me._currentIndex < 0 OrElse Me._currentIndex >= Me._imageFiles.Length Then
                Return
            End If

            filePath = Me._imageFiles(Me._currentIndex)
        End SyncLock

        Dim cached As CachedImage = Nothing
        If Not Me._cache.TryGetValue(filePath, cached) Then
            Return
        End If

        Dim sourceBmp As Bitmap = cached.Bitmap
        If sourceBmp Is Nothing Then
            Return
        End If

        If clockwise Then
            Me._rotationAngle += 90
        Else
            Me._rotationAngle -= 90
        End If

        Me._rotationAngle = ((Me._rotationAngle Mod 360) + 360) Mod 360


        Dim oldDisplayed As Image = Me.PicBox.Image

        Dim displayBmp As Bitmap = DirectCast(cached.Bitmap.Clone(), Bitmap)

        Select Case Me._rotationAngle

            Case 90
                displayBmp.RotateFlip(RotateFlipType.Rotate90FlipNone)

            Case 180
                displayBmp.RotateFlip(RotateFlipType.Rotate180FlipNone)

            Case 270
                displayBmp.RotateFlip(RotateFlipType.Rotate270FlipNone)

        End Select

        If Me._isActualSize Then
            Me.ExitActualSize()
        End If

        If Me._isZoomedMode Then
            Me.ResetZoom()
        End If

        Me.PicBox.Image = displayBmp

        If oldDisplayed IsNot Nothing AndAlso Not System.Object.ReferenceEquals(oldDisplayed, sourceBmp) Then
            oldDisplayed.Dispose()
        End If

        Me.PicBox.Invalidate()
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

    ''' <summary>
    ''' Updates the extended label instantly with pre-calculated metadata from the cache structure.
    ''' </summary>
    Private Sub UpdateLabelInfoExtendedDirect(filecountstr As String, fileName As String, cachedImg As CachedImage)
        Dim line1 As String = $"{filecountstr}  |  {cachedImg.Width} x {cachedImg.Height} x {cachedImg.Bpp} BPP  |  {cachedImg.FormattedSize}  |  Created: {cachedImg.CreatedText}  |  Modified: {cachedImg.ModifiedText}"
        Dim line2 As String = $"{fileName}"
        Try
            Me.lblInfo.Text = $"{line1}{Environment.NewLine}{line2}"
        Catch
            ' Last-resort silent safety catch
        End Try
    End Sub

    Private Function FormatFileSize(fileSize As Long) As String

        Dim bytes As Double = Convert.ToDouble(fileSize)
        Dim unit As String = "Bytes"

        If fileSize >= 1073741824 Then
            unit = "GB"
            bytes = fileSize / 1073741824.0

        ElseIf fileSize >= 1048576 Then
            unit = "MB"
            bytes = fileSize / 1048576.0

        ElseIf fileSize >= 1024 Then
            unit = "KB"
            bytes = fileSize / 1024.0

        End If

        Return If(unit = "Bytes", $"{fileSize:N0} Bytes", $"{bytes:F2} {unit}")
    End Function

    Private Sub StartWorker()
        Me._cancelWorker = False

        If Me.SHOW_PROGRESS_BAR Then
            Me.progressBar.Style = Windows.Forms.ProgressBarStyle.Marquee
            Me.progressBar.Visible = True
        End If
        If Me.SHOW_CACHE_LABEL Then
            Me.lblLoading.Text = "Loading initial images…"
            Me.lblLoading.Visible = True
        End If

        Me._workerThread = New Thread(AddressOf Me.WorkerLoop) With {
            .IsBackground = True,
            .Priority = ThreadPriority.BelowNormal
        }
        Me._workerThread.Start()

        Me._wakeSignal.Set()
    End Sub

    ''' <summary>
    ''' Signals the background cache worker to quit, waits for it to
    ''' finish (up to 1 s) and then disposes every cached bitmap.
    ''' Call this before loading a new directory or deleting all files.
    ''' </summary>
    Private Sub StopWorkerAndClearCache()
        ' Release PicBox.Image first so DrainPendingDisposal can dispose it if queued
        Me.PicBox.Image = Nothing

        Me._cancelWorker = True
        Me._wakeSignal.Set()

        If Me._workerThread IsNot Nothing AndAlso Me._workerThread.IsAlive Then
            Me._workerThread.Join(1000)
        End If

        Me._cancelWorker = False

        For Each kvp As KeyValuePair(Of String, CachedImage) In Me._cache
            kvp.Value.Bitmap?.Dispose()
        Next
        Me._cache.Clear()

        ' Drain any remaining pending disposals now that PicBox.Image is Nothing
        Dim b As Bitmap = Nothing
        Do While Me._pendingDisposal.TryDequeue(b)
            b?.Dispose()
        Loop

        If Me._isActualSize Then Me.ExitActualSize()
        If Me._isFullscreen Then Me.ExitFullscreen()
    End Sub

    Private Sub WorkerLoop()
        Do While Not Me._cancelWorker
            Me._wakeSignal.WaitOne(25)
            If Me._cancelWorker Then Exit Do

            Dim center As Integer
            Dim localImageFiles As String()

            SyncLock Me._indexLock
                center = Me._currentIndex
                localImageFiles = Me._imageFiles
            End SyncLock

            If localImageFiles Is Nothing OrElse localImageFiles.Length = 0 Then
                Continue Do
            End If

            Me.EnsureFileIndexMap(localImageFiles)

            Dim localFileIndexMap As Dictionary(Of String, Integer)
            SyncLock Me._indexLock
                localFileIndexMap = Me._fileIndexMap
            End SyncLock

            If localFileIndexMap Is Nothing OrElse localFileIndexMap.Count = 0 Then
                Continue Do
            End If

            Dim passVersion As Integer = Me._cacheWindowVersion

            If center = Me._lastWorkerCenter AndAlso passVersion = Me._lastWorkerCacheVersion Then
                Continue Do
            End If

            Me._lastWorkerCenter = center
            Me._lastWorkerCacheVersion = passVersion

            ' Reset progress throttle so the first image of a fresh pass always reports progress.
            Interlocked.Exchange(Me._pendingProgressInvoke, 0)

            ' ==========================================
            ' CHANGE 1: DEBOUNCE DELAY FOR RAPID SCROLLING
            ' ==========================================
            ' Sleep briefly to see if the user is holding down navigation keys (PgDn/PgUp).
            ' This stops the background thread from hammering the disk and GDI+ locks 
            ' with obsolete images while the user is actively skipping through files.
            Thread.Sleep(10)

            Dim postSleepCenter As Integer
            SyncLock Me._indexLock
                postSleepCenter = Me._currentIndex
            End SyncLock

            ' If the index shifted during our sleep, cancel this caching pass immediately
            If postSleepCenter <> center OrElse passVersion <> Me._cacheWindowVersion Then
                Continue Do
            End If
            ' ==========================================

            Dim lo As Integer = Math.Max(0, center - Me.CACHE_RADIUS_BACK)
            Dim hi As Integer = Math.Min(localImageFiles.Length - 1, center + Me.CACHE_RADIUS_FORWARD)

            Dim windowSize As Integer = hi - lo + 1
            Dim cachedInWindow As Integer = 0

            For i As Integer = lo To hi
                Dim filePath As String = localImageFiles(i)

                If Me._cache.ContainsKey(filePath) Then
                    cachedInWindow += 1
                End If
            Next

            Dim restartPass As Boolean = False

            ' Evict entries outside the asymmetric window
            For Each kvp As KeyValuePair(Of String, CachedImage) In Me._cache
                If Me._cancelWorker Then Exit Do
                If passVersion <> Me._cacheWindowVersion Then
                    restartPass = True
                    Exit For
                End If

                Dim idx As Integer = -1
                If Not localFileIndexMap.TryGetValue(kvp.Key, idx) OrElse idx < lo OrElse idx > hi Then
                    Dim removed As CachedImage = Nothing
                    If Me._cache.TryRemove(kvp.Key, removed) Then
                        If removed.Bitmap IsNot Nothing Then
                            Me._pendingDisposal.Enqueue(removed.Bitmap)
                        End If
                    End If
                End If
            Next

            If restartPass Then
                Continue Do
            End If

            If Me._cancelWorker Then Exit Do

            ' Build load order: center first, then expand outward
            Dim loadOrder As New List(Of Integer) From {center}
            Dim radius As Integer = 1

            Do While (center - radius >= lo) OrElse (center + radius <= hi)
                If center + radius <= hi Then loadOrder.Add(center + radius)
                If center - radius >= lo Then loadOrder.Add(center - radius)
                radius += 1
            Loop

            Dim loadedCount As Integer = cachedInWindow
            Dim totalToLoad As Integer = windowSize

            For Each idx As Integer In loadOrder
                If Me._cancelWorker Then Exit Do
                If passVersion <> Me._cacheWindowVersion Then
                    restartPass = True
                    Exit For
                End If

                Dim currentCenter As Integer
                Dim currentImageFiles As String()

                SyncLock Me._indexLock
                    currentCenter = Me._currentIndex
                    currentImageFiles = Me._imageFiles
                End SyncLock

                If currentCenter <> center Then
                    restartPass = True
                    Exit For
                End If

                If currentImageFiles Is Nothing OrElse currentImageFiles.Length = 0 Then
                    Exit Do
                End If

                If idx >= 0 AndAlso idx < currentImageFiles.Length Then
                    Dim filePath As String = currentImageFiles(idx)

                    If Not Me._cache.ContainsKey(filePath) Then
                        Try
                            If File.Exists(filePath) Then
                                Dim bmp As Bitmap = Me.LoadFast(filePath)
                                If bmp IsNot Nothing Then
                                    Dim fileInfo As New FileInfo(filePath)
                                    Dim formattedSize As String = Me.FormatFileSize(fileInfo.Length)
                                    Dim createdText As String = fileInfo.CreationTime.ToString("MM/dd/yyyy HH:mm:ss")
                                    Dim modifiedText As String = fileInfo.LastWriteTime.ToString("MM/dd/yyyy HH:mm:ss")
                                    Dim imageWidth As Integer = bmp.Width
                                    Dim imageHeight As Integer = bmp.Height
                                    Dim bpp As Integer = Image.GetPixelFormatSize(bmp.PixelFormat)

                                    Dim cachedImg As New CachedImage(bmp, imageWidth, imageHeight, bpp, formattedSize, createdText, modifiedText)

                                    If Me._cache.TryAdd(filePath, cachedImg) Then
                                        loadedCount += 1

                                        ' ==========================================
                                        ' CHANGE 2: SMALL THREAD YIELD
                                        ' ==========================================
                                        ' Relinquish CPU slice briefly to keep UI response lightning fast
                                        ' Thread.Sleep(1)
                                    Else
                                        bmp.Dispose()
                                    End If
                                End If
                            End If
                        Catch
                            ' Ignore I/O errors if the file is being modified or deleted
                        End Try
                    End If
                End If

                Dim pct As Integer = 0
                If totalToLoad > 0 Then
                    pct = CInt(Math.Round(loadedCount / totalToLoad * 100.0))
                End If

                Dim pctCopy As Integer = pct
                Dim ldCopy As Integer = loadedCount
                Dim totCopy As Integer = totalToLoad
                Dim centerCopy As Integer = center

                If Me.IsHandleCreated AndAlso Not Me._cancelWorker Then
                    If Interlocked.CompareExchange(Me._pendingProgressInvoke, 1, 0) = 0 Then
                        Me.BeginInvoke(
                        Sub()
                            Try
                                ' Si el índice actual ya cambió (por un borrado o movimiento), ignoramos este reporte viejo
                                Dim currentCenter1 As Integer
                                SyncLock Me._indexLock
                                    currentCenter1 = Me._currentIndex
                                End SyncLock

                                If currentCenter1 = centerCopy Then
                                    Me.ReportProgress(pctCopy, ldCopy, totCopy)
                                End If
                            Finally
                                Interlocked.Exchange(Me._pendingProgressInvoke, 0)
                            End Try
                        End Sub)
                    End If
                End If
            Next

            If restartPass Then
                Continue Do
            End If

            If Me.IsHandleCreated AndAlso Not Me._cancelWorker Then
                Me.BeginInvoke(Sub() Me.OnWindowLoaded())
            End If
        Loop
    End Sub

    Private Sub DisplayCurrentImage()
        If Me._imageFiles.Length = 0 Then Return

        Me.PicBox.Invalidate()
        If Me._isActualSize Then Me.ExitActualSize()
        Me._rotationAngle = 0
        Me.ResetZoom()

        Dim gen As Integer = Interlocked.Increment(Me._displayGeneration)
        Dim idx As Integer
        Dim filePath As String
        Dim filecountstr As String
        Dim fileName As String
        SyncLock Me._indexLock
            idx = Me._currentIndex
            filePath = Me._imageFiles(idx)
            filecountstr = If(Me._imageFiles.Length = 0, "0 / 0", $"{idx + 1:N0} / {Me._imageFiles.Length:N0}")
            fileName = Path.GetFileName(filePath)
        End SyncLock

        Dim cachedImg As New CachedImage()
        If Me._cache.TryGetValue(filePath, cachedImg) Then
            Try
                Me.PicBox.Image = cachedImg.Bitmap
                If Not File.Exists(filePath) Then
                    Me._isImageMissing = True
                    Me.PicBox.Invalidate()
                Else
                    Me._isImageMissing = False
                End If
                Me.iscurrentimagedisplayed = True
                SyncLock Me._indexLock
                    Me.BeginInvoke(Sub() Me.UpdateLabelInfoExtendedDirect(filecountstr, fileName, cachedImg))
                End SyncLock
                Thread.CurrentThread.Join(0)
            Catch
                Return
            End Try
            Me.Text = If(Me._isFullscreen, $"{My.Application.Info.Title} [FULLSCREEN] — {fileName}", $"{My.Application.Info.Title} — {fileName}")
        Else
            Me.lblInfo.Text = $"{filecountstr}  |  Loading metadata…{Environment.NewLine}{fileName}"
            Me.Text = If(Me._isFullscreen, $"{My.Application.Info.Title} [FULLSCREEN] — {fileName} (loading…)", $"{My.Application.Info.Title} — {fileName} (loading…)")

            Dim filePathCopy As String = filePath
            Dim genCopy As Integer = gen
            Dim filecountstrCopy As String = filecountstr

            ThreadPool.QueueUserWorkItem(
                Sub(state As Object)
                    If genCopy <> Me._displayGeneration Then Return
                    Dim loaded As Bitmap = Nothing
                    Try
                        loaded = Me.LoadFast(filePathCopy)
                    Catch
                    End Try
                    If loaded Is Nothing Then Return

                    If genCopy <> Me._displayGeneration Then
                        loaded.Dispose()
                        Return
                    End If

                    Dim fileInfo As New FileInfo(filePathCopy)
                    Dim formattedSize As String = Me.FormatFileSize(fileInfo.Length)
                    Dim createdText As String = fileInfo.CreationTime.ToString("MM/dd/yyyy HH:mm:ss")
                    Dim modifiedText As String = fileInfo.LastWriteTime.ToString("MM/dd/yyyy HH:mm:ss")
                    Dim imageWidth As Integer = loaded.Width
                    Dim imageHeight As Integer = loaded.Height
                    Dim bpp As Integer = Image.GetPixelFormatSize(loaded.PixelFormat)

                    Dim newCachedImg As New CachedImage(loaded, imageWidth, imageHeight, bpp, formattedSize, createdText, modifiedText)

                    If Not Me._cache.TryAdd(filePathCopy, newCachedImg) Then
                        loaded.Dispose()
                        loaded = Nothing
                        Me._cache.TryGetValue(filePathCopy, newCachedImg)
                    End If

                    If Me.IsHandleCreated Then
                        Me.BeginInvoke(
                            Sub()
                                If genCopy <> Me._displayGeneration Then Return
                                Try
                                    If newCachedImg.Bitmap IsNot Nothing Then
                                        Me.DrainPendingDisposal()
                                        Me.PicBox.Image = newCachedImg.Bitmap

                                        If Not File.Exists(filePathCopy) Then
                                            Me._isImageMissing = True
                                            Me.PicBox.Invalidate()
                                        Else
                                            Me._isImageMissing = False
                                        End If

                                        iscurrentimagedisplayed = True
                                        Me.DrainPendingDisposal()

                                        Me.UpdateLabelInfoExtendedDirect(filecountstrCopy, Path.GetFileName(filePathCopy), newCachedImg)
                                    End If
                                Catch
                                    Return
                                End Try
                                Me.Text = If(Me._isFullscreen, $"{My.Application.Info.Title} [FULLSCREEN] — {Path.GetFileName(filePathCopy)}", $"{My.Application.Info.Title} — {Path.GetFileName(filePathCopy)}")
                            End Sub)
                    End If
                End Sub)
        End If
    End Sub

    Private Sub ScheduleDisplay()
        Me.NavTimer.Stop()
        Me.NavTimer.Start()

        Me.UpdateNavigationButtons()
        If Me._imageFiles.Length > 0 Then
            Dim idx As Integer
            Dim filePath As String
            Dim filecountstr As String
            Dim fileName As String
            SyncLock Me._indexLock
                idx = Me._currentIndex
                filePath = Me._imageFiles(idx)
                filecountstr = If(Me._imageFiles.Length = 0, "0 / 0", $"{idx + 1:N0} / {Me._imageFiles.Length:N0}")
                fileName = Path.GetFileName(filePath)
            End SyncLock

            Dim cachedData As New CachedImage()
            If Me._cache.TryGetValue(filePath, cachedData) Then
                Me.BeginInvoke(Sub() Me.UpdateLabelInfoExtendedDirect(filecountstr, fileName, cachedData))
            Else
                Me.lblInfo.Text = $"{filecountstr}  |  Loading metadata…{Environment.NewLine}{fileName}"
            End If
        End If
    End Sub

    ''' <summary>
    ''' Disposes bitmaps evicted from the cache by the worker thread.
    ''' Those bitmaps cannot be disposed directly on the worker because they
    ''' might still be referenced by PicBox.Image, which would cause a GDI+
    ''' ArgumentException during painting.  Must be called on the UI thread.
    ''' </summary>
    Private Sub DrainPendingDisposal()
        Dim stillNeeded As New System.Collections.Generic.List(Of Bitmap)()
        Dim b As Bitmap = Nothing

        Do While Me._pendingDisposal.TryDequeue(b)
            If b Is Nothing Then Continue Do

            If Object.ReferenceEquals(b, Me.PicBox.Image) Then
                stillNeeded.Add(b) ' Still displayed — defer until PicBox.Image changes
            Else
                b.Dispose()
            End If
        Loop

        ' Re-enqueue anything we could not dispose yet
        For Each bmp As Bitmap In stillNeeded
            Me._pendingDisposal.Enqueue(bmp)
        Next
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

            Dim map As New Dictionary(Of String, Integer)(files.Length, StringComparer.OrdinalIgnoreCase)

            For i As Integer = 0 To files.Length - 1
                map(files(i)) = i
            Next

            Me._fileIndexMap = map
            Me._fileIndexMapSource = files
        End SyncLock
    End Sub

    Private Function GetInsertionIndex(files As String(), anchorFilePath As String) As Integer
        If files Is Nothing OrElse files.Length = 0 Then
            Return 0
        End If

        Dim lo As Integer = 0
        Dim hi As Integer = files.Length

        Do While lo < hi
            Dim mid As Integer = lo + ((hi - lo) \ 2)
            Dim cmp As Integer = StrCmpLogicalW(files(mid), anchorFilePath)

            If cmp < 0 Then
                lo = mid + 1
            Else
                hi = mid
            End If
        Loop

        Return If(lo >= files.Length, files.Length - 1, lo)
    End Function

#End Region

End Class

#End Region
