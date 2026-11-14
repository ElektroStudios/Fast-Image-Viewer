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


' Contains the form's navigation coordination.
Partial Public NotInheritable Class Form1 ' NOSONAR

    Private Sub NavigateNext()
        If Me.PAUSE_BETWEEN_FASTFORWARD_MILLISECONDS > 0 AndAlso (Date.Now - Me._lastNavTime).TotalMilliseconds < Me.PAUSE_BETWEEN_FASTFORWARD_MILLISECONDS Then
            Return
        End If
        Me._lastNavTime = Date.Now

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
        If Me.PAUSE_BETWEEN_FASTFORWARD_MILLISECONDS > 0 AndAlso (Date.Now - Me._lastNavTime).TotalMilliseconds < Me.PAUSE_BETWEEN_FASTFORWARD_MILLISECONDS Then
            Return
        End If
        Me._lastNavTime = Date.Now

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

    Private Sub StartSlideshow()

        Me._isSlideshowActive = True
        Me.MenuItemStartSlideshow.Text = "S&top Slideshow"
        Me.SlideshowTimer.Interval = Me.SLIDESHOW_INTERVAL_SECONDS * 1000
        Me.SlideshowTimer.Start()
        If Not Me._isFullscreen Then
            Me.EnterFullscreen()
        End If
    End Sub

    Private Sub StopSlideshow()
        Me._isSlideshowActive = False
        Me.MenuItemStartSlideshow.Text = "&Start Slideshow"
        Me.SlideshowTimer.Stop()
    End Sub
End Class
