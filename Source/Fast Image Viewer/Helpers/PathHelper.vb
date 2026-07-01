
#Region " Option Statements "

Option Strict On
Option Explicit On
Option Infer Off

#End Region

#Region " Imports "

Imports System.Diagnostics
Imports System.IO

#End Region

#Region " PathHelper "

''' <summary>
''' Provides helper methods for manipulating Windows file system paths.
''' </summary>
Friend Module PathHelper

#Region " Static Methods "

    ''' <summary>
    ''' Converts a standard path into an Extended-Length Path (prefixed with \\?\) to bypass the 260 character MAX_PATH limitation.
    ''' </summary>
    ''' 
    ''' <param name="targetPath">
    ''' The absolute path to convert.
    ''' </param>
    ''' 
    ''' <returns>
    ''' The extended-length path string.
    ''' </returns>
    <DebuggerStepThrough>
    Friend Function GetExtendedPath(targetPath As String) As String

        If String.IsNullOrWhiteSpace(targetPath) Then
            Return targetPath
        End If

        ' Already an extended path.
        If targetPath.StartsWith("\\?\", StringComparison.Ordinal) Then
            Return targetPath
        End If

        ' Relative paths cannot be converted to Extended-Length paths.
        If Not Path.IsPathRooted(targetPath) Then
            Return targetPath
        End If

        ' Handle UNC paths: \\Server\Share -> \\?\UNC\Server\Share
        If targetPath.StartsWith("\\", StringComparison.Ordinal) Then
            Return $"\\?\UNC\{targetPath.Substring(2)}"
        End If

        ' Handle Local paths: C:\Folder -> \\?\C:\Folder
        Return $"\\?\{targetPath}"
    End Function

    ''' <summary>
    ''' Strips the Extended-Length Path prefix for UI display or Shell interop.
    ''' </summary>
    <DebuggerStepThrough>
    Friend Function GetNormalPath(targetPath As String) As String

        Return If(String.IsNullOrWhiteSpace(targetPath),
            targetPath,
            If(targetPath.StartsWith("\\?\UNC\", StringComparison.OrdinalIgnoreCase),
            $"\\{targetPath.Substring(8)}",
            If(targetPath.StartsWith("\\?\", StringComparison.Ordinal), targetPath.Substring(4), targetPath)))
    End Function

#End Region

End Module

#End Region
