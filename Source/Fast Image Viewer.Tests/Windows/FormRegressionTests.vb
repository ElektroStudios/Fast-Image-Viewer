Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.Drawing
Imports System.Collections.Concurrent
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports Xunit

Public Class FormRegressionTests
    Private Const Flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic

    <Fact>
    Public Sub RefreshReusesTheExistingWorker()
        RunOnSta(Sub()
                     Using form As New Form1()
                         Try
                             Invoke(form, "StartWorker")
                             Dim first As Thread = ReadField(Of Thread)(form, "_workerThread")
                             Invoke(form, "StartWorker")
                             Assert.Same(first, ReadField(Of Thread)(form, "_workerThread"))
                             Assert.True(first.IsBackground)
                             Assert.Equal(ThreadPriority.BelowNormal, first.Priority)
                         Finally
                             Invoke(form, "StopWorkerAndClearCache")
                             DisposeTimers(form)
                         End Try
                     End Using
                 End Sub)
    End Sub

    <Fact>
    Public Sub ClearingCacheInvalidatesPendingDisplayRequests()
        RunOnSta(Sub()
                     Using form As New Form1()
                         Try
                             Dim before As Integer = ReadField(Of Integer)(form, "_displayGeneration")
                             Invoke(form, "StopWorkerAndClearCache")
                             Assert.NotEqual(before, ReadField(Of Integer)(form, "_displayGeneration"))
                         Finally
                             DisposeTimers(form)
                         End Try
                     End Using
                 End Sub)
    End Sub

    <Fact>
    Public Sub DeferredDisposalKeepsTheDisplayedBitmapAlive()
        RunOnSta(Sub()
                     Using form As New Form1()
                         Dim picture As PictureBox = ReadField(Of PictureBox)(form, "PicBox")
                         Dim queue As DeferredDisposalQueue(Of Bitmap) = ReadField(Of DeferredDisposalQueue(Of Bitmap))(form, "_pendingDisposal")
                         Dim bitmap As New Bitmap(10, 10)
                         Try
                             picture.Image = bitmap
                             queue.Enqueue(bitmap)
                             Invoke(form, "DrainPendingDisposal")
                             Assert.Equal(10, bitmap.Width)
                             picture.Image = Nothing
                             Invoke(form, "DrainPendingDisposal")
                             Dim retired As Bitmap = Nothing
                             Assert.False(queue.TryDequeue(retired))
                             Assert.ThrowsAny(Of Exception)(Function() bitmap.Width)
                         Finally
                             picture.Image = Nothing
                             bitmap.Dispose()
                             DisposeTimers(form)
                         End Try
                     End Using
                 End Sub)
    End Sub

    <Theory>
    <InlineData(0, 0, False, False)>
    <InlineData(1, 0, False, False)>
    <InlineData(3, 0, False, True)>
    <InlineData(3, 1, True, True)>
    <InlineData(3, 2, True, False)>
    Public Sub NavigationButtonBoundaries(count As Integer, index As Integer, previous As Boolean, following As Boolean)
        RunOnSta(Sub()
                     Using form As New Form1()
                         Try
                             Dim files As String() = If(count = 0, Array.Empty(Of String)(), New String(count - 1) {})
                             GetType(Form1).GetField("_imageFiles", Flags).SetValue(form, files)
                             GetType(Form1).GetField("_currentIndex", Flags).SetValue(form, index)
                             Invoke(form, "UpdateNavigationButtons")
                             Assert.Equal(previous, ReadField(Of Button)(form, "BtnPrev").Enabled)
                             Assert.Equal(following, ReadField(Of Button)(form, "BtnNext").Enabled)
                         Finally
                             DisposeTimers(form)
                         End Try
                     End Using
                 End Sub)
    End Sub

    ''' <summary>Reads fields and compiler-generated WithEvents properties.</summary>
    ''' <typeparam name="T">The expected member type.</typeparam>
    ''' <param name="form">The form containing the member.</param>
    ''' <param name="name">The member name.</param>
    ''' <returns>The field or property value.</returns>
    Private Shared Function ReadField(Of T)(form As Form1, name As String) As T
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
        If String.IsNullOrWhiteSpace(name) Then
            Throw New ArgumentException("A member name is required.", NameOf(name))
        End If

        Const memberFlags As BindingFlags = BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic
        Dim declaringType As Type = form.GetType()
        Dim field As FieldInfo = declaringType.GetField(name, memberFlags)
        If field IsNot Nothing Then
            Return DirectCast(field.GetValue(form), T)
        End If

        Dim propertyInfo As PropertyInfo = declaringType.GetProperty(name, memberFlags)
        If propertyInfo IsNot Nothing AndAlso
           propertyInfo.GetGetMethod(nonPublic:=True) IsNot Nothing AndAlso
           propertyInfo.GetIndexParameters().Length = 0 Then
            Return DirectCast(propertyInfo.GetValue(form, Nothing), T)
        End If

        Throw New MissingMemberException(declaringType.FullName, name)
    End Function

    Private Shared Sub Invoke(form As Form1, name As String)
        GetType(Form1).GetMethod(name, Flags).Invoke(form, Nothing)
    End Sub

    Private Shared Sub DisposeTimers(form As Form1)
        ReadField(Of System.Windows.Forms.Timer)(form, "NavTimer")?.Dispose()
        ReadField(Of System.Windows.Forms.Timer)(form, "SlideshowTimer")?.Dispose()
        ReadField(Of AutoResetEvent)(form, "_wakeSignal").Dispose()
        ReadField(Of AboutBox1)(form, "AboutBox").Dispose()
    End Sub

    Private Shared Sub RunOnSta(action As Action)
        Dim failure As Exception = Nothing
        Dim thread As New Thread(Sub()
                                     Dim previousDirectory As String = IO.Directory.GetCurrentDirectory()
                                     Dim testDirectory As String = IO.Path.Combine(IO.Path.GetTempPath(), "FivTests-" & Guid.NewGuid().ToString("N"))
                                     Try
                                         IO.Directory.CreateDirectory(testDirectory)
                                         IO.Directory.SetCurrentDirectory(testDirectory)
                                         action()
                                     Catch ex As Exception
                                         failure = ex
                                     Finally
                                         IO.Directory.SetCurrentDirectory(previousDirectory)
                                         IO.Directory.Delete(testDirectory, True)
                                     End Try
                                 End Sub)
        thread.SetApartmentState(ApartmentState.STA)
        thread.IsBackground = True
        thread.Start()
        If Not thread.Join(TimeSpan.FromSeconds(15)) Then Throw New TimeoutException("The STA test did not finish.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub
End Class
