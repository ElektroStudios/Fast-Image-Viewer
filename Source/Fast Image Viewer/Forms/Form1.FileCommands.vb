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


' Contains the form's filecommands event handlers.
Partial Public NotInheritable Class Form1 ' NOSONAR

    Private Sub MenuStrip1_MenuActivate(sender As Object, e As EventArgs) Handles MainMenuStrip1.MenuActivate
        Dim hasImage As Boolean = Me.PicBox.Image IsNot Nothing

        Me.MenuItemCopyFileTo.Enabled = hasImage
        Me.MenuItemSaveAs.Enabled = hasImage
        Me.MenuItemCloseCurrentDirectory.Enabled = hasImage
        Me.MenuItemSendAllToRecycleBin.Enabled = hasImage
        Me.MenuItemShowInExplorer.Enabled = hasImage
        Me.MenuItemSetWallpaper.Enabled = hasImage

        Me.MenuItemZoomIn.Enabled = hasImage
        Me.MenuItemZoomOut.Enabled = hasImage
        Me.MenuItemResetZoom.Enabled = hasImage

        Me.MenuItemResetOrientation.Enabled = Me._rotationAngle <> 0
    End Sub

    ''' <summary>
    ''' Opens a modern Vista-style folder browser (OokiiDialogs) and
    ''' reloads the viewer with the chosen directory.
    ''' </summary>
    Private Sub MenuItemOpenDirectory_Click(sender As Object, e As System.EventArgs) Handles MenuItemOpenDirectory.Click
        Using dlg As New VistaFolderBrowserDialog()
            dlg.Description = "Select an image directory"
            dlg.UseDescriptionForTitle = True
            dlg.ShowNewFolderButton = False

            If Not String.IsNullOrWhiteSpace(Me.FolderPath) AndAlso
               Directory.Exists(Me.FolderPath) Then
                dlg.SelectedPath = Me.FolderPath
            End If

            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return

            Dim chosen As String = dlg.SelectedPath
            If String.IsNullOrWhiteSpace(chosen) OrElse Not Directory.Exists(chosen) Then Return

            Me.StopWorkerAndClearCache()
            Me.FolderPath = chosen
            Me._currentIndex = 0
            Me._lastWorkerCenter = -1
            Me.PicBox.Image = Nothing
            Me.lblInfo.Text = "0 / 0  |  No more images."

            Me.LoadDirectory()
        End Using
    End Sub

    Private Sub MenuItemCloseCurrentDirectory_Click(sender As Object, e As System.EventArgs) Handles MenuItemCloseCurrentDirectory.Click
        If Me._imageFiles.Length = 0 Then
            MessageBox.Show(Me, "No files are currently loaded.",
                            "Close current directory",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Me.StopWorkerAndClearCache()
        Me.FolderPath = ""
        Me._lastWorkerCenter = -1
        Me.PicBox.Image = Nothing
        Me._imageFiles = Array.Empty(Of String)()
        Me._currentIndex = 0
        Me.lblInfo.Text = "0 / 0  |  No more images."
        Me.UpdateNavigationButtons()
    End Sub

    ''' <summary>
    ''' Sends every currently loaded file in <see cref="_imageFiles"/> to
    ''' the Windows Recycle Bin after the user confirms via a dialog.
    ''' </summary>
    Private Sub MenuItemSendAllToRecycleBin_Click(sender As Object, e As System.EventArgs) Handles MenuItemSendAllToRecycleBin.Click
        If Me._imageFiles.Length = 0 Then
            MessageBox.Show(Me, "No files are currently loaded.",
                            "Send to recycle bin",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim confirm As DialogResult = MessageBox.Show(
            Me,
            $"Send all {Me._imageFiles.Length} loaded image file(s) to the Recycle Bin?",
            "Send all to recycle bin",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1)

        If confirm <> DialogResult.OK Then Return

        ' Stop cache worker and release all bitmaps before deleting files.
        Me.StopWorkerAndClearCache()
        Me.PicBox.Image = Nothing

        Dim errors As New System.Text.StringBuilder()

        For Each filePath As String In Me._imageFiles
            Try
                If File.Exists(filePath) Then
                    FileSystem.DeleteFile(filePath,
                                          UIOption.OnlyErrorDialogs,
                                          RecycleOption.SendToRecycleBin)
                End If
            Catch ex As Exception
                errors.AppendLine($"{Path.GetFileName(filePath)}: {ex.Message}")
                Exit For
            End Try
#If DEBUG Then
            Thread.CurrentThread.Join(0) ' Prevents ContextSwitchDeadlock on long-running loops
#End If
        Next

        Me._imageFiles = Array.Empty(Of String)()
        Me._currentIndex = 0
        Me.lblInfo.Text = "0 / 0  |  No more images."
        Me.UpdateNavigationButtons()

        If errors.Length > 0 Then
            MessageBox.Show(Me,
                            $"Some files could not be recycled:{Environment.NewLine}{errors}",
                            "Partial error",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub

    ''' <summary>
    ''' Opens a standard file dialog to load a single image file, 
    ''' automatically pointing the viewer context to its parent folder.
    ''' </summary>
    Private Sub MenuItemOpenFile_Click(sender As Object, e As EventArgs) Handles MenuItemOpenFile.Click
        Using dlg As New OpenFileDialog()
            Dim filterBuilder As New System.Text.StringBuilder("Image Files|")
            For i As Integer = 0 To AppGlobals.SupportedExtensions.Length - 1
                Dim ext As String = AppGlobals.SupportedExtensions(i).Replace(".", "")
                filterBuilder.Append($"*.{ext}")
                If i < AppGlobals.SupportedExtensions.Length - 1 Then
                    filterBuilder.Append(";")
                End If
            Next
            filterBuilder.Append("|All Files (*.*)|*.*")

            dlg.Filter = filterBuilder.ToString()
            dlg.Title = "Select an image file"

            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return

            Dim chosenFile As String = dlg.FileName
            If String.IsNullOrWhiteSpace(chosenFile) OrElse Not File.Exists(chosenFile) Then Return

            Me.LoadSingleImageFile(chosenFile)
        End Using
    End Sub

    Private Sub MenuItemCopyFileTo_Click(sender As Object, e As EventArgs) Handles MenuItemCopyFileTo.Click

        Dim getcachedimg As CachedImage = Nothing
        If Not Me._cache.TryGetValue(Me._imageFiles(Me._currentIndex), getcachedimg) Then
            MessageBox.Show("No image loaded.", "Save Image", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        Using saveDialog As New SaveFileDialog()

            Dim filepath As String = Path.GetFullPath(Me._imageFiles(Me._currentIndex))
            Dim dirpath As String = Path.GetDirectoryName(filepath)
            Dim filename As String = Path.GetFileName(filepath)
            Dim fileExt As String = Path.GetExtension(filename).ToLowerInvariant()

            saveDialog.Filter = "All files|*"
            saveDialog.Title = "Save Current Image"
            saveDialog.FileName = filename
            saveDialog.DefaultExt = fileExt
            saveDialog.FilterIndex = 1
            saveDialog.AddExtension = True
            saveDialog.OverwritePrompt = True
            saveDialog.ValidateNames = True
            saveDialog.SupportMultiDottedExtensions = False
            saveDialog.InitialDirectory = dirpath

            Dim filterIndex As Integer = 1
            Select Case fileExt
                Case ".jpg", ".jpeg"
                    saveDialog.Filter &= "|JPEG Image (*.jpg; *.jpeg)|*.jpg;*.jpeg"
                    saveDialog.FilterIndex = 2

                Case ".png"
                    saveDialog.Filter &= "|PNG Image (*.PNG)|*.png"
                    saveDialog.FilterIndex = 2

                Case ".bmp"
                    saveDialog.Filter &= "|Bitmap Image (*.bmp)|*.bmp"
                    saveDialog.FilterIndex = 2

                Case ".tif", ".tiff"
                    saveDialog.Filter &= "|TIFF Image (*.tif; *.tiff)|*.tif;*.tiff"
                    saveDialog.FilterIndex = 2

                Case Else
                    saveDialog.FilterIndex = 1
            End Select

            If saveDialog.ShowDialog() = DialogResult.OK Then
                Try
                    File.Copy(filepath, saveDialog.FileName, True)
                    MessageBox.Show($"File successfully copied to: {saveDialog.FileName}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

                Catch ex As Exception
                    MessageBox.Show($"An error occurred while copying the file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using

    End Sub

    Private Sub MenuItemSaveAs_Click(sender As Object, e As EventArgs) Handles MenuItemSaveAs.Click

        Dim getcachedimg As CachedImage = Nothing
        If Not Me._cache.TryGetValue(Me._imageFiles(Me._currentIndex), getcachedimg) Then
            MessageBox.Show("No image loaded.", "Save Image", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        Using saveDialog As New SaveFileDialog()

            Dim filepath As String = Path.GetFullPath(Me._imageFiles(Me._currentIndex))
            Dim dirpath As String = Path.GetDirectoryName(filepath)
            Dim filename As String = Path.GetFileName(filepath)
            Dim fileExt As String = Path.GetExtension(filename).ToLowerInvariant()

            saveDialog.Filter = "JPEG Image (*.jpg; *.jpeg)|*.jpg;*.jpeg|PNG Image (*.png)|*.png|Bitmap Image (*.bmp)|*.bmp|TIFF Image (*.tif; *.tiff)|*.tif;*.tiff"
            saveDialog.Title = "Save Current Image"
            saveDialog.FileName = filename
            saveDialog.DefaultExt = fileExt
            saveDialog.FilterIndex = 1
            saveDialog.AddExtension = True
            saveDialog.OverwritePrompt = True
            saveDialog.ValidateNames = True
            saveDialog.SupportMultiDottedExtensions = False
            saveDialog.InitialDirectory = dirpath

            Dim filterIndex As Integer = 1
            Select Case fileExt
                Case ".jpg", ".jpeg"
                    saveDialog.FilterIndex = 1

                Case ".png"
                    saveDialog.FilterIndex = 2

                Case ".bmp"
                    saveDialog.FilterIndex = 3

                Case ".tif", ".tiff"
                    saveDialog.FilterIndex = 4

                Case Else
                    saveDialog.FilterIndex = 1 ' default jpeg
            End Select

            If saveDialog.ShowDialog() = DialogResult.OK Then
                Try
                    Dim selectedExtension As String = Path.GetExtension(saveDialog.FileName).ToLowerInvariant()
                    Dim imageFormatToSave As ImageFormat = ImageFormat.Png

                    Select Case saveDialog.FilterIndex
                        Case 1 '".jpg", ".jpeg"
                            imageFormatToSave = ImageFormat.Jpeg
                        Case 2 '".png"
                            imageFormatToSave = ImageFormat.Png
                        Case 3 '".bmp"
                            imageFormatToSave = ImageFormat.Bmp
                        Case 4 '".tif", ".tiff"
                            imageFormatToSave = ImageFormat.Tiff
                    End Select

                    Dim imageToSave As Image = getcachedimg.Bitmap

                    If imageToSave IsNot Nothing Then
                        Using bmpToSave As Bitmap = DirectCast(imageToSave.Clone(), Bitmap)

                            Select Case Me._rotationAngle
                                Case 90
                                    bmpToSave.RotateFlip(RotateFlipType.Rotate90FlipNone)
                                Case 180
                                    bmpToSave.RotateFlip(RotateFlipType.Rotate180FlipNone)
                                Case 270
                                    bmpToSave.RotateFlip(RotateFlipType.Rotate270FlipNone)
                            End Select

                            bmpToSave.Save(saveDialog.FileName, imageFormatToSave)
                        End Using

                        MessageBox.Show($"Image successfully saved to: {saveDialog.FileName}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Else
                        MessageBox.Show("The cached image object does not contain valid image data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    End If

                Catch ex As Exception
                    MessageBox.Show($"An error occurred while saving the image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using

    End Sub

    Private Sub MenuItemShowInExplorer_Click(sender As Object, e As EventArgs) Handles MenuItemShowInExplorer.Click

        Dim filePath As String = Me._imageFiles(Me._currentIndex)

        ' Validate the target file path
        If String.IsNullOrWhiteSpace(filePath) Then
            Throw New InvalidOperationException("The retrieved file path is null, empty, or consists only of white-space characters.")
        End If

        If Not File.Exists(filePath) Then
            Throw New FileNotFoundException($"The current image file no longer exists: {filePath}", filePath)
        End If

        ' Resolve the absolute path for explorer.exe to satisfy SonarQube and prevent path hijacking
        Dim windowsDirectory As String = Environment.GetFolderPath(Environment.SpecialFolder.Windows)

        If String.IsNullOrWhiteSpace(windowsDirectory) Then
            Throw New PlatformNotSupportedException("Cannot determine the Windows installation directory.")
        End If

        Dim explorerPath As String = Path.Combine(windowsDirectory, "explorer.exe")

        ' Defensive check for the critical system executable
        If Not File.Exists(explorerPath) Then
            Throw New FileNotFoundException($"The executable for Windows Explorer was not found at the expected absolute path: {explorerPath}", explorerPath)
        End If

        ' Initialize and start the process safely
        Dim startInfo As ProcessStartInfo = New ProcessStartInfo(explorerPath, $"/select,""{filePath}""") With {
            .UseShellExecute = True
        }

        Process.Start(startInfo)
    End Sub

    Private Sub MenuItemWallpaperFill_Click(sender As Object, e As EventArgs) Handles MenuItemWallpaperFill.Click
        Me.ApplyWallpaperFromMenu(DesktopWallpaperStyle.Fill)
    End Sub

    Private Sub MenuItemWallpaperFit_Click(sender As Object, e As EventArgs) Handles MenuItemWallpaperFit.Click
        Me.ApplyWallpaperFromMenu(DesktopWallpaperStyle.Fit)
    End Sub

    Private Sub MenuItemWallpaperStretch_Click(sender As Object, e As EventArgs) Handles MenuItemWallpaperStretch.Click
        Me.ApplyWallpaperFromMenu(DesktopWallpaperStyle.Stretch)
    End Sub

    Private Sub MenuItemWallpaperTile_Click(sender As Object, e As EventArgs) Handles MenuItemWallpaperTile.Click
        Me.ApplyWallpaperFromMenu(DesktopWallpaperStyle.Tile)
    End Sub

    Private Sub MenuItemWallpaperCenter_Click(sender As Object, e As EventArgs) Handles MenuItemWallpaperCenter.Click
        Me.ApplyWallpaperFromMenu(DesktopWallpaperStyle.Center)
    End Sub

    Private Sub MenuItemWallpaperSpan_Click(sender As Object, e As EventArgs) Handles MenuItemWallpaperSpan.Click
        Me.ApplyWallpaperFromMenu(DesktopWallpaperStyle.Span)
    End Sub

    Private Sub MenuItemExit_Click(sender As Object, e As EventArgs) Handles MenuItemExit.Click

        Me.Close()
    End Sub

End Class
