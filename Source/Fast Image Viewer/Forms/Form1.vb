
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

''' <summary>Coordinates the viewer window and its background image cache.</summary>
Partial Public NotInheritable Class Form1 : Inherits Form ' NOSONAR

#Region " Fields "

    Private FolderPath As String

    Private _isZoomedMode As Boolean = False
    Private _currentZoomFactor As Double = 1.0
    Private _baseZoomFactor As Double = 1.0

    ' Evicted bitmaps from the worker that have not yet been disposed
    Private ReadOnly _pendingDisposal As New DeferredDisposalQueue(Of Bitmap)()

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

    ' Coalesces queued progress callbacks to keep the UI message queue responsive.
    Private _pendingProgressInvoke As Integer = 0

    Private _isSlideshowActive As Boolean = False
    Private WithEvents SlideshowTimer As New System.Windows.Forms.Timer()
    Private _lastNavTime As Date = Date.MinValue

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
    Private WithEvents MenuItemRememberWindowState As ToolStripMenuItem
    Private WithEvents MenuItemUpscaleSmallImages As ToolStripMenuItem

    Private WithEvents MenuItemAdvanced As ToolStripMenuItem
    Private WithEvents MenuItemAdvancedWarning As ToolStripLabel
    Private WithEvents MenuItemDisableFastLoadResize As ToolStripMenuItem
    Private NumFastForwardPauseHost As ToolStripControlHost
    Private NumFastForwardPauseCtrl As NumericUpDown

    Private WithEvents MenuItemSlideshow As ToolStripMenuItem
    Private WithEvents MenuItemStartSlideshow As ToolStripMenuItem
    Private NumSlideshowIntervalHost As ToolStripControlHost
    Private NumSlideshowIntervalCtrl As NumericUpDown

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

        If Me.REMEMBER_WINDOW_STATE AndAlso My.Settings.SavedWindowSize.Width > 0 Then
            Me.Location = My.Settings.SavedWindowLocation
            Me.Size = My.Settings.SavedWindowSize
            Me.WindowState = If(My.Settings.SavedWindowMaximized, FormWindowState.Maximized, FormWindowState.Normal)
        Else
            Me.WindowState = FormWindowState.Maximized
        End If

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

        If Me.REMEMBER_WINDOW_STATE Then
            My.Settings.SavedWindowMaximized = (Me.WindowState = FormWindowState.Maximized)
            If Me.WindowState = FormWindowState.Normal Then
                My.Settings.SavedWindowLocation = Me.Location
                My.Settings.SavedWindowSize = Me.Size
            Else
                My.Settings.SavedWindowLocation = Me.RestoreBounds.Location
                My.Settings.SavedWindowSize = Me.RestoreBounds.Size
            End If
            My.Settings.Save()
        End If

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

            Case Keys.L
                Me.RotateImage(False)

            Case Keys.R
                Me.RotateImage(True)

        End Select

        MyBase.OnKeyDown(e)
    End Sub

    ''' <summary>
    ''' Resets the navigation throttle when a navigation key is released.
    ''' </summary>
    ''' <param name="e">The key event arguments.</param>
    Protected Overrides Sub OnKeyUp(e As KeyEventArgs)

        Select Case e.KeyCode
            Case Keys.PageDown, Keys.PageUp, Keys.Back,
                 Keys.Left, Keys.Right

                Me._lastNavTime = Date.MinValue

        End Select

        MyBase.OnKeyUp(e)

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

            Case Keys.Delete, Keys.Delete Or Keys.Shift
                If TypeOf Me.ActiveControl Is TextBoxBase OrElse (Me.ActiveControl IsNot Nothing AndAlso Me.ActiveControl.GetType().Name.Contains("TextBox")) Then
                    Return MyBase.ProcessCmdKey(msg, keyData)
                End If

                Dim permanentDeletion As Boolean = (keyData And Keys.Shift) = Keys.Shift

                If permanentDeletion Then
                    Me.DeleteCurrentImage(permanentDeletion:=True)
                ElseIf My.Settings.DelKeyMovesImageToCustomFolder Then
                    Me.MoveCurrentImageToCustomDir()
                Else
                    Me.DeleteCurrentImage(permanentDeletion:=False)
                End If
                Return True

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

End Class

#End Region
