' ***********************************************************************
' Author   : ElektroStudios
' Modified : 25-April-2024
' ***********************************************************************

#Region " Option Statements "

Option Strict On
Option Explicit On
Option Infer Off

#End Region

#Region " Imports "

Imports System.IO

Imports System.Collections.Generic

Imports System.Diagnostics
Imports System.Security
Imports System.Security.AccessControl
Imports System.Security.Principal

#End Region

#Region " UtilDirectory "

' ReSharper disable once CheckNamespace

Namespace DevCase.Core.IO.FileSystem

    ''' <summary>
    ''' Contains directory related utilities.
    ''' </summary>
    Public NotInheritable Class UtilDirectory

#Region " Constructors "

        ''' <summary>
        ''' Prevents a default instance of the <see cref="UtilDirectory"/> class from being created.
        ''' </summary>
        <DebuggerNonUserCode>
        Private Sub New()
        End Sub

#End Region

#Region " Public Methods "

        ''' <summary>
        ''' Determines whether the current user have read and write permissions on the specified directory.
        ''' </summary>
        ''' 
        ''' <param name="directoryPath">
        ''' The full path of the directory to evaluate.
        ''' </param>
        ''' 
        ''' <returns>
        ''' <see langword="True"/> if the current user or any of its groups has the required
        ''' read and write access rights on the directory; otherwise, <see langword="False"/>.
        ''' </returns>
        <DebuggerStepThrough>
        Public Shared Function HasReadAndWriteAccess(directoryPath As String) As Boolean

            If String.IsNullOrWhiteSpace(directoryPath) Then
                Throw New ArgumentNullException(NameOf(directoryPath))
            End If

            If Not Directory.Exists(directoryPath) Then
                Throw New DirectoryNotFoundException($"Directory not found: {directoryPath}")
            End If

            Try
                Dim directoryInfo As New DirectoryInfo(directoryPath)
                Dim acl As DirectorySecurity = directoryInfo.GetAccessControl()
                Dim rules As AuthorizationRuleCollection =
                    acl.GetAccessRules(includeExplicit:=True, includeInherited:=True, targetType:=GetType(SecurityIdentifier))

                Dim identity As WindowsIdentity = WindowsIdentity.GetCurrent()
                If identity Is Nothing Then
                    Return False
                End If

                ' Collect SIDs for current user and groups.
                Dim sids As New HashSet(Of SecurityIdentifier)()
                If identity.User IsNot Nothing Then
                    sids.Add(identity.User)
                End If
                For Each grp As IdentityReference In identity.Groups
                    Dim sid As SecurityIdentifier = TryCast(grp, SecurityIdentifier)
                    If sid IsNot Nothing Then
                        sids.Add(sid)
                    End If
                Next

                ' Define the specific bits we require for read and write.
                ' Note: We intentionally DO NOT include Delete/DeleteSubdirectoriesAndFiles here,
                ' because a deny on Delete should not block basic read/write operations.
                Dim requiredRead As FileSystemRights = FileSystemRights.ReadData Or FileSystemRights.ListDirectory Or FileSystemRights.Read
                Dim requiredWrite As FileSystemRights = FileSystemRights.WriteData Or FileSystemRights.AppendData Or FileSystemRights.Write

                ' Accumulate allow and deny masks for relevant SIDs.
                Dim accumulatedAllow As FileSystemRights = 0
                Dim accumulatedDeny As FileSystemRights = 0

                For Each ruleObj As AuthorizationRule In rules
                    Dim rule As FileSystemAccessRule = TryCast(ruleObj, FileSystemAccessRule)
                    If rule Is Nothing Then
                        Continue For
                    End If

                    Dim sid As SecurityIdentifier = TryCast(rule.IdentityReference, SecurityIdentifier)
                    If sid Is Nothing OrElse Not sids.Contains(sid) Then
                        Continue For
                    End If

                    Dim rights As FileSystemRights = rule.FileSystemRights

                    If rule.AccessControlType = AccessControlType.Deny Then
                        accumulatedDeny = accumulatedDeny Or rights

                    ElseIf rule.AccessControlType = AccessControlType.Allow Then
                        accumulatedAllow = accumulatedAllow Or rights

                    End If
                Next

                ' If any required read/write bit is explicitly denied, cannot read/write.
                If (accumulatedDeny And (requiredRead Or requiredWrite)) <> 0 Then
                    Return False
                End If

                ' Check that all required read bits are allowed.
                If (accumulatedAllow And requiredRead) <> requiredRead Then
                    Return False
                End If

                ' Check that all required write bits are allowed.
                Return (accumulatedAllow And requiredWrite) = requiredWrite

            Catch ex As UnauthorizedAccessException
                ' Explicitly cannot access the directory.
                Return False

            Catch ex As SecurityException
                ' Security policy prevents access.
                Return False

            Catch ex As Exception
                ' Unexpected error.
                Return False

            End Try
        End Function

#End Region

    End Class

End Namespace

#End Region
