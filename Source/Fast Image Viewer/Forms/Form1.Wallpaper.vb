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


' Contains the form's wallpaper coordination.
Partial Public NotInheritable Class Form1

    Private Sub ApplyCurrentImageAsWallpaper(style As DesktopWallpaperStyle)
        Dim filePath As String = Me._imageFiles(Me._currentIndex)

        If Not File.Exists(filePath) Then
            Throw New FileNotFoundException("The current image file no longer exists.", filePath)
        End If

        Me.UpdateWallpaperRegistry(style)

        Dim bitmapPath As String = Me.CreateWallpaperBitmap(filePath)
        Dim ok As Boolean = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0UI, bitmapPath, SPIF_UPDATEINIFILE Or SPIF_SENDCHANGE)

        If Not ok Then
            Throw New InvalidOperationException($"SystemParametersInfo(SPI_SETDESKWALLPAPER) failed with Win32 error {Marshal.GetLastWin32Error()}.")
        End If
    End Sub

    Private Sub ApplyWallpaperFromMenu(style As DesktopWallpaperStyle)
        Try
            Me.ApplyCurrentImageAsWallpaper(style)
        Catch ex As Exception
            MessageBox.Show(Me,
                            $"Could not set the wallpaper:{Environment.NewLine}{ex.Message}",
                            "Wallpaper",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub UpdateWallpaperRegistry(style As DesktopWallpaperStyle)
        Dim wallpaperStyleValue As String
        Dim tileWallpaperValue As String

        Select Case style

            Case DesktopWallpaperStyle.Tile
                wallpaperStyleValue = "0"
                tileWallpaperValue = "1"

            Case DesktopWallpaperStyle.Center
                wallpaperStyleValue = "0"
                tileWallpaperValue = "0"

            Case DesktopWallpaperStyle.Stretch
                wallpaperStyleValue = "2"
                tileWallpaperValue = "0"

            Case DesktopWallpaperStyle.Fit
                wallpaperStyleValue = "6"
                tileWallpaperValue = "0"

            Case DesktopWallpaperStyle.Fill
                wallpaperStyleValue = "10"
                tileWallpaperValue = "0"

            Case DesktopWallpaperStyle.Span
                wallpaperStyleValue = "22"
                tileWallpaperValue = "0"

            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(style), style, "Unsupported wallpaper style.")
        End Select

        Registry.SetValue("HKEY_CURRENT_USER\Control Panel\Desktop", "WallpaperStyle", wallpaperStyleValue, RegistryValueKind.String)
        Registry.SetValue("HKEY_CURRENT_USER\Control Panel\Desktop", "TileWallpaper", tileWallpaperValue, RegistryValueKind.String)
    End Sub

    Private Function CreateWallpaperBitmap(filePath As String) As String
        Dim tempFolder As String = Path.Combine(Path.GetTempPath(), $"{My.Application.Info.AssemblyName}")
        Directory.CreateDirectory(tempFolder)

        Dim tempBitmapPath As String = Path.Combine(tempFolder, "CurrentWallpaper.bmp")

        Using sourceImage As Image = Image.FromFile(filePath)
            Using bitmap As New Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format24bppRgb)
                Using g As Graphics = Graphics.FromImage(bitmap)
                    g.DrawImage(sourceImage, 0, 0, sourceImage.Width, sourceImage.Height)
                End Using
                bitmap.Save(tempBitmapPath, ImageFormat.Bmp)
            End Using
        End Using

        Return tempBitmapPath
    End Function
End Class
