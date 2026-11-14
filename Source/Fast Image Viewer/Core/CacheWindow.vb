Option Strict On
Option Explicit On
Option Infer Off

Imports System.Collections.Generic

''' <summary>Calculates the asymmetric cache window and center-first load order.</summary>
Friend Structure CacheWindow
    Friend ReadOnly First As Integer
    Friend ReadOnly Last As Integer
    Friend ReadOnly Center As Integer

    ''' <summary>Creates a bounded window for a nonempty file list.</summary>
    Friend Sub New(count As Integer, center As Integer, backward As Integer, forward As Integer)
        If count <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(count))
        If center < 0 OrElse center >= count Then Throw New ArgumentOutOfRangeException(NameOf(center))
        If backward < 0 Then Throw New ArgumentOutOfRangeException(NameOf(backward))
        If forward < 0 Then Throw New ArgumentOutOfRangeException(NameOf(forward))
        Me.Center = center
        Me.First = CInt(Math.Max(0L, CLng(center) - backward))
        Me.Last = CInt(Math.Min(CLng(count) - 1, CLng(center) + forward))
    End Sub

    ''' <summary>Returns center, next, previous, and then increasing distances.</summary>
    Friend Function CreateLoadOrder() As List(Of Integer)
        Dim result As New List(Of Integer) From {Me.Center}
        Dim radius As Integer = 1
        Do While radius <= Me.Center - Me.First OrElse radius <= Me.Last - Me.Center
            If radius <= Me.Last - Me.Center Then result.Add(Me.Center + radius)
            If radius <= Me.Center - Me.First Then result.Add(Me.Center - radius)
            radius += 1
        Loop
        Return result
    End Function
End Structure
