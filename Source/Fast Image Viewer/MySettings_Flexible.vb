
#Region " Option Statements "

Option Strict On
Option Explicit On
Option Infer Off

#End Region

#Region " Imports "

Imports System.Diagnostics

Imports DevCase.Core.Application.UserSettings

#End Region

#Region " MySettings "

Namespace My

    <Global.System.Configuration.SettingsProvider(GetType(FlexibleSettingsProvider))>
    Partial Friend NotInheritable Class MySettings

        Public Sub New()
            FlexibleSettingsProvider.BaseDirectoryPath = ".\"
            FlexibleSettingsProvider.DirectoryName = Nothing
            FlexibleSettingsProvider.DirectoryNameFlags = FlexibleSettingsDirectoryNameFlags.None
            FlexibleSettingsProvider.FileName = "user.config"

            Debug.WriteLine($"Effective config file path: {FlexibleSettingsProvider.EffectiveConfigFilePath}")
        End Sub

    End Class

End Namespace

#End Region
