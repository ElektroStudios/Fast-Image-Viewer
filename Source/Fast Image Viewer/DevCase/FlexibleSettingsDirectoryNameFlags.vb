' ***********************************************************************
' Author   : ElektroStudios
' Modified : 09-February-2026
' ***********************************************************************

#Region " Option Statements "

Option Strict On
Option Explicit On
Option Infer Off

#End Region

#Region " FlexibleSettingsDirectoryNameFlags "

' ReSharper disable once CheckNamespace

Namespace DevCase.Core.Application.UserSettings

    ''' <summary>
    ''' Specifies flags that allows to automatically append extra information to the 
    ''' settings storage folder name specified by <see cref="FlexibleSettingsProvider.DirectoryName"/> property.
    ''' </summary>
    <Flags>
    Public Enum FlexibleSettingsDirectoryNameFlags

        ''' <summary>
        ''' No additional information is appended to the directory name.
        ''' </summary>
        None = 0

        ''' <summary>
        ''' Appends the current application name to the directory name.
        ''' </summary>
        ApplicationName = 1 << 0

        ''' <summary>
        ''' Appends the current assembly name to the directory name.
        ''' </summary>
        AssemblyName = 1 << 1

        ''' <summary>
        ''' Appends the current application version to the directory name.
        ''' </summary>
        Version = 1 << 2

        ''' <summary>
        ''' Appends a deterministic hash to the directory name.
        ''' </summary>
        Hash = 1 << 3

        ''' <summary>
        ''' Appends the current user name to the directory name.
        ''' </summary>
        UserName = 1 << 4

    End Enum

End Namespace

#End Region
