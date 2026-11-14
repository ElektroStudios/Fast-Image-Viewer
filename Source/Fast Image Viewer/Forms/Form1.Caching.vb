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


' Contains the form's caching coordination.
Partial Public NotInheritable Class Form1

    Private Sub StartWorker()
        ' Folder refreshes reuse the current worker instead of leaking another thread.
        If Me._workerThread IsNot Nothing AndAlso Me._workerThread.IsAlive Then
            Me._wakeSignal.Set()
            Return
        End If
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

    Private Sub StopWorkerAndClearCache()
        ' Release PicBox.Image first so DrainPendingDisposal can dispose it if queued
        Interlocked.Increment(Me._displayGeneration)
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

            ' Preserve the existing debounce before each cache pass.
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

            Dim window As New CacheWindow(localImageFiles.Length, center, Me.CACHE_RADIUS_BACK, Me.CACHE_RADIUS_FORWARD)
            Dim lo As Integer = window.First
            Dim hi As Integer = window.Last

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
            Dim loadOrder As List(Of Integer) = window.CreateLoadOrder()

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

                                Dim cachedImg As CachedImage = CachedImageLoader.Load(filePath, Me._screenWidth, Me._screenHeight, Me.DISABLE_IMAGERESIZING_ON_LOAD_FASTLOAD, Me.UPSCALE_SMALL_IMAGES_TO_FIT_WINDOW)
                                Dim bmp As Bitmap = cachedImg.Bitmap
                                If bmp IsNot Nothing Then
                                    If Me._cache.TryAdd(filePath, cachedImg) Then
                                        loadedCount += 1

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
                                ' Ignore progress for a selection that has already changed.
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

    ''' <summary>Releases evicted images on the UI thread while retaining the displayed image.</summary>
    Private Sub DrainPendingDisposal()
        Me._pendingDisposal.Drain(TryCast(Me.PicBox.Image, Bitmap))
    End Sub
End Class
