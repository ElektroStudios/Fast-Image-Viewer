Option Strict On
Option Explicit On
Option Infer Off

Imports System.Drawing
Imports System.IO

''' <summary>Loads pixel data and metadata as one ownership transaction.</summary>
Friend NotInheritable Class CachedImageLoader
    Private Sub New()
    End Sub

    ''' <summary>Disposes the decoded bitmap if metadata cannot be read.</summary>
    Friend Shared Function Load(filePath As String, screenWidth As Integer, screenHeight As Integer, disableResize As Boolean, upscale As Boolean) As CachedImage
        Dim width As Integer
        Dim height As Integer
        Dim bitmap As Bitmap = BitmapLoader.LoadFast(filePath, screenWidth, screenHeight, disableResize, upscale, width, height)
        If bitmap Is Nothing Then Return Nothing
        Try
            Dim info As New FileInfo(filePath)
            Return New CachedImage(bitmap, width, height, Image.GetPixelFormatSize(bitmap.PixelFormat),
                                   FileSizeFormatter.FormatFileSize(info.Length),
                                   info.CreationTime.ToString("MM/dd/yyyy HH:mm:ss"),
                                   info.LastWriteTime.ToString("MM/dd/yyyy HH:mm:ss"))
        Catch
            bitmap.Dispose()
            Throw
        End Try
    End Function
End Class
