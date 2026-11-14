Option Strict On
Option Explicit On
Option Infer Off

Imports System.Drawing

''' <summary>Calculates decoded bitmap sizes without allocating image buffers.</summary>
Friend NotInheritable Class ImageSizing
    Private Sub New()
    End Sub

    ''' <summary>Fits an image to the screen while preserving the resize preferences.</summary>
    Friend Shared Function GetLoadSize(width As Integer, height As Integer, screenWidth As Integer, screenHeight As Integer, disableResize As Boolean, upscale As Boolean) As Size
        If width <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(width))
        If height <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(height))
        If screenWidth <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(screenWidth))
        If screenHeight <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(screenHeight))
        Dim scale As Double = Math.Min(screenWidth / width, screenHeight / height)
        If disableResize OrElse (scale >= 1.0 AndAlso Not upscale) Then Return New Size(width, height)
        Return New Size(Math.Max(1, CInt(Math.Floor(width * scale))), Math.Max(1, CInt(Math.Floor(height * scale))))
    End Function
End Class
