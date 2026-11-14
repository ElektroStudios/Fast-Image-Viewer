Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Drawing
Imports Xunit

Public Class FileSizeFormatterTests
    <Theory>
    <InlineData(0L, "0 Bytes")>
    <InlineData(1L, "1 Bytes")>
    <InlineData(1023L, "1,023 Bytes")>
    <InlineData(1024L, "1.00 KB")>
    <InlineData(1536L, "1.50 KB")>
    <InlineData(1048575L, "1024.00 KB")>
    <InlineData(1048576L, "1.00 MB")>
    <InlineData(1073741823L, "1024.00 MB")>
    <InlineData(1073741824L, "1.00 GB")>
    <InlineData(1610612736L, "1.50 GB")>
    Public Sub PreservesBinaryUnitsAndBoundaryRounding(value As Long, expected As String)
        Dim previous As CultureInfo = CultureInfo.CurrentCulture
        Try
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US")
            Assert.Equal(expected, FileSizeFormatter.FormatFileSize(value))
        Finally
            CultureInfo.CurrentCulture = previous
        End Try
    End Sub

    <Fact>
    Public Sub UsesCurrentCulture()
        Dim previous As CultureInfo = CultureInfo.CurrentCulture
        Try
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-ES")
            Assert.Equal("1,50 KB", FileSizeFormatter.FormatFileSize(1536))
        Finally
            CultureInfo.CurrentCulture = previous
        End Try
    End Sub
End Class
