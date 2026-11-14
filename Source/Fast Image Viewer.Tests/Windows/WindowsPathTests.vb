Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.Collections.Generic
Imports Xunit

Public Class WindowsPathTests
    <Theory>
    <InlineData("C:\images\a.jpg", "\\?\C:\images\a.jpg")>
    <InlineData("\\server\share\a.jpg", "\\?\UNC\server\share\a.jpg")>
    <InlineData("\\?\C:\images\a.jpg", "\\?\C:\images\a.jpg")>
    <InlineData("relative.jpg", "relative.jpg")>
    <InlineData("", "")>
    <InlineData(Nothing, Nothing)>
    Public Sub ExtendsAbsolutePaths(path As String, expected As String)
        Assert.Equal(expected, PathHelper.GetExtendedPath(path))
    End Sub

    <Theory>
    <InlineData("image1.jpg", 0)>
    <InlineData("image3.jpg", 1)>
    <InlineData("image9.jpg", 1)>
    <InlineData("image20.jpg", 2)>
    <InlineData("image99.jpg", 2)>
    Public Sub NaturalOrderAnchorUsesWindowsComparer(anchor As String, expected As Integer)
        Dim files As String() = {"image2.jpg", "image10.jpg", "image20.jpg"}
        Assert.Equal(expected, ImageFileCatalog.GetInsertionIndex(files, anchor, Comparer(Of String).Create(AddressOf NativeMethods.StrCmpLogicalW)))
    End Sub
End Class
