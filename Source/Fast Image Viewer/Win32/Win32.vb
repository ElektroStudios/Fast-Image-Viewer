
#Region " Option Statements "

Option Strict On
Option Explicit On
Option Infer Off

#End Region

#Region " Win32 "

Friend Module Win32

    Friend Const GWL_STYLE As Integer = -16
    Friend Const WS_CAPTION As Integer = &HC00000
    Friend Const WS_THICKFRAME As Integer = &H40000

    Friend Const SWP_FRAMECHANGED As UInteger = &H20UI
    Friend Const SWP_NOZORDER As UInteger = &H4UI
    Friend Const SWP_NOACTIVATE As UInteger = &H10UI

    Friend Const DWMWA_USE_IMMERSIVE_DARK_MODE As Integer = 20

    Friend Const SPI_SETDESKWALLPAPER As UInteger = &H14UI
    Friend Const SPIF_UPDATEINIFILE As UInteger = &H1UI
    Friend Const SPIF_SENDCHANGE As UInteger = &H2UI

End Module

#End Region
