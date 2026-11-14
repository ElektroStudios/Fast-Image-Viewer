Option Strict On
Option Explicit On
Option Infer Off

Imports System.Drawing
Imports System.Drawing.Imaging

''' <summary>Decodes detached PArgb bitmaps; the caller owns each successful result.</summary>
Friend NotInheritable Class BitmapLoader ' NOSONAR
    Private Sub New()
    End Sub

    ''' <summary>Loads an image with the viewer's existing screen-fit policy.</summary>
    Friend Shared Function LoadFast(filePath As String, screenWidth As Integer, screenHeight As Integer, disableResize As Boolean, upscale As Boolean, ByRef originalWidth As Integer, ByRef originalHeight As Integer) As Bitmap
        Dim source As Bitmap
        Try
            source = New Bitmap(filePath)
        Catch
            Return Nothing
        End Try
        Using source
            originalWidth = source.Width
            originalHeight = source.Height
            Dim target As Size = ImageSizing.GetLoadSize(source.Width, source.Height, screenWidth, screenHeight, disableResize, upscale)
            Dim scale As Double = Math.Min(screenWidth / source.Width, screenHeight / source.Height)
            Dim useLowInterpolation As Boolean = Not (disableResize OrElse (scale >= 1.0 AndAlso Not upscale))
            Return CopyBitmap(source, target, useLowInterpolation)
        End Using
    End Function

    ''' <summary>Loads an unscaled image without keeping its source file locked.</summary>
    Friend Shared Function LoadOriginal(filePath As String) As Bitmap
        Dim source As Bitmap
        Try
            source = New Bitmap(filePath)
        Catch
            Return Nothing
        End Try
        Using source
            Return CopyBitmap(source, source.Size, False)
        End Using
    End Function

    ''' <summary>Transfers ownership only after drawing succeeds.</summary>
    Private Shared Function CopyBitmap(source As Bitmap, target As Size, lowInterpolation As Boolean) As Bitmap
        Dim result As New Bitmap(target.Width, target.Height, PixelFormat.Format32bppPArgb)
        Try
            Using graphics As Graphics = Graphics.FromImage(result)
                If lowInterpolation Then graphics.InterpolationMode = Drawing2D.InterpolationMode.Low
                graphics.DrawImage(source, 0, 0, target.Width, target.Height)
            End Using
            Return result
        Catch
            result.Dispose()
            Throw
        End Try
    End Function
End Class
