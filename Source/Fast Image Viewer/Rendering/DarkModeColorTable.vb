Option Strict On
Option Explicit On
Option Infer Off

Imports System.Drawing
Imports System.Windows.Forms

''' <summary>
''' Defines the flat dark palette for menu strips, toolbars, and drop-down menus.
''' </summary>
Friend NotInheritable Class DarkModeColorTable
    Inherits ProfessionalColorTable

    ''' <summary>
    ''' Gets the background color of menu strips and toolbars.
    ''' </summary>
    Friend Shared ReadOnly Property StripBackground As Color = Color.FromArgb(30, 30, 30)

    ''' <summary>
    ''' Gets the background color of drop-down menus.
    ''' </summary>
    Friend Shared ReadOnly Property DropDownSurface As Color = Color.FromArgb(37, 37, 38)

    ''' <summary>
    ''' Gets the background color of hovered and keyboard-selected items.
    ''' </summary>
    Friend Shared ReadOnly Property SelectedBackground As Color = Color.FromArgb(62, 62, 66)

    ''' <summary>
    ''' Gets the background color of pressed items and check boxes.
    ''' </summary>
    Friend Shared ReadOnly Property PressedBackground As Color = Color.FromArgb(45, 45, 48)

    ''' <summary>
    ''' Gets the color of flat borders and separators.
    ''' </summary>
    Friend Shared ReadOnly Property BorderColor As Color = Color.FromArgb(63, 63, 70)

    ''' <summary>
    ''' Gets the foreground color of enabled items.
    ''' </summary>
    Friend Shared ReadOnly Property TextColor As Color = Color.WhiteSmoke

    ''' <summary>
    ''' Gets the foreground color of disabled items.
    ''' </summary>
    Friend Shared ReadOnly Property DisabledTextColor As Color = Color.FromArgb(112, 112, 112)

    ''' <summary>
    ''' Gets the color of enabled check marks and checked toolbar indicators.
    ''' </summary>
    Friend Shared ReadOnly Property AccentColor As Color = Color.FromArgb(0, 122, 204)

    ''' <summary>
    ''' Gets the background color of text and numeric editors hosted in the menus.
    ''' </summary>
    Friend Shared ReadOnly Property InputBackground As Color = Color.FromArgb(50, 50, 50)

    ''' <summary>
    ''' Initializes the color table with the application palette.
    ''' </summary>
    Friend Sub New()
        Me.UseSystemColors = False
    End Sub

    ''' <summary>
    ''' Gets the starting color for the menu strip background.
    ''' </summary>
    Public Overrides ReadOnly Property MenuStripGradientBegin As Color
        Get
            Return StripBackground
        End Get
    End Property

    ''' <summary>
    ''' Gets the ending color for the menu strip background.
    ''' </summary>
    Public Overrides ReadOnly Property MenuStripGradientEnd As Color
        Get
            Return StripBackground
        End Get
    End Property

    ''' <summary>
    ''' Gets the starting color for the toolbar background.
    ''' </summary>
    Public Overrides ReadOnly Property ToolStripGradientBegin As Color
        Get
            Return StripBackground
        End Get
    End Property

    ''' <summary>
    ''' Gets the middle color for the toolbar background.
    ''' </summary>
    Public Overrides ReadOnly Property ToolStripGradientMiddle As Color
        Get
            Return StripBackground
        End Get
    End Property

    ''' <summary>
    ''' Gets the ending color for the toolbar background.
    ''' </summary>
    Public Overrides ReadOnly Property ToolStripGradientEnd As Color
        Get
            Return StripBackground
        End Get
    End Property

    ''' <summary>
    ''' Gets the drop-down background color.
    ''' </summary>
    Public Overrides ReadOnly Property ToolStripDropDownBackground As Color
        Get
            Return DropDownSurface
        End Get
    End Property

    ''' <summary>
    ''' Gets the starting color for the drop-down image margin.
    ''' </summary>
    Public Overrides ReadOnly Property ImageMarginGradientBegin As Color
        Get
            Return DropDownSurface
        End Get
    End Property

    ''' <summary>
    ''' Gets the middle color for the drop-down image margin.
    ''' </summary>
    Public Overrides ReadOnly Property ImageMarginGradientMiddle As Color
        Get
            Return DropDownSurface
        End Get
    End Property

    ''' <summary>
    ''' Gets the ending color for the drop-down image margin.
    ''' </summary>
    Public Overrides ReadOnly Property ImageMarginGradientEnd As Color
        Get
            Return DropDownSurface
        End Get
    End Property

    ''' <summary>
    ''' Gets the selected menu item color.
    ''' </summary>
    Public Overrides ReadOnly Property MenuItemSelected As Color
        Get
            Return SelectedBackground
        End Get
    End Property

    ''' <summary>
    ''' Gets the drop-down menu border color.
    ''' </summary>
    Public Overrides ReadOnly Property MenuBorder As Color
        Get
            Return BorderColor
        End Get
    End Property

    ''' <summary>
    ''' Gets the separator color.
    ''' </summary>
    Public Overrides ReadOnly Property SeparatorDark As Color
        Get
            Return BorderColor
        End Get
    End Property

    ''' <summary>
    ''' Gets the check background when a checked menu item displays an image.
    ''' </summary>
    Public Overrides ReadOnly Property CheckBackground As Color
        Get
            Return PressedBackground
        End Get
    End Property

    ''' <summary>
    ''' Gets the check background when a checked image is selected.
    ''' </summary>
    Public Overrides ReadOnly Property CheckSelectedBackground As Color
        Get
            Return PressedBackground
        End Get
    End Property

    ''' <summary>
    ''' Gets the check background when a checked image is pressed.
    ''' </summary>
    Public Overrides ReadOnly Property CheckPressedBackground As Color
        Get
            Return PressedBackground
        End Get
    End Property

    ''' <summary>
    ''' Gets the outline indicating a checked image in a shared image margin.
    ''' </summary>
    Public Overrides ReadOnly Property ButtonSelectedBorder As Color
        Get
            Return AccentColor
        End Get
    End Property

End Class
