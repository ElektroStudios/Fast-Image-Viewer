
#Region " Option Statements "

Option Strict On
Option Explicit On
Option Infer Off

#End Region

#Region " Imports "

Imports System.Drawing
Imports System.Windows.Forms

#End Region

#Region " SafePictureBox "

Public Class SafePictureBox : Inherits PictureBox

    Public Sub New()
        Me.DoubleBuffered = True
    End Sub

    Protected Overrides Sub OnPaint(ByVal pe As PaintEventArgs)
        Try
            MyBase.OnPaint(pe)
        Catch ex As ArgumentException
            ' The displayed bitmap was disposed mid-paint (GDI+ concurrency).
            ' Clear the stale reference so the next paint cycle works cleanly
            ' and the loading overlay (subscribed to the Paint event) can draw.
            Me.Image = Nothing   ' Also calls Invalidate internally
            Using bgBrush As New SolidBrush(Me.BackColor)
                pe.Graphics.FillRectangle(bgBrush, Me.ClientRectangle)
            End Using
            Me.Invalidate()      ' Guarantee a new paint fires (overlay will be visible)
        End Try
    End Sub

End Class

#End Region
