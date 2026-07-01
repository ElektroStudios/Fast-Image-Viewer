
#Region " Option Statements "

Option Strict On
Option Explicit On
Option Infer Off

#End Region

#Region " Imports "

Imports System.Drawing

#End Region

#Region " CachedImage "

Friend Structure CachedImage

    Friend ReadOnly Bitmap As Bitmap
    Friend ReadOnly Width As Integer
    Friend ReadOnly Height As Integer
    Friend ReadOnly Bpp As Integer
    Friend ReadOnly FormattedSize As String
    Friend ReadOnly CreatedText As String
    Friend ReadOnly ModifiedText As String

    Friend Sub New(bmp As Bitmap, width As Integer, height As Integer, bpp As Integer, sizeText As String, created As String, modified As String)
        Me.Bitmap = bmp
        Me.Width = width
        Me.Height = height
        Me.Bpp = bpp
        Me.FormattedSize = sizeText
        Me.CreatedText = created
        Me.ModifiedText = modified
    End Sub

End Structure

#End Region
