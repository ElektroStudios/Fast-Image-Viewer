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


' Contains the form's viewmodes coordination.
Partial Public NotInheritable Class Form1

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
        Me.Text = $"{My.Application.Info.ProductName} [FULLSCREEN] — {Path.GetFileName(Me._imageFiles(Me._currentIndex))}"
    End Sub

    Private Sub ExitFullscreen()
        Me.StopSlideshow()
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
        Me.Text = $"{My.Application.Info.ProductName} — {Path.GetFileName(Me._imageFiles(Me._currentIndex))}"
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

        Select Case Me._rotationAngle
            Case 90
                bmp.RotateFlip(RotateFlipType.Rotate90FlipNone)
            Case 180
                bmp.RotateFlip(RotateFlipType.Rotate180FlipNone)
            Case 270
                bmp.RotateFlip(RotateFlipType.Rotate270FlipNone)
        End Select

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

        Dim pnlClientW As Integer = Me.PanScroll.ClientSize.Width
        Dim pnlClientH As Integer = Me.PanScroll.ClientSize.Height

        Dim targetX As Integer = If(bmp.Width < pnlClientW, (pnlClientW - bmp.Width) \ 2, 0)
        Dim targetY As Integer = If(bmp.Height < pnlClientH, (pnlClientH - bmp.Height) \ 2, 0)

        pbActual.Location = New Point(targetX, targetY)

        Dim scrollX As Integer = Math.Max(0, (bmp.Width - pnlClientW) \ 2)
        Dim scrollY As Integer = Math.Max(0, (bmp.Height - pnlClientH) \ 2)
        Me.PanScroll.AutoScrollPosition = New Point(scrollX, scrollY)

        Me.PanScroll.Focus()
        Me._isActualSize = True
        Me.Text = $"{My.Application.Info.ProductName} [1:1] — {Path.GetFileName(filePath)}"
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
            Me.Text = $"{My.Application.Info.ProductName} — {Path.GetFileName(Me._imageFiles(Me._currentIndex))}"
        End If

        Me.ResetZoom()
    End Sub

    Private Sub ScrollActualSize(dx As Integer, dy As Integer)
        Dim cur As New Point(-Me.PanScroll.AutoScrollPosition.X,
                                     -Me.PanScroll.AutoScrollPosition.Y)
        Me.PanScroll.AutoScrollPosition = New Point(cur.X + dx, cur.Y + dy)
    End Sub

    Private Sub EnableDoubleBuffering(ByVal targetControl As Control)

        Dim doubleBufferedProperty As Reflection.PropertyInfo =
            GetType(Control).GetProperty("DoubleBuffered", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance)

        doubleBufferedProperty?.SetValue(targetControl, True, Nothing)
    End Sub

    Private Sub AdjustZoom(delta As Double)

        If Me._imageFiles Is Nothing OrElse Me._imageFiles.Length = 0 Then
            Return
        End If

        If Me.PicBox.Image Is Nothing AndAlso Me._actualBitmap Is Nothing Then
            Return
        End If

        If Me._isFullscreen Then
            Me.ExitFullscreen()
        End If

        Dim baseBmp As Bitmap = If(Me._isActualSize, Me._actualBitmap, DirectCast(Me.PicBox.Image, Bitmap))

        If baseBmp Is Nothing Then
            Return
        End If

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

                ' Do not enlarge small images when initializing zoom mode.
                Me._baseZoomFactor = Math.Min(scaleX, scaleY)

                If Me._baseZoomFactor > 1.0 Then
                    Me._baseZoomFactor = 1.0
                End If
            End If

            Me._currentZoomFactor = Me._baseZoomFactor
            Me._isZoomedMode = True
        End If

        Dim newZoom As Double = Me._currentZoomFactor + delta

        If newZoom < 0.1 Then newZoom = 0.1
        If newZoom > 15.0 Then newZoom = 15.0

        ' Absolute cap to prevent GDI+ memory crash and black screen on large images.
        Dim maxDimension As Double = 30000.0
        If Convert.ToDouble(baseWidth) * newZoom > maxDimension Then newZoom = maxDimension / Convert.ToDouble(baseWidth)
        If Convert.ToDouble(baseHeight) * newZoom > maxDimension Then newZoom = maxDimension / Convert.ToDouble(baseHeight)

        ' Exit zoom mode cleanly if we return to the exact starting zoom factor
        If Math.Abs(newZoom - Me._baseZoomFactor) < 0.05 Then
            Me.ResetZoom()
            Return
        End If

        Me._currentZoomFactor = newZoom

        Dim newWidth As Integer = Convert.ToInt32(Math.Round(Convert.ToDouble(baseWidth) * Me._currentZoomFactor))
        Dim newHeight As Integer = Convert.ToInt32(Math.Round(Convert.ToDouble(baseHeight) * Me._currentZoomFactor))

        Dim pbScroll As PictureBox = Nothing

        ' Prevents ghosting of the previous image in the scrollable area when zooming in or out.
        NativeMethods.SendMessage(Me.Handle, &HB, 0, 0)

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

        Dim pnlClientW As Integer = Me.PanScroll.ClientSize.Width
        Dim pnlClientH As Integer = Me.PanScroll.ClientSize.Height
        Dim viewCenterX As Double = pnlClientW / 2.0
        Dim viewCenterY As Double = pnlClientH / 2.0

        Dim oldW As Double = Convert.ToDouble(pbScroll.Width)
        Dim oldH As Double = Convert.ToDouble(pbScroll.Height)

        Dim imgX As Double = viewCenterX - pbScroll.Left
        Dim imgY As Double = viewCenterY - pbScroll.Top

        Dim ratioX As Double = If(oldW > 0, imgX / oldW, 0.5)
        Dim ratioY As Double = If(oldH > 0, imgY / oldH, 0.5)

        Dim staleRegion As Rectangle = Rectangle.Empty
        Dim needsStaleWipe As Boolean = False

        ' Update the scrollable image before calculating its centered position.

        If Me.PicBox.Visible Then
            staleRegion = Me.PicBox.Bounds
            needsStaleWipe = True
            Me.PicBox.Visible = False
            Me.PanScroll.Visible = True
        End If

        pbScroll.Image = baseBmp
        pbScroll.Size = New Size(newWidth, newHeight)
        Me.PanScroll.AutoScrollMinSize = pbScroll.Size

        If needsStaleWipe AndAlso Me.PicBox.Parent IsNot Nothing Then
            Me.PicBox.Parent.Invalidate(staleRegion, True)
        End If

        pnlClientW = Me.PanScroll.ClientSize.Width
        pnlClientH = Me.PanScroll.ClientSize.Height
        viewCenterX = pnlClientW / 2.0
        viewCenterY = pnlClientH / 2.0

        ' Center images smaller than the viewport and preserve the zoom focus.

        Dim targetX As Integer = pbScroll.Location.X
        Dim targetY As Integer = pbScroll.Location.Y

        If newWidth < pnlClientW Then
            targetX = (pnlClientW - newWidth) \ 2
        ElseIf pbScroll.Location.X > 0 Then
            targetX = 0
        End If

        If newHeight < pnlClientH Then
            targetY = (pnlClientH - newHeight) \ 2
        ElseIf pbScroll.Location.Y > 0 Then
            targetY = 0
        End If

        If pbScroll.Location.X <> targetX OrElse pbScroll.Location.Y <> targetY Then
            pbScroll.Location = New Point(targetX, targetY)
        End If

        If newWidth >= pnlClientW OrElse newHeight >= pnlClientH Then

            Dim newScrollX As Integer = CInt(Math.Round((ratioX * newWidth) - viewCenterX))
            Dim newScrollY As Integer = CInt(Math.Round((ratioY * newHeight) - viewCenterY))

            Me.PanScroll.AutoScrollPosition = New Point(Math.Max(0, newScrollX), Math.Max(0, newScrollY))

        End If

        ' Prevents ghosting of the previous image in the scrollable area when zooming in or out.
        NativeMethods.SendMessage(Me.Handle, &HB, 1, 0)
        Me.Invalidate(invalidateChildren:=True)
    End Sub

    Private Sub ResetZoom()
        Me._isZoomedMode = False
        Me._currentZoomFactor = 1.0
        Me._baseZoomFactor = 1.0

        ' If we are zooming from Fit mode (not ActualSize), 
        ' we must destroy the dynamic zoom box and restore the standard PicBox.
        If Not Me._isActualSize AndAlso Me.PanScroll.Visible Then
            Me.PanScroll.AutoScrollMinSize = Size.Empty
            'Me.PanScroll.Controls.Clear()
            Me.PanScroll.Visible = False
            Me.PicBox.Visible = True
        End If
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

        ' ═══════════════════════════════════════════════════════════════
        ' IN-PLACE ROTATION TO PREVENT SCROLL RESET AND GLITCHES
        ' ═══════════════════════════════════════════════════════════════
        If (Me._isActualSize OrElse Me._isZoomedMode) AndAlso Me.PanScroll.Controls.Count > 0 Then
            Dim pb As PictureBox = DirectCast(Me.PanScroll.Controls(0), PictureBox)

            Dim pnlW As Integer = Me.PanScroll.ClientSize.Width
            Dim pnlH As Integer = Me.PanScroll.ClientSize.Height

            ' 1. Capture exact proportions before rotation
            Dim currentScrollX As Integer = Math.Abs(Me.PanScroll.AutoScrollPosition.X)
            Dim currentScrollY As Integer = Math.Abs(Me.PanScroll.AutoScrollPosition.Y)
            Dim imgCenterX As Double = currentScrollX + (pnlW / 2.0)
            Dim imgCenterY As Double = currentScrollY + (pnlH / 2.0)

            Dim propX As Double = If(pb.Width > 0, imgCenterX / Convert.ToDouble(pb.Width), 0.5)
            Dim propY As Double = If(pb.Height > 0, imgCenterY / Convert.ToDouble(pb.Height), 0.5)

            ' Swap proportions since Width and Height are inverted during 90-degree rotations
            Dim newPropX As Double = propY
            Dim newPropY As Double = propX

            Dim oldPbImage As Image = pb.Image

            If Me._actualBitmap IsNot Nothing Then Me._actualBitmap.Dispose()
            Me._actualBitmap = New Bitmap(displayBmp)

            ' 2. Calculate the new dimensions based on current zoom factor
            Dim newWidth As Integer
            Dim newHeight As Integer

            If Me._isZoomedMode Then
                Dim pnlClientW As Double = Convert.ToDouble(Me.PicBox.ClientSize.Width)
                Dim pnlClientH As Double = Convert.ToDouble(Me.PicBox.ClientSize.Height)
                Dim scaleX As Double = pnlClientW / Convert.ToDouble(displayBmp.Width)
                Dim scaleY As Double = pnlClientH / Convert.ToDouble(displayBmp.Height)
                Me._baseZoomFactor = Math.Min(scaleX, scaleY)
                newWidth = Convert.ToInt32(Math.Round(Convert.ToDouble(displayBmp.Width) * Me._currentZoomFactor))
                newHeight = Convert.ToInt32(Math.Round(Convert.ToDouble(displayBmp.Height) * Me._currentZoomFactor))
            Else
                newWidth = displayBmp.Width
                newHeight = displayBmp.Height
            End If

            ' 3. Apply new size to expand the AutoScroll boundaries BEFORE touching Location
            pb.Size = New Size(newWidth, newHeight)
            Me.PanScroll.AutoScrollMinSize = pb.Size

            ' 4. Apply your exact original Location logic
            Dim targetX As Integer = pb.Location.X
            Dim targetY As Integer = pb.Location.Y

            If newWidth < pnlW Then
                targetX = (pnlW - newWidth) \ 2
            ElseIf pb.Location.X > 0 Then
                targetX = 0
            End If

            If newHeight < pnlH Then
                targetY = (pnlH - newHeight) \ 2
            ElseIf pb.Location.Y > 0 Then
                targetY = 0
            End If

            If pb.Location.X <> targetX OrElse pb.Location.Y <> targetY Then
                pb.Location = New Point(targetX, targetY)
            End If

            ' 5. Apply new image
            pb.Image = displayBmp

            ' 6. Re-apply the inverted scroll accurately
            If newWidth >= pnlW OrElse newHeight >= pnlH Then
                Dim newImgCenterX As Double = newPropX * newWidth
                Dim newImgCenterY As Double = newPropY * newHeight

                Dim newScrollX As Integer = CInt(Math.Round(newImgCenterX - (pnlW / 2.0)))
                Dim newScrollY As Integer = CInt(Math.Round(newImgCenterY - (pnlH / 2.0)))
                Me.PanScroll.AutoScrollPosition = New Point(Math.Max(0, newScrollX), Math.Max(0, newScrollY))
            End If

            ' Clean memory avoiding the Parameter Not Valid crash
            Dim oldPicBoxImage As Image = Me.PicBox.Image
            Me.PicBox.Image = displayBmp

            If oldPbImage IsNot Nothing AndAlso Not Object.ReferenceEquals(oldPbImage, sourceBmp) Then oldPbImage.Dispose()
            If oldPicBoxImage IsNot Nothing AndAlso Not Object.ReferenceEquals(oldPicBoxImage, sourceBmp) Then oldPicBoxImage.Dispose()
            Return
        End If

        ' ═══════════════════════════════════════════════════════════════
        ' STANDARD FIT MODE ROTATION (No active scrollbars)
        ' ═══════════════════════════════════════════════════════════════

        If Me._isZoomedMode Then
            Me.ResetZoom()
        End If

        Me.PicBox.SizeMode =
            If(Not Me.UPSCALE_SMALL_IMAGES_TO_FIT_WINDOW AndAlso
                   displayBmp.Width <= Me.PicBox.ClientSize.Width AndAlso
                   displayBmp.Height <= Me.PicBox.ClientSize.Height AndAlso
                   displayBmp.Width <= Me._screenWidth - 2 AndAlso
                   displayBmp.Height <= Me._screenHeight - 2,
               PictureBoxSizeMode.CenterImage,
               PictureBoxSizeMode.Zoom)

        Me.PicBox.Image = displayBmp

        If oldDisplayed IsNot Nothing AndAlso Not System.Object.ReferenceEquals(oldDisplayed, sourceBmp) Then
            oldDisplayed.Dispose()
        End If

        Me.PicBox.Invalidate()
    End Sub
End Class
