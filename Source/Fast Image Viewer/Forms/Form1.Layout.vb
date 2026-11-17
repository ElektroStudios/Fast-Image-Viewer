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


' Contains the form's layout coordination.
Partial Public NotInheritable Class Form1 ' NOSONAR

    Private Sub BuildUI()
        Me.Text = $"{My.Application.Info.ProductName}"
        Me.BackColor = Color.FromArgb(10, 10, 10)
        Me.ForeColor = Color.WhiteSmoke
        Me.KeyPreview = True
        Me.MinimumSize = New Size(400, 200)
        Me.DoubleBuffered = True

        Me.NavTimer = New System.Windows.Forms.Timer With {
            .Interval = NAV_THROTTLE_MS,
            .Enabled = False
        }

        ' ── Menu strip ──────────────────────────────────────────────
        Me.MenuItemOpenFile = New ToolStripMenuItem("Open &file...") With {
            .Image = My.Resources.Resource1.openfile,
            .ShortcutKeys = Keys.Control Or Keys.O
        }

        Me.MenuItemCopyFileTo = New ToolStripMenuItem("&Copy file to...") With {
            .Image = My.Resources.Resource1.copyto,
            .ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.C
        }

        Me.MenuItemSaveAs = New ToolStripMenuItem("&Save file as...") With {
            .Image = My.Resources.Resource1.saveas,
            .ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.S
        }

        Me.MenuItemOpenDirectory = New ToolStripMenuItem("Open &directory") With {
            .Image = My.Resources.Resource1.openfolder,
            .ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.O
        }

        Me.MenuItemCloseCurrentDirectory = New ToolStripMenuItem("Close c&urrent directory") With {
            .Image = My.Resources.Resource1.closefolder,
            .ShortcutKeys = Keys.Control Or Keys.W
        }

        Me.MenuItemSendAllToRecycleBin = New ToolStripMenuItem("Send all loaded files to &recycle bin") With {
            .Image = My.Resources.Resource1.recycle,
            .ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.Delete
        }

        Me.MenuItemShowInExplorer = New ToolStripMenuItem("Show file in &Explorer...") With {
            .Image = Nothing,
            .ShortcutKeys = Keys.Control Or Keys.E
        }

        Me.MenuItemExit = New ToolStripMenuItem("Close &application") With {
            .Image = My.Resources.Resource1._exit,
            .ShortcutKeyDisplayString = "Alt+F4"
        }

        Dim fileMenu As New ToolStripMenuItem("&File")
        fileMenu.DropDownItems.Add(Me.MenuItemOpenFile)
        fileMenu.DropDownItems.Add(Me.MenuItemCopyFileTo)
        fileMenu.DropDownItems.Add(Me.MenuItemSaveAs)
        fileMenu.DropDownItems.Add(New ToolStripSeparator())
        fileMenu.DropDownItems.Add(Me.MenuItemOpenDirectory)
        fileMenu.DropDownItems.Add(Me.MenuItemCloseCurrentDirectory)
        fileMenu.DropDownItems.Add(New ToolStripSeparator())
        fileMenu.DropDownItems.Add(Me.MenuItemShowInExplorer)
        fileMenu.DropDownItems.Add(New ToolStripSeparator())
        fileMenu.DropDownItems.Add(Me.MenuItemSendAllToRecycleBin)
        fileMenu.DropDownItems.Add(New ToolStripSeparator())
        fileMenu.DropDownItems.Add(Me.MenuItemExit)

        ' ── Menu strip: Options Menu ────────────────────────────────
        Me.MenuItemShowCacheLabel = New ToolStripMenuItem("Show Cache &Label") With {
            .CheckOnClick = True,
            .Checked = Me.SHOW_CACHE_LABEL
        }
        Me.MenuItemShowProgressBar = New ToolStripMenuItem("Show Cache &Progress Bar") With {
            .CheckOnClick = True,
            .Checked = Me.SHOW_PROGRESS_BAR
        }

        Me.MenuItemUpscaleSmallImages = New ToolStripMenuItem("&Upscale small images to fit window") With {
            .CheckOnClick = True,
            .Checked = Me.UPSCALE_SMALL_IMAGES_TO_FIT_WINDOW
        }

        ' Create NumericUpDown for Forward Radius
        Me.NumForwardCtrl = New NumericUpDown With {
            .Minimum = 1,
            .Maximum = 5000,
            .Value = Me.CACHE_RADIUS_FORWARD,
            .Width = 80
        }
        Me.NumForwardHost = New ToolStripControlHost(Me.NumForwardCtrl)

        ' Create SubMenu Item for Forward
        Dim menuForwardGroup As New ToolStripMenuItem("&Forward Radius: ")
        menuForwardGroup.DropDownItems.Add(Me.NumForwardHost)

        ' Create NumericUpDown for Back Radius
        Me.NumBackCtrl = New NumericUpDown With {
            .Minimum = 1,
            .Maximum = 5000,
            .Value = Me.CACHE_RADIUS_BACK,
            .Width = 80
        }
        Me.NumBackHost = New ToolStripControlHost(Me.NumBackCtrl)

        ' Create SubMenu Item for Back
        Dim menuBackGroup As New ToolStripMenuItem("&Backward Radius: ")
        menuBackGroup.DropDownItems.Add(Me.NumBackHost)

        ' Submenu Container for Cache Settings
        Me.MenuItemCacheMenu = New ToolStripMenuItem("Configure Cache &Radius")
        Me.MenuItemCacheMenu.DropDownItems.Add(menuForwardGroup)
        Me.MenuItemCacheMenu.DropDownItems.Add(menuBackGroup)

        Me.MenuItemAskForDirectoryPathAtStartup = New ToolStripMenuItem("&Ask for directory path at program startup") With {
            .CheckOnClick = True,
            .Checked = Me.ASK_FOR_DIRECTORY_PATH_AT_STARTUP
        }

        Me.MenuItemRememberWindowState = New ToolStripMenuItem("Remember &window size and position") With {
            .CheckOnClick = True,
            .Checked = Me.REMEMBER_WINDOW_STATE
        }

        Me.MenuItemDelKeyBehavior = New ToolStripMenuItem("'Del' key &moves the current image to custom folder") With {
            .CheckOnClick = True,
            .Checked = Me.DEL_KEY_MOVES_IMAGE_TO_CUSTOM_FOLDER
        }

        Dim customFolderMenu As New ToolStripMenuItem("Custom &folder path")

        Me.CustomFolderTextBox = New ToolStripTextBox With {
            .Text = Me.CUSTOM_FOLDER_NAME,
            .AutoSize = False,
            .Width = 250,
            .Height = 100
        }

        Dim internalTextBox As TextBox = Me.CustomFolderTextBox.TextBox
        If internalTextBox IsNot Nothing Then
            internalTextBox.Multiline = True
            internalTextBox.WordWrap = True
            internalTextBox.ScrollBars = ScrollBars.Vertical
        End If

        ' Assemble Image Menu
        Me.MenuItemSetWallpaper = New ToolStripMenuItem("Set as Desktop &Wallpaper...") With {
            .Image = Nothing
        }
        Me.MenuItemWallpaperFill = New ToolStripMenuItem("&Fill")
        Me.MenuItemWallpaperFit = New ToolStripMenuItem("F&it")
        Me.MenuItemWallpaperStretch = New ToolStripMenuItem("&Stretch")
        Me.MenuItemWallpaperTile = New ToolStripMenuItem("&Tile")
        Me.MenuItemWallpaperCenter = New ToolStripMenuItem("&Center")
        Me.MenuItemWallpaperSpan = New ToolStripMenuItem("S&pan")

        Me.MenuItemSetWallpaper.DropDownItems.Add(Me.MenuItemWallpaperFill)
        Me.MenuItemSetWallpaper.DropDownItems.Add(Me.MenuItemWallpaperFit)
        Me.MenuItemSetWallpaper.DropDownItems.Add(Me.MenuItemWallpaperStretch)
        Me.MenuItemSetWallpaper.DropDownItems.Add(Me.MenuItemWallpaperTile)
        Me.MenuItemSetWallpaper.DropDownItems.Add(Me.MenuItemWallpaperCenter)
        Me.MenuItemSetWallpaper.DropDownItems.Add(Me.MenuItemWallpaperSpan)

        ' Display the existing unmodified keys without registering duplicate shortcuts.
        Me.MenuItemZoomIn.ShortcutKeyDisplayString = "+"
        Me.MenuItemZoomOut.ShortcutKeyDisplayString = "-"
        Me.MenuItemRotateLeft.ShortcutKeyDisplayString = "L"
        Me.MenuItemRotateRight.ShortcutKeyDisplayString = "R"
        Me.MenuItemResetZoom.ShortcutKeys = Keys.Control Or Keys.D0
        Me.MenuItemResetOrientation.ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.D0
        Me.MenuItemResetOrientation.Text = "Reset Orient&ation"

        Dim imageMenu As New ToolStripMenuItem("&Image")
        imageMenu.DropDownItems.Add(Me.MenuItemZoomIn)
        imageMenu.DropDownItems.Add(Me.MenuItemZoomOut)
        imageMenu.DropDownItems.Add(Me.MenuItemResetZoom)
        imageMenu.DropDownItems.Add(New ToolStripSeparator())
        imageMenu.DropDownItems.Add(Me.MenuItemRotateLeft)
        imageMenu.DropDownItems.Add(Me.MenuItemRotateRight)
        imageMenu.DropDownItems.Add(Me.MenuItemResetOrientation)
        imageMenu.DropDownItems.Add(New ToolStripSeparator())
        imageMenu.DropDownItems.Add(Me.MenuItemSetWallpaper)
        AddHandler Me.MenuItemZoomIn.Click, Sub(sender As Object, e As EventArgs) Me.AdjustZoom(0.1)
        AddHandler Me.MenuItemZoomOut.Click, Sub(sender As Object, e As EventArgs) Me.AdjustZoom(-0.1)
        AddHandler Me.MenuItemResetZoom.Click, Sub(sender As Object, e As EventArgs)
                                                  If Me.PicBox.Image IsNot Nothing Then Me.ResetZoom()
                                              End Sub
        AddHandler Me.MenuItemRotateLeft.Click, Sub(sender As Object, e As EventArgs) Me.RotateImage(False)
        AddHandler Me.MenuItemRotateRight.Click, Sub(sender As Object, e As EventArgs) Me.RotateImage(True)
        AddHandler Me.MenuItemResetOrientation.Click, Sub(sender As Object, e As EventArgs)
                                                          If Me._rotationAngle = 0 Then Return
                                                          Me._rotationAngle = 90
                                                          Me.RotateImage(False)
                                                      End Sub

        ' Assemble Options Menu
        Dim optionsMenu As New ToolStripMenuItem("&Options")
        optionsMenu.DropDownItems.Add(Me.MenuItemShowCacheLabel)
        optionsMenu.DropDownItems.Add(Me.MenuItemShowProgressBar)
        optionsMenu.DropDownItems.Add(Me.MenuItemCacheMenu)
        optionsMenu.DropDownItems.Add(New ToolStripSeparator())
        optionsMenu.DropDownItems.Add(Me.MenuItemUpscaleSmallImages)
        optionsMenu.DropDownItems.Add(New ToolStripSeparator())
        optionsMenu.DropDownItems.Add(Me.MenuItemRememberWindowState)
        optionsMenu.DropDownItems.Add(New ToolStripSeparator())
        optionsMenu.DropDownItems.Add(Me.MenuItemAskForDirectoryPathAtStartup)
        optionsMenu.DropDownItems.Add(New ToolStripSeparator())
        optionsMenu.DropDownItems.Add(Me.MenuItemDelKeyBehavior)

        customFolderMenu.DropDownItems.Add(Me.CustomFolderTextBox)
        optionsMenu.DropDownItems.Add(customFolderMenu)

        Me.MenuItemAdvanced = New ToolStripMenuItem("Ad&vanced")
        Me.MenuItemAdvancedWarning = New ToolStripLabel("If unsure, do not change anything!") With {
            .ForeColor = Color.Red,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }

        Me.NumFastForwardPauseCtrl = New NumericUpDown With {
            .Minimum = 0,
            .Maximum = 5000,
            .Value = Me.PAUSE_BETWEEN_FASTFORWARD_MILLISECONDS,
            .Width = 80
        }
        Me.NumFastForwardPauseHost = New ToolStripControlHost(Me.NumFastForwardPauseCtrl)
        Dim menuFastForwardGroup As New ToolStripMenuItem("&Pause between fast forward (ms): ")
        menuFastForwardGroup.DropDownItems.Add(Me.NumFastForwardPauseHost)

        Me.MenuItemDisableFastLoadResize = New ToolStripMenuItem("Disable image resizing on load (&FastLoad)") With {
            .CheckOnClick = True,
            .Checked = Me.DISABLE_IMAGERESIZING_ON_LOAD_FASTLOAD
        }

        Me.MenuItemAdvanced.DropDownItems.Add(Me.MenuItemAdvancedWarning)
        Me.MenuItemAdvanced.DropDownItems.Add(New ToolStripSeparator())
        Me.MenuItemAdvanced.DropDownItems.Add(menuFastForwardGroup)
        Me.MenuItemAdvanced.DropDownItems.Add(Me.MenuItemDisableFastLoadResize)

        AddHandler Me.NumFastForwardPauseCtrl.ValueChanged, AddressOf Me.NumFastForwardPauseCtrl_ValueChanged

        Me.MenuItemSlideshow = New ToolStripMenuItem("&Slideshow")
        Me.MenuItemStartSlideshow = New ToolStripMenuItem("&Start Slideshow") With {
            .ShortcutKeys = Keys.F5
        }

        Me.NumSlideshowIntervalCtrl = New NumericUpDown With {
            .Minimum = 1,
            .Maximum = 9999,
            .Value = Me.SLIDESHOW_INTERVAL_SECONDS,
            .Width = 80
        }
        Me.NumSlideshowIntervalHost = New ToolStripControlHost(Me.NumSlideshowIntervalCtrl)
        Dim menuSlideshowIntervalGroup As New ToolStripMenuItem("&Interval (seconds): ")
        menuSlideshowIntervalGroup.DropDownItems.Add(Me.NumSlideshowIntervalHost)

        Me.MenuItemSlideshow.DropDownItems.Add(Me.MenuItemStartSlideshow)
        Me.MenuItemSlideshow.DropDownItems.Add(New ToolStripSeparator())
        Me.MenuItemSlideshow.DropDownItems.Add(menuSlideshowIntervalGroup)

        AddHandler Me.NumSlideshowIntervalCtrl.ValueChanged, AddressOf Me.NumSlideshowIntervalCtrl_ValueChanged

        Dim aboutMenu As New ToolStripMenuItem("&About")
        AddHandler aboutMenu.Click, Sub() Me.AboutBox.ShowDialog()

        ' Wire up change events for the numbers
        AddHandler Me.NumForwardCtrl.ValueChanged, AddressOf Me.CacheRadius_ValueChanged
        AddHandler Me.NumBackCtrl.ValueChanged, AddressOf Me.CacheRadius_ValueChanged

        ' ── Assemble MenuStrip ──────────────────────────────────────
        Me.MainMenuStrip1 = New MenuStrip()
        Me.MainMenuStrip1.Items.Add(fileMenu)
        Me.MainMenuStrip1.Items.Add(imageMenu)
        Me.MainMenuStrip1.Items.Add(Me.MenuItemSlideshow)
        Me.MainMenuStrip1.Items.Add(optionsMenu)
        Me.MainMenuStrip1.Items.Add(Me.MenuItemAdvanced)
        Me.MainMenuStrip1.Items.Add(aboutMenu)
        Me.MainMenuStrip1.BackColor = Color.FromArgb(30, 30, 30)
        Me.MainMenuStrip1.ForeColor = SystemColors.ControlDark

        Me.MainMenuStrip = Me.MainMenuStrip1
        ' ────────────────────────────────────────────────────────────

        Me.PicBox = New SafePictureBox With {
            .Dock = DockStyle.Fill,
            .SizeMode = PictureBoxSizeMode.CenterImage,
            .BackColor = Color.Black
        }
        AddHandler Me.PicBox.Paint, AddressOf Me.PicBox_Paint

        ' Inline event handler to update SizeMode dynamically when resizing the form
        AddHandler Me.PicBox.Resize,
            Sub(sender As Object, e As EventArgs)
                If Me.PicBox.Image IsNot Nothing Then
                    Dim bmp As Bitmap = DirectCast(Me.PicBox.Image, Bitmap)
                    Me.PicBox.SizeMode =
                    If(Not Me.UPSCALE_SMALL_IMAGES_TO_FIT_WINDOW AndAlso
                           bmp.Width <= Me.PicBox.ClientSize.Width AndAlso
                           bmp.Height <= Me.PicBox.ClientSize.Height AndAlso
                           bmp.Width <= Me._screenWidth - 2 AndAlso
                           bmp.Height <= Me._screenHeight - 2,
                       PictureBoxSizeMode.CenterImage,
                       PictureBoxSizeMode.Zoom)
                End If
            End Sub

        Me.PanScroll = New Panel With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .BackColor = Color.Black,
            .Visible = False
        }

        ' Dynamically center actual size image on window resize.
        AddHandler Me.PanScroll.Resize,
            Sub(sender As Object, e As EventArgs)
                ' Check if PanScroll is visible, which naturally covers both Actual Size and Zoom modes
                If Me.PanScroll.Visible AndAlso Me.PanScroll.Controls.Count > 0 Then
                    Dim pb As PictureBox = DirectCast(Me.PanScroll.Controls(0), PictureBox)
                    Dim pnlClientW As Integer = Me.PanScroll.ClientSize.Width
                    Dim pnlClientH As Integer = Me.PanScroll.ClientSize.Height

                    ' Suspend layout and reset scroll to calculate true (0,0) based coordinates
                    Me.PanScroll.SuspendLayout()
                    Dim savedScroll As Point = Me.PanScroll.AutoScrollPosition
                    Me.PanScroll.AutoScrollPosition = Point.Empty

                    ' If the image is smaller than the window, center it. If larger, lock to 0.
                    Dim targetX As Integer = If(pb.Width < pnlClientW, (pnlClientW - pb.Width) \ 2, 0)
                    Dim targetY As Integer = If(pb.Height < pnlClientH, (pnlClientH - pb.Height) \ 2, 0)

                    pb.Location = New Point(targetX, targetY)

                    ' Restore scroll (WinForms strictly requires positive values when setting AutoScrollPosition)
                    Me.PanScroll.AutoScrollPosition = New Point(Math.Abs(savedScroll.X), Math.Abs(savedScroll.Y))
                    Me.PanScroll.ResumeLayout(True)
                End If
            End Sub

        Dim pnlContent As New Panel With {
            .Dock = DockStyle.Fill,
            .Margin = Padding.Empty,
            .Padding = Padding.Empty
        }
        pnlContent.Controls.Add(Me.PicBox)
        pnlContent.Controls.Add(Me.PanScroll)

        Me.pnlBottom = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(20, 20, 20)
        }

        Me.BtnPrev = Me.MakeButton("◀  &Prev", New Point(12, 10))
        Me.BtnPrev.Enabled = False
        Me.BtnPrev.Width = 100
        Me.pnlBottom.Controls.Add(Me.BtnPrev)

        Me.BtnNext = Me.MakeButton("&Next  ▶", New Point(Me.BtnPrev.Bounds.Right + 10, Me.BtnPrev.Top))
        Me.BtnNext.Enabled = False
        Me.BtnNext.Width = 100
        Me.pnlBottom.Controls.Add(Me.BtnNext)

        Me.lblInfo = New Label With {
            .ForeColor = Color.FromArgb(220, 220, 220),
            .Font = New Font("Segoe UI", 10.6F),
            .AutoSize = False,
            .Size = New Size(800, Me.pnlBottom.Bounds.Height),
            .Location = New Point(Me.BtnNext.Bounds.Right + 10, Me.BtnNext.Bounds.Top),
            .TextAlign = ContentAlignment.TopLeft
        }
        Me.pnlBottom.Controls.Add(Me.lblInfo)

        Me.lblLoading = New Label With {
            .ForeColor = Color.FromArgb(220, 220, 220),
            .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold),
            .AutoSize = True,
            .BackColor = Color.Transparent,
            .Location = New Point(20, 20),
            .Visible = False
        }
        Me.PicBox.Controls.Add(Me.lblLoading)

        Me.progressBar = New ProgressBar With {
            .Style = Windows.Forms.ProgressBarStyle.Continuous,
            .Height = 4,
            .Dock = DockStyle.Top,
            .Visible = False
        }
        Me.pnlBottom.Controls.Add(Me.progressBar)

        Me.tblMain = New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .RowCount = 3,
            .ColumnCount = 1,
            .Margin = Padding.Empty,
            .Padding = Padding.Empty
        }
        Me.tblMain.RowStyles.Add(New RowStyle())
        Me.tblMain.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Me.tblMain.RowStyles.Add(New RowStyle(SizeType.Absolute, 56.0F))
        Me.tblMain.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        Me.tblMain.Controls.Add(Me.MainMenuStrip1, 0, 0)
        Me.tblMain.Controls.Add(pnlContent, 0, 1)
        Me.tblMain.Controls.Add(Me.pnlBottom, 0, 2)

        Me.Controls.Add(Me.tblMain)
    End Sub

    Private Function MakeButton(text As String, loc As Point) As Button
        Dim b As New Button With {
            .Text = text,
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = Color.FromArgb(50, 50, 50),
            .FlatStyle = FlatStyle.Flat
        }
        b.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80)
        b.Size = New Size(120, 36)
        b.Location = loc
        b.Cursor = Cursors.Hand
        Return b
    End Function

    Private Sub InitializeCustomFolderName(initialValue As String)
        Dim value As String = If(initialValue, String.Empty)

        If String.IsNullOrWhiteSpace(value) Then
            Throw New InvalidOperationException("Custom folder path must start with a valid non-empty value.")
        End If

        Me._lastValidCustomFolderPath = value
        Me.CustomFolderTextBox.Text = value
    End Sub
End Class
