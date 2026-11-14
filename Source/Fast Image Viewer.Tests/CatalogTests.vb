Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Drawing
Imports Xunit

Public Class CatalogTests
    <Theory>
    <InlineData("a.jpg", True)>
    <InlineData("a.JPEG", True)>
    <InlineData("a.PnG", True)>
    <InlineData("a.bmp", True)>
    <InlineData("a.tif", True)>
    <InlineData("a.TIFF", True)>
    <InlineData("a.webp", True)>
    <InlineData("a.gif", False)>
    <InlineData("a.svg", False)>
    <InlineData("a.txt", False)>
    <InlineData("a.jpg.txt", False)>
    <InlineData("a", False)>
    <InlineData("a.", False)>
    <InlineData("", False)>
    <InlineData("a.png ", False)>
    Public Sub FiltersOnlySupportedExtensions(path As String, expected As Boolean)
        Assert.Equal(expected, ImageFileCatalog.IsSupported(path))
    End Sub

    <Fact>
    Public Sub IndexIsCaseInsensitiveAndUsesLatestDuplicate()
        Dim map As Dictionary(Of String, Integer) = ImageFileCatalog.CreateIndex(New String() {"A.jpg", "b.jpg", "a.JPG"})
        Assert.Equal(2, map.Count)
        Assert.Equal(2, map("A.JPG"))
        Assert.Equal(1, map("B.JPG"))
    End Sub

    <Fact>
    Public Sub EmptyIndexIsEmpty()
        Assert.Empty(ImageFileCatalog.CreateIndex(Array.Empty(Of String)()))
    End Sub

    <Fact>
    Public Sub NullIndexFailsFast()
        Assert.Throws(Of ArgumentNullException)(Function() ImageFileCatalog.CreateIndex(Nothing))
    End Sub

    <Theory>
    <InlineData("a", 0)>
    <InlineData("b", 0)>
    <InlineData("c", 1)>
    <InlineData("d", 1)>
    <InlineData("e", 2)>
    <InlineData("f", 2)>
    <InlineData("z", 2)>
    Public Sub RemovedAnchorSelectsItsSuccessorOrLastFile(anchor As String, expected As Integer)
        Assert.Equal(expected, ImageFileCatalog.GetInsertionIndex(New String() {"b", "d", "f"}, anchor, StringComparer.Ordinal))
    End Sub

    <Fact>
    Public Sub EmptyAndNullCatalogReturnZero()
        Assert.Equal(0, ImageFileCatalog.GetInsertionIndex(Nothing, "x", StringComparer.Ordinal))
        Assert.Equal(0, ImageFileCatalog.GetInsertionIndex(Array.Empty(Of String)(), "x", StringComparer.Ordinal))
    End Sub

    <Fact>
    Public Sub BinarySearchMatchesLinearSuccessorAcrossLargeCatalog()
        Dim files As String() = Enumerable.Range(0, 15000).Select(Function(i As Integer) (i * 2).ToString("D6", CultureInfo.InvariantCulture)).ToArray()
        For i As Integer = 0 To 30001 Step 37
            Dim expected As Integer = Math.Min((i + 1) \ 2, files.Length - 1)
            Assert.Equal(expected, ImageFileCatalog.GetInsertionIndex(files, i.ToString("D6", CultureInfo.InvariantCulture), StringComparer.Ordinal))
        Next
    End Sub
End Class
