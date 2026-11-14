Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Drawing
Imports Xunit

Public Class CacheWindowTests
    <Theory>
    <InlineData(10, 5, 2, 3, New Integer() {5, 6, 4, 7, 3, 8})>
    <InlineData(10, 0, 5, 3, New Integer() {0, 1, 2, 3})>
    <InlineData(10, 9, 3, 5, New Integer() {9, 8, 7, 6})>
    <InlineData(1, 0, 50, 50, New Integer() {0})>
    <InlineData(10, 4, 0, 0, New Integer() {4})>
    <InlineData(10, 4, 0, 3, New Integer() {4, 5, 6, 7})>
    <InlineData(10, 4, 3, 0, New Integer() {4, 3, 2, 1})>
    Public Sub LoadOrderPreservesForwardFirstAlternation(count As Integer, center As Integer, backward As Integer, forward As Integer, expected As Integer())
        Assert.Equal(expected, New CacheWindow(count, center, backward, forward).CreateLoadOrder().ToArray())
    End Sub

    <Theory>
    <InlineData(1, 0, 0)>
    <InlineData(1, 0, 5)>
    <InlineData(1, 5, 0)>
    <InlineData(1, 1, 1)>
    <InlineData(1, 5, 50)>
    <InlineData(1, 50, 5)>
    <InlineData(1, 100, 100)>
    <InlineData(1, 2147483647, 2147483647)>
    <InlineData(2, 0, 0)>
    <InlineData(2, 0, 5)>
    <InlineData(2, 5, 0)>
    <InlineData(2, 1, 1)>
    <InlineData(2, 5, 50)>
    <InlineData(2, 50, 5)>
    <InlineData(2, 100, 100)>
    <InlineData(2, 2147483647, 2147483647)>
    <InlineData(3, 0, 0)>
    <InlineData(3, 0, 5)>
    <InlineData(3, 5, 0)>
    <InlineData(3, 1, 1)>
    <InlineData(3, 5, 50)>
    <InlineData(3, 50, 5)>
    <InlineData(3, 100, 100)>
    <InlineData(3, 2147483647, 2147483647)>
    <InlineData(10, 0, 0)>
    <InlineData(10, 0, 5)>
    <InlineData(10, 5, 0)>
    <InlineData(10, 1, 1)>
    <InlineData(10, 5, 50)>
    <InlineData(10, 50, 5)>
    <InlineData(10, 100, 100)>
    <InlineData(10, 2147483647, 2147483647)>
    <InlineData(51, 0, 0)>
    <InlineData(51, 0, 5)>
    <InlineData(51, 5, 0)>
    <InlineData(51, 1, 1)>
    <InlineData(51, 5, 50)>
    <InlineData(51, 50, 5)>
    <InlineData(51, 100, 100)>
    <InlineData(51, 2147483647, 2147483647)>
    <InlineData(100, 0, 0)>
    <InlineData(100, 0, 5)>
    <InlineData(100, 5, 0)>
    <InlineData(100, 1, 1)>
    <InlineData(100, 5, 50)>
    <InlineData(100, 50, 5)>
    <InlineData(100, 100, 100)>
    <InlineData(100, 2147483647, 2147483647)>
    Public Sub EveryCenterHasCompleteBoundedUniqueWindow(count As Integer, backward As Integer, forward As Integer)
        For center As Integer = 0 To count - 1
            Dim window As New CacheWindow(count, center, backward, forward)
            Dim actual As List(Of Integer) = window.CreateLoadOrder()
            Assert.Equal(center, actual(0))
            Assert.Equal(window.Last - window.First + 1, actual.Count)
            Assert.Equal(actual.Count, actual.Distinct().Count())
            Assert.Equal(Enumerable.Range(window.First, actual.Count).ToArray(), actual.OrderBy(Function(value As Integer) value).ToArray())
            Dim centerCopy As Integer = center
            Dim distances As Integer() = actual.Select(Function(value As Integer) Math.Abs(value - centerCopy)).ToArray()
            Assert.Equal(distances.OrderBy(Function(value As Integer) value).ToArray(), distances)
        Next
    End Sub

    <Theory>
    <InlineData(0, 0, 0, 0)>
    <InlineData(-1, 0, 0, 0)>
    <InlineData(5, -1, 0, 0)>
    <InlineData(5, 5, 0, 0)>
    <InlineData(5, 0, -1, 0)>
    <InlineData(5, 0, 0, -1)>
    Public Sub InvalidWindowsFailFast(count As Integer, center As Integer, backward As Integer, forward As Integer)
        Assert.Throws(Of ArgumentOutOfRangeException)(Sub() NewWindow(count, center, backward, forward))
    End Sub

    Private Shared Sub NewWindow(count As Integer, center As Integer, backward As Integer, forward As Integer)
        Dim ignored As New CacheWindow(count, center, backward, forward)
    End Sub
End Class
