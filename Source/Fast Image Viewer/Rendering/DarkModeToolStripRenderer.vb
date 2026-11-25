Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Drawing.Text
Imports System.Windows.Forms

''' <summary>
''' Renders menu strips, toolbars, and context menus with flat dark surfaces and glyphs.
''' </summary>
Friend NotInheritable Class DarkModeToolStripRenderer
    Inherits ToolStripProfessionalRenderer

    ''' <summary>
    ''' Initializes the renderer with the dark palette and square corners.
    ''' </summary>
    Friend Sub New()
        MyBase.New(New DarkModeColorTable())
        Me.RoundedEdges = False
    End Sub

    ''' <summary>
    ''' Applies this renderer and its colors to a strip, its existing drop-downs, and its hosted editors.
    ''' </summary>
    ''' <param name="strip">The menu strip, toolbar, or context menu to style.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="strip"/> is <see langword="Nothing"/>.</exception>
    Friend Sub ApplyTo(strip As ToolStrip)
        If strip Is Nothing Then
            Throw New ArgumentNullException(NameOf(strip))
        End If

        Dim pending As New Stack(Of ToolStrip)()
        pending.Push(strip)

        While pending.Count > 0
            Dim current As ToolStrip = pending.Pop()
            current.Renderer = Me
            current.BackColor = If(TypeOf current Is ToolStripDropDown,
                                   Me.ColorTable.ToolStripDropDownBackground,
                                   DarkModeColorTable.StripBackground)
            current.ForeColor = DarkModeColorTable.TextColor

            For index As Integer = 0 To current.Items.Count - 1
                Dim item As ToolStripItem = current.Items(index)
                Dim dropDownItem As ToolStripDropDownItem = TryCast(item, ToolStripDropDownItem)
                If dropDownItem IsNot Nothing AndAlso dropDownItem.HasDropDownItems Then
                    pending.Push(dropDownItem.DropDown)
                End If

                Dim host As ToolStripControlHost = TryCast(item, ToolStripControlHost)
                If host IsNot Nothing Then
                    Dim editor As Control = host.Control
                    If TypeOf editor Is TextBoxBase Then
                        DirectCast(editor, TextBoxBase).BorderStyle = BorderStyle.None
                    ElseIf TypeOf editor Is NumericUpDown Then
                        DirectCast(editor, NumericUpDown).BorderStyle = BorderStyle.None
                    End If

                    If TypeOf editor Is TextBoxBase OrElse TypeOf editor Is NumericUpDown Then
                        editor.BackColor = DarkModeColorTable.InputBackground
                        editor.ForeColor = DarkModeColorTable.TextColor
                    End If
                End If
            Next
        End While
    End Sub

    ''' <summary>
    ''' Fills the strip surface with a solid color, including automatically generated overflow menus.
    ''' </summary>
    ''' <param name="e">The strip and painting surface.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="Nothing"/>.</exception>
    Protected Overrides Sub OnRenderToolStripBackground(e As ToolStripRenderEventArgs)
        If e Is Nothing Then
            Throw New ArgumentNullException(NameOf(e))
        End If

        Dim background As Color = If(TypeOf e.ToolStrip Is ToolStripDropDown,
                                     Me.ColorTable.ToolStripDropDownBackground,
                                     DarkModeColorTable.StripBackground)
        Using brush As New SolidBrush(background)
            e.Graphics.FillRectangle(brush, e.AffectedBounds)
        End Using
    End Sub

    ''' <summary>
    ''' Paints the drop-down image and check margin using the same solid color as the menu.
    ''' </summary>
    ''' <param name="e">The margin bounds and painting surface.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="Nothing"/>.</exception>
    Protected Overrides Sub OnRenderImageMargin(e As ToolStripRenderEventArgs)
        If e Is Nothing Then
            Throw New ArgumentNullException(NameOf(e))
        End If

        Using brush As New SolidBrush(Me.ColorTable.ImageMarginGradientBegin)
            e.Graphics.FillRectangle(brush, e.AffectedBounds)
        End Using
    End Sub

    ''' <summary>
    ''' Draws a single-pixel outline inside the client area of a drop-down menu.
    ''' </summary>
    ''' <param name="e">The strip and painting surface.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="Nothing"/>.</exception>
    Protected Overrides Sub OnRenderToolStripBorder(e As ToolStripRenderEventArgs)
        If e Is Nothing Then
            Throw New ArgumentNullException(NameOf(e))
        End If

        Dim bounds As Rectangle = e.ToolStrip.ClientRectangle
        If TypeOf e.ToolStrip Is ToolStripDropDown AndAlso bounds.Width > 1 AndAlso bounds.Height > 1 Then
            Using border As New Pen(Me.ColorTable.MenuBorder)
                e.Graphics.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1)
            End Using
        End If
    End Sub

    ''' <summary>
    ''' Draws item text in the dark palette while retaining shortcut alignment, mnemonics, and warning label colors.
    ''' </summary>
    ''' <param name="e">The text, layout, and painting surface provided by WinForms.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="Nothing"/>.</exception>
    Protected Overrides Sub OnRenderItemText(e As ToolStripItemTextRenderEventArgs)
        If e Is Nothing Then
            Throw New ArgumentNullException(NameOf(e))
        End If

        Dim textColor As Color = DarkModeColorTable.TextColor
        Dim itemColor As Color = e.Item.ForeColor
        If Not e.Item.Enabled Then
            textColor = DarkModeColorTable.DisabledTextColor
        ElseIf TypeOf e.Item Is ToolStripLabel AndAlso Not itemColor.IsSystemColor AndAlso Not itemColor.IsEmpty Then
            textColor = itemColor
        End If

        e.TextColor = textColor
        Dim bounds As Rectangle = e.TextRectangle
        If bounds.Width > 0 AndAlso bounds.Height > 0 Then
            If e.TextDirection = ToolStripTextDirection.Vertical90 OrElse
               e.TextDirection = ToolStripTextDirection.Vertical270 Then
                Using textBitmap As New Bitmap(bounds.Height, bounds.Width, PixelFormat.Format32bppPArgb)
                    textBitmap.SetResolution(e.Graphics.DpiX, e.Graphics.DpiY)
                    Using textGraphics As Graphics = Graphics.FromImage(textBitmap)
                        textGraphics.TextRenderingHint = TextRenderingHint.AntiAlias
                        Dim textBounds As New Rectangle(Point.Empty, textBitmap.Size)
                        TextRenderer.DrawText(textGraphics, e.Text, e.TextFont, textBounds, textColor, e.TextFormat)
                    End Using

                    Dim rotation As RotateFlipType = If(e.TextDirection = ToolStripTextDirection.Vertical90,
                                                        RotateFlipType.Rotate90FlipNone,
                                                        RotateFlipType.Rotate270FlipNone)
                    textBitmap.RotateFlip(rotation)
                    e.Graphics.DrawImageUnscaled(textBitmap, bounds.Location)
                End Using
            Else
                TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, bounds, textColor, e.TextFormat)
            End If
        End If
    End Sub

    ''' <summary>
    ''' Paints a single flat arrow using WinForms' direction and layout.
    ''' </summary>
    ''' <param name="e">The arrow direction, bounds, and painting surface.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="Nothing"/>.</exception>
    Protected Overrides Sub OnRenderArrow(e As ToolStripArrowRenderEventArgs)
        If e Is Nothing Then
            Throw New ArgumentNullException(NameOf(e))
        End If

        e.ArrowColor = If(e.Item.Enabled, DarkModeColorTable.TextColor, DarkModeColorTable.DisabledTextColor)
        MyBase.OnRenderArrow(e)
    End Sub

    ''' <summary>
    ''' Draws a single-pixel separator across a menu or toolbar.
    ''' </summary>
    ''' <param name="e">The separator orientation, item bounds, and painting surface.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="Nothing"/>.</exception>
    Protected Overrides Sub OnRenderSeparator(e As ToolStripSeparatorRenderEventArgs)
        If e Is Nothing Then
            Throw New ArgumentNullException(NameOf(e))
        End If

        Dim size As Size = e.Item.Size
        If size.Width > 0 AndAlso size.Height > 0 Then
            Using brush As New SolidBrush(Me.ColorTable.SeparatorDark)
                If e.Vertical Then
                    Dim inset As Integer = Math.Min(3, (size.Height - 1) \ 2)
                    e.Graphics.FillRectangle(brush, size.Width \ 2, inset, 1, size.Height - 2 * inset)
                Else
                    Dim inset As Integer = Math.Min(3, (size.Width - 1) \ 2)
                    e.Graphics.FillRectangle(brush, inset, size.Height \ 2, size.Width - 2 * inset, 1)
                End If
            End Using
        End If
    End Sub

    ''' <summary>
    ''' Draws a flat check box with a check mark or indeterminate dash in the image margin.
    ''' </summary>
    ''' <param name="e">The checked item, image bounds, and painting surface.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="Nothing"/>.</exception>
    Protected Overrides Sub OnRenderItemCheck(e As ToolStripItemImageRenderEventArgs)
        If e Is Nothing Then
            Throw New ArgumentNullException(NameOf(e))
        End If

        Dim imageBounds As Rectangle = e.ImageRectangle
        Dim side As Integer = Math.Min(imageBounds.Width, imageBounds.Height) - 2
        If side >= 7 Then
            Dim box As New Rectangle(imageBounds.X + (imageBounds.Width - side) \ 2,
                                     imageBounds.Y + (imageBounds.Height - side) \ 2, side, side)
            Using background As New SolidBrush(DarkModeColorTable.PressedBackground),
                  border As New Pen(Me.ColorTable.MenuBorder)
                e.Graphics.FillRectangle(background, box)
                e.Graphics.DrawRectangle(border, box.X, box.Y, box.Width - 1, box.Height - 1)
            End Using

            Dim menuItem As ToolStripMenuItem = DirectCast(e.Item, ToolStripMenuItem)
            Dim markColor As Color = If(menuItem.Enabled, DarkModeColorTable.AccentColor,
                                        DarkModeColorTable.DisabledTextColor)
            Dim inset As Integer = Math.Max(2, side \ 4)
            Dim centerY As Integer = box.Y + side \ 2
            Dim left As Integer = box.Left + inset
            Dim right As Integer = box.Right - inset - 1

            Using mark As New Pen(markColor, CSng(Math.Max(1, side \ 7)))
                If menuItem.CheckState = CheckState.Indeterminate Then
                    e.Graphics.DrawLine(mark, left, centerY, right, centerY)
                Else
                    Dim points As Point() = {
                        New Point(left, centerY),
                        New Point(box.X + side \ 2 - 1, box.Bottom - inset - 1),
                        New Point(right, box.Top + inset)
                    }
                    e.Graphics.DrawLines(mark, points)
                End If
            End Using
        End If
    End Sub

    ''' <summary>
    ''' Draws a flat highlight for a selected menu item or an active drop-down header.
    ''' </summary>
    ''' <param name="e">The item and painting surface.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="Nothing"/>.</exception>
    Protected Overrides Sub OnRenderMenuItemBackground(e As ToolStripItemRenderEventArgs)
        If e Is Nothing Then
            Throw New ArgumentNullException(NameOf(e))
        End If

        Me.PaintItemState(e)
    End Sub

    ''' <summary>
    ''' Draws toolbar button states and a narrow indicator for a checked button.
    ''' </summary>
    ''' <param name="e">The button and painting surface.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="Nothing"/>.</exception>
    Protected Overrides Sub OnRenderButtonBackground(e As ToolStripItemRenderEventArgs)
        If e Is Nothing Then
            Throw New ArgumentNullException(NameOf(e))
        End If

        Dim button As ToolStripButton = DirectCast(e.Item, ToolStripButton)
        Dim isChecked As Boolean = button.CheckState <> CheckState.Unchecked
        Me.PaintItemState(e, isChecked)

        If isChecked AndAlso button.Width > 2 AndAlso button.Height > 1 Then
            Dim color As Color = If(button.Enabled, DarkModeColorTable.AccentColor,
                                    DarkModeColorTable.DisabledTextColor)
            Using brush As New SolidBrush(color)
                e.Graphics.FillRectangle(brush, 1, button.Height - 2, button.Width - 2, 1)
            End Using
        End If
    End Sub

    ''' <summary>
    ''' Draws the flat interaction state of a toolbar drop-down button.
    ''' </summary>
    ''' <param name="e">The button and painting surface.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="Nothing"/>.</exception>
    Protected Overrides Sub OnRenderDropDownButtonBackground(e As ToolStripItemRenderEventArgs)
        If e Is Nothing Then
            Throw New ArgumentNullException(NameOf(e))
        End If

        Me.PaintItemState(e)
    End Sub

    ''' <summary>
    ''' Draws a toolbar split button with flat states and a single-pixel divider.
    ''' </summary>
    ''' <param name="e">The split button and painting surface.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="Nothing"/>.</exception>
    Protected Overrides Sub OnRenderSplitButtonBackground(e As ToolStripItemRenderEventArgs)
        If e Is Nothing Then
            Throw New ArgumentNullException(NameOf(e))
        End If

        Dim button As ToolStripSplitButton = DirectCast(e.Item, ToolStripSplitButton)
        Me.PaintItemState(e)

        If button.Enabled AndAlso button.ButtonPressed Then
            Using brush As New SolidBrush(DarkModeColorTable.PressedBackground)
                e.Graphics.FillRectangle(brush, button.ButtonBounds)
            End Using
        End If

        Dim splitter As Rectangle = button.SplitterBounds
        If splitter.Width > 0 AndAlso splitter.Height > 0 Then
            Using brush As New SolidBrush(Me.ColorTable.SeparatorDark)
                e.Graphics.FillRectangle(brush, splitter.X, splitter.Y, 1, splitter.Height)
            End Using
        End If

        Me.DrawArrow(New ToolStripArrowRenderEventArgs(e.Graphics, button, button.DropDownButtonBounds,
                                                       DarkModeColorTable.TextColor, ArrowDirection.Down))
    End Sub

    ''' <summary>
    ''' Draws a flat overflow button with a direction appropriate to the toolbar orientation.
    ''' </summary>
    ''' <param name="e">The overflow button and painting surface.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="Nothing"/>.</exception>
    ''' <exception cref="InvalidOperationException">The overflow button is missing its parent strip.</exception>
    Protected Overrides Sub OnRenderOverflowButtonBackground(e As ToolStripItemRenderEventArgs)
        If e Is Nothing Then
            Throw New ArgumentNullException(NameOf(e))
        End If

        Dim button As ToolStripItem = e.Item
        Dim parent As ToolStrip = button.GetCurrentParent()
        If parent Is Nothing Then
            Throw New InvalidOperationException("The overflow button must have a parent strip.")
        End If

        Me.PaintItemState(e)
        Dim direction As ArrowDirection = ArrowDirection.Down
        If parent.Orientation = Orientation.Vertical Then
            direction = If(parent.RightToLeft = RightToLeft.Yes, ArrowDirection.Left, ArrowDirection.Right)
        End If

        Dim bounds As New Rectangle(Point.Empty, button.Size)
        Me.DrawArrow(New ToolStripArrowRenderEventArgs(e.Graphics, button, bounds,
                                                       DarkModeColorTable.TextColor, direction))
    End Sub

    ''' <summary>
    ''' Draws the toolbar move handle as flat dots.
    ''' </summary>
    ''' <param name="e">The grip bounds, display style, and painting surface.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="Nothing"/>.</exception>
    Protected Overrides Sub OnRenderGrip(e As ToolStripGripRenderEventArgs)
        If e Is Nothing Then
            Throw New ArgumentNullException(NameOf(e))
        End If

        Dim bounds As Rectangle = e.GripBounds
        Using brush As New SolidBrush(Me.ColorTable.SeparatorDark)
            If e.GripDisplayStyle = ToolStripGripDisplayStyle.Vertical Then
                Dim x As Integer = bounds.Left + (bounds.Width - 2) \ 2
                For y As Integer = bounds.Top + 2 To bounds.Bottom - 4 Step 4
                    e.Graphics.FillRectangle(brush, x, y, 2, 2)
                Next
            Else
                Dim y As Integer = bounds.Top + (bounds.Height - 2) \ 2
                For x As Integer = bounds.Left + 2 To bounds.Right - 4 Step 4
                    e.Graphics.FillRectangle(brush, x, y, 2, 2)
                Next
            End If
        End Using
    End Sub

    ''' <summary>
    ''' Fills the item with the solid color for its pressed, selected, or latched state.
    ''' </summary>
    ''' <param name="e">The validated item rendering arguments.</param>
    ''' <param name="latched">Whether an enabled checkable toolbar button is latched.</param>
    Private Sub PaintItemState(e As ToolStripItemRenderEventArgs, Optional latched As Boolean = False)
        Dim item As ToolStripItem = e.Item
        If item.Enabled AndAlso (item.Pressed OrElse item.Selected OrElse latched) Then
            Dim background As Color = If(item.Pressed OrElse (latched AndAlso Not item.Selected),
                                         DarkModeColorTable.PressedBackground,
                                         Me.ColorTable.MenuItemSelected)
            Dim bounds As New Rectangle(Point.Empty, item.Size)
            Using brush As New SolidBrush(background)
                e.Graphics.FillRectangle(brush, bounds)
            End Using
        End If
    End Sub

End Class
