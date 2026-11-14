Option Strict On
Option Explicit On
Option Infer Off

Imports System.Collections.Generic
Imports System.IO

''' <summary>Provides file filtering, indexing and sorted-list anchor lookup.</summary>
Friend NotInheritable Class ImageFileCatalog ' NOSONAR
    Private Sub New()
    End Sub

    ''' <summary>Tests an extension against the viewer's supported file types.</summary>
    Friend Shared Function IsSupported(filePath As String) As Boolean
        Dim extension As String = Path.GetExtension(filePath)
        For Each supported As String In AppGlobals.SupportedExtensions
            If String.Equals(extension, supported, StringComparison.OrdinalIgnoreCase) Then Return True
        Next
        Return False
    End Function

    ''' <summary>Builds the case-insensitive lookup used during eviction.</summary>
    Friend Shared Function CreateIndex(files As String()) As Dictionary(Of String, Integer)
        If files Is Nothing Then Throw New ArgumentNullException(NameOf(files))
        Dim result As New Dictionary(Of String, Integer)(files.Length, StringComparer.OrdinalIgnoreCase)
        For i As Integer = 0 To files.Length - 1
            result(files(i)) = i
        Next
        Return result
    End Function

    ''' <summary>Finds the successor of a removed file, or the last remaining file.</summary>
    Friend Shared Function GetInsertionIndex(files As String(), anchorFilePath As String, comparer As IComparer(Of String)) As Integer
        If files Is Nothing OrElse files.Length = 0 Then
            Return 0
        End If

        Dim lo As Integer = 0
        Dim hi As Integer = files.Length

        Do While lo < hi
            Dim mid As Integer = lo + ((hi - lo) \ 2)
            Dim cmp As Integer = comparer.Compare(files(mid), anchorFilePath)

            If cmp < 0 Then
                lo = mid + 1
            Else
                hi = mid
            End If
        Loop

        Return If(lo >= files.Length, files.Length - 1, lo)
    End Function
End Class
