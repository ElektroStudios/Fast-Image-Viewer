Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Drawing
Imports Xunit

Public Class ImageSizingTests
    <Theory>
    <InlineData(4000, 2000, 1920, 1080, False, False, 1920, 960)>
    <InlineData(2000, 4000, 1920, 1080, False, False, 540, 1080)>
    <InlineData(200, 100, 1920, 1080, False, False, 200, 100)>
    <InlineData(200, 100, 1920, 1080, False, True, 1920, 960)>
    <InlineData(4000, 2000, 1920, 1080, True, False, 4000, 2000)>
    <InlineData(200, 100, 1920, 1080, True, True, 200, 100)>
    <InlineData(1, 100000, 1920, 1080, False, False, 1, 1080)>
    <InlineData(100000, 1, 1920, 1080, False, False, 1919, 1)>
    <InlineData(1920, 1080, 1920, 1080, False, True, 1920, 1080)>
    Public Sub RespectsFitAndResizePreferences(w As Integer, h As Integer, sw As Integer, sh As Integer, disabled As Boolean, upscale As Boolean, ew As Integer, eh As Integer)
        Assert.Equal(New Size(ew, eh), ImageSizing.GetLoadSize(w, h, sw, sh, disabled, upscale))
    End Sub

    <Theory>
    <InlineData(1, 1)>
    <InlineData(1, 2147483647)>
    <InlineData(2147483647, 1)>
    <InlineData(640, 480)>
    <InlineData(3840, 2160)>
    <InlineData(10000, 30000)>
    <InlineData(30000, 10000)>
    <InlineData(1920, 1080)>
    <InlineData(1919, 1079)>
    <InlineData(17, 31)>
    <InlineData(31, 17)>
    Public Sub FittedDimensionsAreAlwaysPositiveAndInsideTheScreen(width As Integer, height As Integer)
        For Each screen As Size In New Size() {New Size(1, 1), New Size(1920, 1080), New Size(3840, 2160)}
            For Each upscale As Boolean In New Boolean() {False, True}
                Dim actual As Size = ImageSizing.GetLoadSize(width, height, screen.Width, screen.Height, False, upscale)
                Assert.InRange(actual.Width, 1, screen.Width)
                Assert.InRange(actual.Height, 1, screen.Height)
                Assert.Equal(New Size(width, height), ImageSizing.GetLoadSize(width, height, screen.Width, screen.Height, True, upscale))
            Next
        Next
    End Sub

    <Theory>
    <InlineData(0, 1, 1, 1)>
    <InlineData(1, 0, 1, 1)>
    <InlineData(1, 1, 0, 1)>
    <InlineData(1, 1, 1, 0)>
    <InlineData(-1, 1, 1, 1)>
    Public Sub RejectsInvalidDimensions(w As Integer, h As Integer, sw As Integer, sh As Integer)
        Assert.Throws(Of ArgumentOutOfRangeException)(Function() ImageSizing.GetLoadSize(w, h, sw, sh, False, False))
    End Sub
End Class
