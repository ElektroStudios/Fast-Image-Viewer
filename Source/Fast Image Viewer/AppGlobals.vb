
#Region " Option Statements "

Option Strict On
Option Explicit On
Option Infer Off

#End Region

#Region " AppGlobals "

Friend Module AppGlobals

    Friend ReadOnly SupportedExtensions As String() = {
        ".jpg", ".jpeg",
        ".png",
        ".bmp",
        ".tif", ".tiff",
        ".webp"
    }

    Friend Const GitHubRepositoruUrl As String = ""

    ''' <summary>
    ''' Milliseconds to wait before committing a navigation when keys
    ''' are held down or pressed rapidly.  Lower = more responsive but
    ''' more intermediate loads; higher = smoother skip-through.
    ''' 50 ms is a good sweet-spot: fast enough to feel instant on a
    ''' single tap, slow enough to coalesce a held key.
    ''' </summary>
    Friend Const NAV_THROTTLE_MS As Integer = 50

    Friend Const MOUSE_WHEEL_INVERTED As Boolean = False

    Friend Const PAN_KEY_STEP As Integer = 40

End Module

#End Region
