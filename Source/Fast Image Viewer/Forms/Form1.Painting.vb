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


' Contains the form's painting event handlers.
Partial Public NotInheritable Class Form1 ' NOSONAR

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

End Class
