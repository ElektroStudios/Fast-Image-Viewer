Option Strict On
Option Explicit On
Option Infer Off

Imports System.Collections.Concurrent
Imports System.Collections.Generic

''' <summary>Transfers evicted resources to their owning thread for deferred disposal.</summary>
Friend NotInheritable Class DeferredDisposalQueue(Of T As {Class, IDisposable})
    ''' <summary>Stores resources waiting for disposal.</summary>
    Private ReadOnly items As New ConcurrentQueue(Of T)()

    ''' <summary>Queues a resource from any producer thread.</summary>
    Friend Sub Enqueue(item As T)
        Me.items.Enqueue(item)
    End Sub

    ''' <summary>Removes a resource for explicit shutdown cleanup.</summary>
    Friend Function TryDequeue(ByRef item As T) As Boolean
        Return Me.items.TryDequeue(item)
    End Function

    ''' <summary>Disposes retired resources, retaining the currently displayed instance by identity.</summary>
    ''' <remarks>Call on the resource-owning UI thread. Producers must enqueue each retired resource once.</remarks>
    Friend Sub Drain(displayed As T)
        Dim retained As New List(Of T)()
        Dim item As T = Nothing
        Do While Me.items.TryDequeue(item)
            If item Is Nothing Then Continue Do
            If Object.ReferenceEquals(item, displayed) Then
                retained.Add(item)
            Else
                item.Dispose()
            End If
        Loop
        For Each resource As T In retained
            Me.items.Enqueue(resource)
        Next
    End Sub
End Class
