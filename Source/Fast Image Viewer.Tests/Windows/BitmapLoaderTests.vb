Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports Xunit

Public Class BitmapLoaderTests
    <Theory>
    <InlineData(80, 40, 40, 40, False, False, 40, 20)>
    <InlineData(80, 40, 40, 40, True, False, 80, 40)>
    <InlineData(20, 10, 40, 40, False, False, 20, 10)>
    <InlineData(20, 10, 40, 40, False, True, 40, 20)>
    <InlineData(1, 1000, 100, 100, False, False, 1, 100)>
    <InlineData(1000, 1, 100, 100, False, False, 100, 1)>
    Public Sub DecodePreservesMetadataAndDetachesFile(w As Integer, h As Integer, sw As Integer, sh As Integer, disabled As Boolean, upscale As Boolean, ew As Integer, eh As Integer)
        Dim path As String = IO.Path.Combine(IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") & ".png")
        Try
            Using source As New Bitmap(w, h)
                Using g As Graphics = Graphics.FromImage(source)
                    g.Clear(Color.Red)
                End Using
                source.Save(path, ImageFormat.Png)
            End Using
            Dim originalWidth As Integer
            Dim originalHeight As Integer
            Using result As Bitmap = BitmapLoader.LoadFast(path, sw, sh, disabled, upscale, originalWidth, originalHeight)
                Assert.NotNull(result)
                Assert.Equal(New Size(ew, eh), result.Size)
                Assert.Equal(w, originalWidth)
                Assert.Equal(h, originalHeight)
                Assert.Equal(PixelFormat.Format32bppPArgb, result.PixelFormat)
                Using exclusive As New FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                    Assert.True(exclusive.Length > 0)
                End Using
                File.Delete(path)
                Using copy As New Bitmap(result)
                    Assert.Equal(result.Size, copy.Size)
                End Using
            End Using
        Finally
            File.Delete(path)
        End Try
    End Sub

    <Fact>
    Public Sub OriginalLoadIsDetachedAndUnscaled()
        Dim path As String = IO.Path.GetTempFileName()
        Try
            Using source As New Bitmap(37, 19)
                source.Save(path, ImageFormat.Png)
            End Using
            Using result As Bitmap = BitmapLoader.LoadOriginal(path)
                Assert.Equal(New Size(37, 19), result.Size)
                File.Delete(path)
                Assert.Equal(PixelFormat.Format32bppPArgb, result.PixelFormat)
            End Using
        Finally
            File.Delete(path)
        End Try
    End Sub

    <Fact>
    Public Sub InvalidAndMissingImagesReturnNothing()
        Dim path As String = IO.Path.GetTempFileName()
        Try
            File.WriteAllText(path, "This is not an image.")
            Dim width As Integer
            Dim height As Integer
            Assert.Null(BitmapLoader.LoadFast(path, 100, 100, False, False, width, height))
            Assert.Null(BitmapLoader.LoadOriginal(path))
            File.Delete(path)
            Assert.Null(BitmapLoader.LoadOriginal(path))
        Finally
            File.Delete(path)
        End Try
    End Sub

    <Fact>
    Public Sub FailedSizingReleasesSourceFile()
        Dim path As String = IO.Path.GetTempFileName()
        Try
            Using source As New Bitmap(10, 10)
                source.Save(path, ImageFormat.Png)
            End Using
            Dim width As Integer
            Dim height As Integer
            Assert.Throws(Of ArgumentOutOfRangeException)(Function() BitmapLoader.LoadFast(path, 0, 10, False, False, width, height))
            Using exclusive As New FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                Assert.True(exclusive.Length > 0)
            End Using
        Finally
            File.Delete(path)
        End Try
    End Sub

    <Fact>
    Public Sub CachedMetadataUsesOriginalDimensions()
        Dim path As String = IO.Path.GetTempFileName()
        Try
            Using source As New Bitmap(200, 100)
                source.Save(path, ImageFormat.Png)
            End Using
            Dim loaded As CachedImage = CachedImageLoader.Load(path, 50, 50, False, False)
            Using loaded.Bitmap
                Assert.Equal(200, loaded.Width)
                Assert.Equal(100, loaded.Height)
                Assert.Equal(32, loaded.Bpp)
                Assert.Equal(New Size(50, 25), loaded.Bitmap.Size)
                Assert.Equal(FileSizeFormatter.FormatFileSize(New FileInfo(path).Length), loaded.FormattedSize)
                Assert.False(String.IsNullOrWhiteSpace(loaded.CreatedText))
                Assert.False(String.IsNullOrWhiteSpace(loaded.ModifiedText))
                File.Delete(path)
            End Using
        Finally
            File.Delete(path)
        End Try
    End Sub
End Class
