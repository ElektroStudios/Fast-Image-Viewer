
#Region " Option Statements "

Option Strict On
Option Explicit On
Option Infer Off

#End Region

#Region " Imports "

Imports System.Runtime.InteropServices
Imports System.Security

#End Region

#Region " NativeMethods "

<SuppressUnmanagedCodeSecurity>
Friend Module NativeMethods

    <DllImport("user32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Friend Function SystemParametersInfo(uAction As UInteger, uParam As UInteger, lpvParam As String, fuWinIni As UInteger) As Boolean
    End Function

    <DllImport("user32.dll")>
    Friend Function GetWindowLong(hWnd As IntPtr, nIndex As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Friend Function SetWindowLong(hWnd As IntPtr, nIndex As Integer, dwNewLong As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Friend Function SetWindowPos(hWnd As IntPtr, hWndInsertAfter As IntPtr,
                                         X As Integer, Y As Integer,
                                         cx As Integer, cy As Integer,
                                         uFlags As UInteger) As Boolean
    End Function

    <DllImport("dwmapi.dll")>
    Friend Function DwmSetWindowAttribute(hWnd As IntPtr, attr As Integer, ByRef attrValue As Integer, attrSize As Integer) As Integer
    End Function

    <DllImport("shlwapi.dll", CharSet:=CharSet.Unicode, ExactSpelling:=True)>
    Friend Function StrCmpLogicalW(x As String, y As String) As Integer
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Friend Function SendMessage(hWnd As IntPtr, Msg As Integer, wParam As Integer, lParam As Integer) As IntPtr
    End Function

End Module

#End Region
