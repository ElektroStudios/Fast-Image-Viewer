Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Drawing
Imports Xunit

Public Class PathTests
    <Theory>
    <InlineData(Nothing, Nothing)>
    <InlineData("", "")>
    <InlineData(" ", " ")>
    <InlineData("C:\images\a.jpg", "C:\images\a.jpg")>
    <InlineData("\\?\C:\images\a.jpg", "C:\images\a.jpg")>
    <InlineData("\\?\UNC\server\share\a.jpg", "\\server\share\a.jpg")>
    <InlineData("\\?\unc\server\share\a.jpg", "\\server\share\a.jpg")>
    <InlineData("relative.jpg", "relative.jpg")>
    Public Sub NormalizesDisplayPaths(path As String, expected As String)
        Assert.Equal(expected, PathHelper.GetNormalPath(path))
    End Sub
End Class
