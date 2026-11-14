Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.Threading
Imports Xunit

Public Class DeferredDisposalTests
    <Fact>
    Public Sub EmptyDrainDoesNothing()
        Dim queue As New DeferredDisposalQueue(Of Resource)()
        queue.Drain(Nothing)
        Dim value As Resource = Nothing
        Assert.False(queue.TryDequeue(value))
    End Sub

    <Fact>
    Public Sub RetiredResourceIsDisposedExactlyOnce()
        Dim queue As New DeferredDisposalQueue(Of Resource)()
        Dim value As New Resource()
        queue.Enqueue(value)
        queue.Drain(Nothing)
        queue.Drain(Nothing)
        Assert.Equal(1, value.DisposeCount)
    End Sub

    <Fact>
    Public Sub DisplayedResourceSurvivesRepeatedDrains()
        Dim queue As New DeferredDisposalQueue(Of Resource)()
        Dim value As New Resource()
        queue.Enqueue(value)
        For i As Integer = 1 To 10
            queue.Drain(value)
        Next
        Assert.Equal(0, value.DisposeCount)
        queue.Drain(Nothing)
        Assert.Equal(1, value.DisposeCount)
    End Sub

    <Fact>
    Public Sub RetentionUsesIdentityRatherThanValueEquality()
        Dim queue As New DeferredDisposalQueue(Of Resource)()
        Dim retired As New Resource()
        Dim displayed As New Resource()
        queue.Enqueue(retired)
        queue.Enqueue(displayed)
        queue.Drain(displayed)
        Assert.Equal(1, retired.DisposeCount)
        Assert.Equal(0, displayed.DisposeCount)
        queue.Drain(Nothing)
        Assert.Equal(1, displayed.DisposeCount)
    End Sub

    <Fact>
    Public Sub NullEntriesAreIgnored()
        Dim queue As New DeferredDisposalQueue(Of Resource)()
        queue.Enqueue(Nothing)
        queue.Drain(Nothing)
        Dim value As Resource = Nothing
        Assert.False(queue.TryDequeue(value))
    End Sub

    <Fact>
    Public Sub ShutdownCanTakeOwnershipWithoutDisposingTwice()
        Dim queue As New DeferredDisposalQueue(Of Resource)()
        Dim value As New Resource()
        queue.Enqueue(value)
        Dim removed As Resource = Nothing
        Assert.True(queue.TryDequeue(removed))
        Assert.Same(value, removed)
        Assert.Equal(0, value.DisposeCount)
        removed.Dispose()
        queue.Drain(Nothing)
        Assert.Equal(1, value.DisposeCount)
    End Sub

    <Fact>
    Public Sub ConcurrentProducersDoNotLoseResources()
        Dim queue As New DeferredDisposalQueue(Of Resource)()
        Dim resources(399) As Resource
        For i As Integer = 0 To resources.Length - 1
            resources(i) = New Resource()
        Next
        Dim threads(3) As Thread
        For producer As Integer = 0 To 3
            Dim offset As Integer = producer * 100
            threads(producer) = New Thread(Sub()
                                              For i As Integer = offset To offset + 99
                                                  queue.Enqueue(resources(i))
                                              Next
                                          End Sub)
            threads(producer).Start()
        Next
        For Each thread As Thread In threads
            Assert.True(thread.Join(5000))
        Next
        queue.Drain(Nothing)
        For Each value As Resource In resources
            Assert.Equal(1, value.DisposeCount)
        Next
    End Sub

    Private NotInheritable Class Resource
        Implements IDisposable

        Friend DisposeCount As Integer

        Public Sub Dispose() Implements IDisposable.Dispose
            Me.DisposeCount += 1
        End Sub

        Public Overrides Function Equals(obj As Object) As Boolean
            Return TypeOf obj Is Resource
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return 0
        End Function
    End Class
End Class
