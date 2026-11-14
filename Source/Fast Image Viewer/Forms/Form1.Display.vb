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


' Contains the form's display coordination.
Partial Public NotInheritable Class Form1 ' NOSONAR

    Private Function LoadOriginal(filePath As String) As Bitmap
        Return BitmapLoader.LoadOriginal(filePath)
    End Function

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
                Me.PicBox.SizeMode =
                    If(Not Me.UPSCALE_SMALL_IMAGES_TO_FIT_WINDOW AndAlso
                           cachedImg.Bitmap.Width <= Me.PicBox.ClientSize.Width AndAlso
                           cachedImg.Bitmap.Height <= Me.PicBox.ClientSize.Height AndAlso
                           cachedImg.Bitmap.Width <= Me._screenWidth - 2 AndAlso
                           cachedImg.Bitmap.Height <= Me._screenHeight - 2,
                    PictureBoxSizeMode.CenterImage,
                    PictureBoxSizeMode.Zoom)

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
            Me.Text = If(Me._isFullscreen, $"{My.Application.Info.ProductName} [FULLSCREEN] — {fileName}", $"{My.Application.Info.ProductName} — {fileName}")
        Else
            Me.lblInfo.Text = $"{filecountstr}  |  Loading metadata…{Environment.NewLine}{fileName}"
            Me.Text = If(Me._isFullscreen, $"{My.Application.Info.ProductName} [FULLSCREEN] — {fileName} (loading…)", $"{My.Application.Info.ProductName} — {fileName} (loading…)")

            Dim filePathCopy As String = filePath
            Dim genCopy As Integer = gen
            Dim filecountstrCopy As String = filecountstr

            ThreadPool.QueueUserWorkItem(
                Sub(state As Object)
                    If genCopy <> Me._displayGeneration Then Return

                    Dim newCachedImg As CachedImage
                    Try
                        newCachedImg = CachedImageLoader.Load(filePathCopy, Me._screenWidth, Me._screenHeight, Me.DISABLE_IMAGERESIZING_ON_LOAD_FASTLOAD, Me.UPSCALE_SMALL_IMAGES_TO_FIT_WINDOW)
                    Catch
                        ' A file can disappear or become unreadable while metadata is being read.
                        Return
                    End Try
                    Dim loaded As Bitmap = newCachedImg.Bitmap
                    If loaded Is Nothing Then Return
                    If genCopy <> Me._displayGeneration Then
                        loaded.Dispose()
                        Return
                    End If

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

                                        Me.PicBox.SizeMode =
                                        If(Not Me.UPSCALE_SMALL_IMAGES_TO_FIT_WINDOW AndAlso
                                               newCachedImg.Bitmap.Width <= Me.PicBox.ClientSize.Width AndAlso
                                               newCachedImg.Bitmap.Height <= Me.PicBox.ClientSize.Height AndAlso
                                               newCachedImg.Bitmap.Width <= Me._screenWidth - 2 AndAlso
                                               newCachedImg.Bitmap.Height <= Me._screenHeight - 2,
                                            PictureBoxSizeMode.CenterImage,
                                            PictureBoxSizeMode.Zoom)

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
                                Me.Text = If(Me._isFullscreen, $"{My.Application.Info.ProductName} [FULLSCREEN] — {Path.GetFileName(filePathCopy)}", $"{My.Application.Info.ProductName} — {Path.GetFileName(filePathCopy)}")
                            End Sub)
                    End If
                End Sub)
        End If
    End Sub

    Private Sub UpdateLabelInfoExtendedDirect(filecountstr As String, fileName As String, cachedImg As CachedImage)
        Dim line1 As String = $"{filecountstr}  |  {cachedImg.Width} x {cachedImg.Height} x {cachedImg.Bpp} BPP  |  {cachedImg.FormattedSize}  |  Created: {cachedImg.CreatedText}  |  Modified: {cachedImg.ModifiedText}"
        Dim line2 As String = $"{fileName}"
        Try
            Me.lblInfo.Text = $"{line1}{Environment.NewLine}{line2}"
        Catch
            ' Last-resort silent safety catch
        End Try
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
End Class
