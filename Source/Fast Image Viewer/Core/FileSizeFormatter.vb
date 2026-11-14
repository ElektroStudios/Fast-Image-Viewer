Option Strict On
Option Explicit On
Option Infer Off

''' <summary>Formats file lengths using the current UI culture.</summary>
Friend NotInheritable Class FileSizeFormatter
    Private Sub New()
    End Sub

    ''' <summary>Returns the existing binary-unit display text.</summary>
    Friend Shared Function FormatFileSize(fileSize As Long) As String

        Dim bytes As Double = Convert.ToDouble(fileSize)
        Dim unit As String = "Bytes"

        If fileSize >= 1073741824 Then
            unit = "GB"
            bytes = fileSize / 1073741824.0

        ElseIf fileSize >= 1048576 Then
            unit = "MB"
            bytes = fileSize / 1048576.0

        ElseIf fileSize >= 1024 Then
            unit = "KB"
            bytes = fileSize / 1024.0

        End If

        Return If(unit = "Bytes", $"{fileSize:N0} Bytes", $"{bytes:F2} {unit}")
    End Function
End Class
