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


' Contains the form's settings event handlers.
Partial Public NotInheritable Class Form1

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

        ' Validate the final path structure for Windows.
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

    Private Sub MenuItemRememberWindowState_Click(sender As Object, e As EventArgs) Handles MenuItemRememberWindowState.Click
        My.Settings.RememberWindowState = Me.MenuItemRememberWindowState.Checked
        My.Settings.Save()
    End Sub

    Private Sub MenuItemUpscaleSmallImages_Click(sender As Object, e As EventArgs) Handles MenuItemUpscaleSmallImages.Click

        My.Settings.UpscaleSmallImagestoFitWindow = Me.MenuItemUpscaleSmallImages.Checked
        My.Settings.Save()

        If Me._imageFiles Is Nothing OrElse Me._imageFiles.Length = 0 Then
            Exit Sub
        End If

        Me.StopWorkerAndClearCache()

        SyncLock Me._indexLock
            Me._lastWorkerCenter = -1
        End SyncLock

        Me.StartWorker()
        Me.DisplayCurrentImage()
    End Sub

    Private Sub MenuItemDisableFastLoadResize_Click(sender As Object, e As EventArgs) Handles MenuItemDisableFastLoadResize.Click

        My.Settings.DisableImageResizingOnLoadFastLoad = Me.MenuItemDisableFastLoadResize.Checked
        My.Settings.Save()

        If Me._imageFiles Is Nothing OrElse Me._imageFiles.Length = 0 Then
            Exit Sub
        End If

        Me.StopWorkerAndClearCache()

        SyncLock Me._indexLock
            Me._lastWorkerCenter = -1
        End SyncLock

        Me.StartWorker()
        Me.DisplayCurrentImage()
    End Sub

    Private Sub NumFastForwardPauseCtrl_ValueChanged(sender As Object, e As EventArgs)

        My.Settings.PauseBetweenFastforwardMilliseconds = CLng(Me.NumFastForwardPauseCtrl.Value)
        My.Settings.Save()
    End Sub

    Private Sub NumSlideshowIntervalCtrl_ValueChanged(sender As Object, e As EventArgs)

        My.Settings.SlideshowInternalSeconds = CInt(Me.NumSlideshowIntervalCtrl.Value)
        My.Settings.Save()

        If Me._isSlideshowActive Then
            Me.SlideshowTimer.Interval = Me.SLIDESHOW_INTERVAL_SECONDS * 1000 ' ms
        End If
    End Sub

    Private Sub MenuItemStartSlideshow_Click(sender As Object, e As EventArgs) Handles MenuItemStartSlideshow.Click
        If Me._isSlideshowActive Then
            Me.StopSlideshow()
        Else
            Me.StartSlideshow()
        End If
    End Sub

    Private Sub SlideshowTimer_Tick(sender As Object, e As EventArgs) Handles SlideshowTimer.Tick
        If Me._imageFiles.Length = 0 Then
            Me.StopSlideshow()
            Return
        End If

        If Me._currentIndex = Me._imageFiles.Length - 1 Then
            Me.NavigateFirst()
        Else
            Me.NavigateNext()
        End If
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

    Private ReadOnly Property UPSCALE_SMALL_IMAGES_TO_FIT_WINDOW As Boolean
        Get
            Return My.Settings.UpscaleSmallImagestoFitWindow
        End Get
    End Property

    Private ReadOnly Property SLIDESHOW_INTERVAL_SECONDS As Integer
        Get
            Return My.Settings.SlideshowInternalSeconds
        End Get
    End Property

    Private ReadOnly Property PAUSE_BETWEEN_FASTFORWARD_MILLISECONDS As Long
        Get
            Return My.Settings.PauseBetweenFastforwardMilliseconds
        End Get
    End Property

    Private ReadOnly Property DISABLE_IMAGERESIZING_ON_LOAD_FASTLOAD As Boolean
        Get
            Return My.Settings.DisableImageResizingOnLoadFastLoad
        End Get
    End Property

    Private ReadOnly Property REMEMBER_WINDOW_STATE As Boolean
        Get
            Return My.Settings.RememberWindowState
        End Get
    End Property

#End Region

End Class
