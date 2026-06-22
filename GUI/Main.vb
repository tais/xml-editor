Imports System.Globalization
Imports GUI.GUI
Imports System.Threading

Module Main
    'this file creates the xsd from scratch
    'we don't want a typed-dataset, because it's less flexible
    'once we have a flexible way to do the GUI, this format should pay off
    Private Const SchemaName As String = "JA2Data"

    Public GameData(9) As DataManager
    Public GameDataCount As Integer
    Public MainWindow As MainForm
    Public Splash As SplashForm

    Public Sub Main()
        If CheckForDuplicateProcess(My.Application.Info.AssemblyName) Then End

        Try
            AddHandler ErrorHandler.FatalError, AddressOf ExitDueToError

            ' Catch otherwise-unhandled errors from UI event handlers (raised during the message
            ' loop, after Application.Run) and from background threads, so an editing slip shows a
            ' message and keeps the app alive instead of crashing to desktop.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)
            AddHandler Application.ThreadException, AddressOf OnThreadException
            AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnUnhandledException

            ' RoWa21: Changed the thread of the application.
            ' This is used, so that the DECIMAL numeric up down control for the "ShotsPer4Turns"
            ' always displays a "." instead of a "," for the decimal delimiter.
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US")
            Thread.CurrentThread.CurrentUICulture = New CultureInfo("en-US")

            ' Read the init file for defaults; a missing/invalid file is no longer fatal because the
            ' startup folder picker (below) can supply the data folder.
            Try
                IniFile.ReadFile("XMLEditorInit.xml")
            Catch ex As DataLoadException
            End Try

            ' Always ask which data folder to open. Prefer a list built from the vfs_config*.ini files
            ' in the editor's own folder (the JA2 game root); if none are found, fall back to a plain
            ' folder browser. The chosen folder replaces the configured directories and is remembered.
            Dim pickedDir As String
            Dim profiles As List(Of DataProfile) = VfsScanner.Scan(Application.StartupPath)
            If profiles.Count = 0 Then
                Dim cwd As String = System.IO.Directory.GetCurrentDirectory()
                If Not String.Equals(cwd, Application.StartupPath, StringComparison.OrdinalIgnoreCase) Then profiles = VfsScanner.Scan(cwd)
            End If
            If profiles.Count > 0 Then
                pickedDir = ChooseDataProfile(profiles)
            Else
                pickedDir = ChooseDataDirectory()
            End If
            If Not String.IsNullOrEmpty(pickedDir) Then
                IniFile.SetSingleDataDirectory(pickedDir)
                IniFile.SaveDataDirectory("XMLEditorInit.xml", pickedDir)
            End If

            Dim useWorkingDir As Boolean = IniFile.UseWorkingDirectory

            If My.Application.Info.Version.Major <> My.Settings.Last_Version_Major OrElse
                My.Application.Info.Version.Minor <> My.Settings.Last_Version_Minor Then
                useWorkingDir = False
            End If

            Splash = New SplashForm()
            Splash.Show()

            Splash.UpdateLoadingText(DisplayText.Initializing)

            For i As Integer = 0 To GameData.Length - 1
                If Not String.IsNullOrEmpty(IniFile.DataDirectory(i)) Then
                    Splash.UpdateCurrentDirectory(IniFile.DataDirectory(i))

                    GameData(i) = New DataManager(SchemaName, IniFile.DataDirectory(i), IniFile.LanguageSpecific_Russian_GameDirPath(i), IniFile.LanguageSpecific_Polish_GameDirPath(i), _
                                    IniFile.LanguageSpecific_German_GameDirPath(i), IniFile.LanguageSpecific_French_GameDirPath(i), _
                                    IniFile.LanguageSpecific_Italian_GameDirPath(i), IniFile.LanguageSpecific_Chinese_GameDirPath(i), _
                                    IniFile.LanguageSpecific_Dutch_GameDirPath(i), IniFile.LanguageSpecific_Taiwanese_GameDirPath(i), _
                                    IniFile.Hide_Nothing_Items(), useWorkingDir)
                    AddHandler GameData(i).Database.AfterLoadAll, AddressOf DB_AfterLoadAll
                    AddHandler GameData(i).Database.LoadingTable, AddressOf DB_LoadingTable

                    GameDataCount += 1

                    Splash.UpdateLoadingText(DisplayText.CreatingDatabase)
                    GameData(i).CreateDatabase()

                    Splash.UpdateLoadingText(DisplayText.LoadingImages)
                    GameData(i).LoadImages()

                    Splash.UpdateLoadingText(DisplayText.LoadingXmlFiles)
                    GameData(i).LoadData()

                    Splash.UpdateLoadingText(DisplayText.SearchingForOtherDirectories)
                End If
            Next

            If GameDataCount = 0 Then
                Splash.Hide()
                Throw New DataLoadException("No data folder was selected, so there is nothing to edit. Restart the editor and choose your JA2 1.13 data folder (the one containing the XML data, e.g. Data-1.13).")
            End If

            Splash.UpdateLoadingText(DisplayText.LoadingSettings)
            SettingsUtility.LoadSettings()

            Splash.UpdateLoadingText(DisplayText.ApplicationStarting)
            Splash.Hide()

            MainWindow = New MainForm
            Application.Run(MainWindow)

            SettingsUtility.SaveSettings()
        Catch ex As DataLoadException
            ' A data/config problem whose message is already written for the user - show it
            ' plainly (with the file name / location) instead of the generic "report this bug"
            ' dialog, since it is almost always something the user can fix in their own files.
            ErrorHandler.ShowError(ex.Message, "Unable to Load Data", MessageBoxIcon.Exclamation)
        Catch ex As Exception
            ErrorHandler.ShowError(DisplayText.UnhandledError, ex)
        End Try
    End Sub

    ''' <summary>Modal list picker over the data sets found in the vfs_config files. Returns the
    ''' chosen folder path, a browsed path (via the Browse button), or Nothing if cancelled. Any
    ''' failure falls back to the plain folder browser.</summary>
    Private Function ChooseDataProfile(ByVal profiles As List(Of DataProfile)) As String
        Dim browsed As String = Nothing
        Try
            Using frm As New Form()
                frm.Text = "Choose the JA2 1.13 data set to edit"
                frm.ClientSize = New System.Drawing.Size(560, 380)
                frm.StartPosition = FormStartPosition.CenterScreen
                frm.FormBorderStyle = FormBorderStyle.FixedDialog
                frm.MinimizeBox = False
                frm.MaximizeBox = False

                Dim list As New ListBox With {.Dock = DockStyle.Fill, .IntegralHeight = False}
                For Each p As DataProfile In profiles
                    list.Items.Add(p)
                Next

                ' Pre-select the last-used folder if it is in the list.
                Dim last As String = ""
                Try
                    If Not String.IsNullOrEmpty(IniFile.DataDirectory(0)) Then last = System.IO.Path.GetFullPath(IniFile.DataDirectory(0)).TrimEnd("\"c)
                Catch
                End Try
                For i As Integer = 0 To profiles.Count - 1
                    If String.Equals(profiles(i).FolderPath.TrimEnd("\"c), last, StringComparison.OrdinalIgnoreCase) Then
                        list.SelectedIndex = i
                        Exit For
                    End If
                Next
                If list.SelectedIndex < 0 AndAlso list.Items.Count > 0 Then list.SelectedIndex = 0

                Dim lbl As New Label With {
                    .Text = "Data sets found from your vfs_config files. Pick one, or Browse for another folder:",
                    .Dock = DockStyle.Top, .Height = 38, .Padding = New Padding(10, 10, 10, 0)}

                Dim panel As New Panel With {.Dock = DockStyle.Bottom, .Height = 48}
                Dim okBtn As New Button With {.Text = "Open", .DialogResult = DialogResult.OK, .Width = 90, .Top = 9, .Left = 560 - 3 * 96 - 8}
                Dim browseBtn As New Button With {.Text = "Browse...", .Width = 90, .Top = 9, .Left = 560 - 2 * 96 - 8}
                Dim cancelBtn As New Button With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .Width = 90, .Top = 9, .Left = 560 - 96 - 8}
                AddHandler browseBtn.Click, Sub()
                                                Dim b As String = ChooseDataDirectory()
                                                If Not String.IsNullOrEmpty(b) Then
                                                    browsed = b
                                                    frm.DialogResult = DialogResult.OK
                                                    frm.Close()
                                                End If
                                            End Sub
                AddHandler list.DoubleClick, Sub() okBtn.PerformClick()
                panel.Controls.Add(okBtn)
                panel.Controls.Add(browseBtn)
                panel.Controls.Add(cancelBtn)

                frm.Controls.Add(list)
                frm.Controls.Add(lbl)
                frm.Controls.Add(panel)
                frm.AcceptButton = okBtn
                frm.CancelButton = cancelBtn

                If frm.ShowDialog() = DialogResult.OK Then
                    If Not String.IsNullOrEmpty(browsed) Then Return browsed
                    Dim sel As DataProfile = TryCast(list.SelectedItem, DataProfile)
                    If sel IsNot Nothing Then Return sel.FolderPath
                End If
            End Using
            Return Nothing
        Catch
            ' On any failure, fall back to the plain folder browser.
            Return ChooseDataDirectory()
        End Try
    End Function

    ''' <summary>Shows a folder picker for the data directory, pre-selected to the current/last one.
    ''' Returns the chosen path, or Nothing if cancelled / on any error (the caller then falls back
    ''' to whatever the init file configured).</summary>
    Private Function ChooseDataDirectory() As String
        Try
            Using dlg As New FolderBrowserDialog()
                dlg.Description = "Select your JA2 1.13 data folder (the folder that contains the XML data, e.g. ...\Data-1.13)."
                dlg.ShowNewFolderButton = False
                Dim current As String = IniFile.DataDirectory(0)
                If Not String.IsNullOrEmpty(current) Then
                    Try
                        Dim abs As String = System.IO.Path.GetFullPath(current)
                        If System.IO.Directory.Exists(abs) Then dlg.SelectedPath = abs
                    Catch
                    End Try
                End If
                If dlg.ShowDialog() = DialogResult.OK AndAlso Not String.IsNullOrEmpty(dlg.SelectedPath) Then
                    Return dlg.SelectedPath
                End If
            End Using
        Catch
            ' Picker unavailable / failed - fall back to the init-file configuration.
        End Try
        Return Nothing
    End Function

    Private Sub DB_AfterLoadAll(sender As XmlDB)
        Splash.UpdateLoadingText(String.Format(DisplayText.BuildingItemTable, sender.DataManager.Name))
    End Sub

    Private Sub DB_LoadingTable(sender As XmlDB, ByVal fileName As String)
        If fileName.Contains("\") Then fileName = fileName.Remove(0, fileName.LastIndexOf("\") + 1)
        Splash.UpdateLoadingText(String.Format(DisplayText.LoadingTables, sender.DataManager.Name, fileName))
    End Sub

    Private Sub ExitDueToError()
        End
    End Sub

    Private Sub OnThreadException(sender As Object, e As System.Threading.ThreadExceptionEventArgs)
        ErrorHandler.ShowError(DisplayText.UnhandledError, e.Exception)
    End Sub

    Private Sub OnUnhandledException(sender As Object, e As UnhandledExceptionEventArgs)
        Dim ex As Exception = TryCast(e.ExceptionObject, Exception)
        If ex IsNot Nothing Then ErrorHandler.ShowError(DisplayText.UnhandledError, ex)
    End Sub


    'Determines if there already is a 'processName' running in the local host.
    'Returns true if it finds more than 'one processName' running
    Private Function CheckForDuplicateProcess(ByVal processName As String) As Boolean
        'function returns true if it finds more than one 'processName' running
        Dim Procs() As Process
        Dim proc As Process
        'get ALL processes running on this machine in all desktops
        'this also finds all services running as well.
        Procs = Process.GetProcesses()
        Dim count As Integer = 0
        For Each proc In Procs
            If proc.ProcessName.ToString.Equals(processName) Then
                count += 1
            End If
        Next proc
        If count > 1 Then
            Return True
        Else
            Return False
        End If
    End Function
End Module
