Imports GUI.My
Imports Microsoft.VisualBasic.CompilerServices
Imports System
Imports System.Configuration
Imports System.Data
Imports System.IO
Imports System.Runtime.CompilerServices
Namespace GUI

    <StandardModule()> _
    Friend NotInheritable Class SettingsUtility
        Private Shared _settingsTable As DataTable
        Private Const NameCol As String = "Name"
        Private Const SettingsFileName As String = "XMLEditorSettings.xml"
        Private Const ValueCol As String = "Value"

        <Obsolete()> _
        Public Shared Function GetPropertyValue(ByVal name As String) As Object
            Dim value2 As SettingsPropertyValue = MySettingsProperty.Settings.PropertyValues.Item(name)
            If value2 IsNot Nothing Then
                Return value2.PropertyValue()
            End If
            Return Nothing
        End Function

        Public Shared Function GetSettingsValue(ByVal name As String, ByVal defaultValue As Object) As Object
            Dim row As DataRow = _settingsTable.Rows.Find(name)
            If row Is Nothing Then
                Return defaultValue
            End If
            Return row("Value")
        End Function

        Public Shared Sub LoadSettings()
            _settingsTable = MakeSettingsTable()
            Try
                If File.Exists(SettingsFileName) Then _settingsTable.ReadXml(SettingsFileName)
            Catch ex As Exception
                ' Settings are non-critical UI state - a missing OR corrupt file just means
                ' defaults. Don't let it abort startup (previously only FileNotFound was caught,
                ' so a malformed XMLEditorSettings.xml crashed the editor on launch).
                ErrorHandler.LogWarning("Could not read " & SettingsFileName & ": " & ex.Message)
            End Try
        End Sub

        Private Shared Function MakeSettingsTable() As DataTable
            Dim table As New DataTable()
            table.TableName = "Settings"
            table.Columns.Add("Name", GetType(String))
            table.Columns.Add("Value", GetType(String))
            table.PrimaryKey = New DataColumn() {table.Columns("Name")}
            Return table
        End Function

        Public Shared Sub SaveSettings()
            Try
                _settingsTable.WriteXml(SettingsFileName)
            Catch ex As Exception
                ' Never let a settings-save failure (e.g. read-only dir) crash the app on exit.
                ErrorHandler.LogWarning("Could not save " & SettingsFileName & ": " & ex.Message)
            End Try
        End Sub

        <Obsolete()> _
        Public Shared Sub SetPropertyValue(ByVal name As String, ByVal type As Type, ByVal value As Object)
            Dim settings As MySettings = MySettingsProperty.Settings
            Dim value2 As SettingsPropertyValue = settings.PropertyValues().Item(name)
            If value2 Is Nothing Then
                Dim prop As New SettingsProperty(name)
                prop.PropertyType = type
                prop.Provider = (MySettingsProperty.Settings.Providers().Item("LocalFileSettingsProvider"))
                prop.IsReadOnly = False
                prop.SerializeAs = 0
                Dim attribute As New UserScopedSettingAttribute()
                prop.Attributes().Add(attribute.GetType(), attribute)
                settings.Properties().Add(prop)
                value2 = New SettingsPropertyValue(prop)
                settings.PropertyValues().Add(value2)
            End If
            value2.PropertyValue = RuntimeHelpers.GetObjectValue(value)
            settings = Nothing
        End Sub

        Public Shared Sub SetSettingsValue(ByVal name As String, ByVal value As String)
            Dim row As DataRow = _settingsTable.Rows.Find(name)
            If row Is Nothing Then
                row = _settingsTable.NewRow()
                row("Name") = name
                row("Value") = value
                _settingsTable.Rows.Add(row)
            Else
                row("Value") = value
            End If
        End Sub
    End Class
End Namespace
