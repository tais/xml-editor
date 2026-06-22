Imports System.IO

''' <summary>One editable data set discovered from a vfs_config file (a DIRECTORY location that
''' contains a TableData subfolder), labelled with the profile it belongs to.</summary>
Public Class DataProfile
    Public Property Name As String        ' friendly profile name, e.g. "AIMNAS BRAINMOD"
    Public Property FolderPath As String  ' absolute path to the data folder (parent of TableData)
    Public Property Source As String      ' vfs_config file it came from

    Public Overrides Function ToString() As String
        Dim leaf As String = FolderPath
        Try
            leaf = IO.Path.GetFileName(FolderPath.TrimEnd("\"c))
        Catch
        End Try
        Return Name & "   ->   " & leaf
    End Function
End Class

''' <summary>
''' Scans a JA2 1.13 game root for vfs_config*.ini files and returns the editable data sets they
''' define. JA2 1.13 VFS configs list PROFILES, each referencing LOCATIONS; a [LOC_*] with
''' TYPE=DIRECTORY and a PATH whose folder contains a "TableData" subfolder is something this editor
''' can open (e.g. Data-1.13, Data-BRAINMOD). Read-only; tolerant of malformed files.
''' </summary>
Public Class VfsScanner

    Public Shared Function Scan(ByVal gameRoot As String) As List(Of DataProfile)
        Dim results As New List(Of DataProfile)
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Try
            If String.IsNullOrEmpty(gameRoot) OrElse Not Directory.Exists(gameRoot) Then Return results
            For Each cfgPath As String In Directory.GetFiles(gameRoot, "vfs_config*.ini")
                Try
                    ParseConfig(cfgPath, gameRoot, results, seen)
                Catch
                    ' Skip a malformed config; keep scanning the rest.
                End Try
            Next
        Catch
        End Try
        Return results
    End Function

    Private Shared Sub ParseConfig(ByVal cfgPath As String, ByVal gameRoot As String,
                                   ByVal results As List(Of DataProfile), ByVal seen As HashSet(Of String))
        Dim ini As Dictionary(Of String, Dictionary(Of String, String)) = ReadIni(cfgPath)
        Dim cfgName As String = IO.Path.GetFileName(cfgPath)

        ' Preferred: walk PROFILES -> LOCATIONS -> [LOC_*] so each folder is labelled by its profile.
        For Each p As String In SplitCsv(GetVal(ini, "vfs_config", "PROFILES"))
            Dim profSection As String = "PROFILE_" & p
            Dim label As String = StripQuotes(GetVal(ini, profSection, "NAME"))
            If String.IsNullOrEmpty(label) Then label = p
            For Each loc As String In SplitCsv(GetVal(ini, profSection, "LOCATIONS"))
                AddDirectoryLocation(ini, "LOC_" & loc, gameRoot, label, cfgName, results, seen)
            Next
        Next

        ' Lenient fallback: any DIRECTORY location not reached via a profile.
        For Each section As String In ini.Keys
            If section.StartsWith("LOC_", StringComparison.OrdinalIgnoreCase) Then
                AddDirectoryLocation(ini, section, gameRoot, section.Substring(4), cfgName, results, seen)
            End If
        Next
    End Sub

    Private Shared Sub AddDirectoryLocation(ByVal ini As Dictionary(Of String, Dictionary(Of String, String)),
                                            ByVal locSection As String, ByVal gameRoot As String, ByVal label As String,
                                            ByVal cfgName As String, ByVal results As List(Of DataProfile),
                                            ByVal seen As HashSet(Of String))
        If Not String.Equals(GetVal(ini, locSection, "TYPE"), "DIRECTORY", StringComparison.OrdinalIgnoreCase) Then Return
        Dim relPath As String = StripQuotes(GetVal(ini, locSection, "PATH"))
        If String.IsNullOrEmpty(relPath) Then Return

        Dim full As String
        Try
            full = IO.Path.GetFullPath(IO.Path.Combine(gameRoot, relPath))
        Catch
            Return
        End Try
        ' Only data folders the editor can actually open (they hold the XML under TableData).
        If Not Directory.Exists(IO.Path.Combine(full, "TableData")) Then Return
        If seen.Contains(full) Then Return
        seen.Add(full)
        results.Add(New DataProfile With {.Name = label, .FolderPath = full, .Source = cfgName})
    End Sub

    ' --- minimal INI reader: section -> (key -> value), case-insensitive ---
    Private Shared Function ReadIni(ByVal path As String) As Dictionary(Of String, Dictionary(Of String, String))
        Dim ini As New Dictionary(Of String, Dictionary(Of String, String))(StringComparer.OrdinalIgnoreCase)
        Dim cur As Dictionary(Of String, String) = Nothing
        For Each raw As String In File.ReadAllLines(path)
            Dim line As String = raw.Trim()
            If line = "" OrElse line.StartsWith(";") OrElse line.StartsWith("#") Then Continue For
            If line.StartsWith("[") AndAlso line.EndsWith("]") Then
                Dim name As String = line.Substring(1, line.Length - 2).Trim()
                If Not ini.ContainsKey(name) Then ini(name) = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                cur = ini(name)
            ElseIf cur IsNot Nothing AndAlso line.Contains("=") Then
                Dim idx As Integer = line.IndexOf("="c)
                cur(line.Substring(0, idx).Trim()) = line.Substring(idx + 1).Trim()
            End If
        Next
        Return ini
    End Function

    Private Shared Function GetVal(ByVal ini As Dictionary(Of String, Dictionary(Of String, String)),
                                   ByVal section As String, ByVal key As String) As String
        Dim s As Dictionary(Of String, String) = Nothing
        If ini.TryGetValue(section, s) Then
            Dim v As String = Nothing
            If s.TryGetValue(key, v) Then Return v
        End If
        Return ""
    End Function

    Private Shared Function SplitCsv(ByVal s As String) As List(Of String)
        Dim result As New List(Of String)
        If String.IsNullOrEmpty(s) Then Return result
        For Each part As String In s.Split(","c)
            Dim t As String = part.Trim()
            If t <> "" Then result.Add(t)
        Next
        Return result
    End Function

    Private Shared Function StripQuotes(ByVal s As String) As String
        If s Is Nothing Then Return ""
        s = s.Trim()
        If s.Length >= 2 AndAlso s.StartsWith("""") AndAlso s.EndsWith("""") Then s = s.Substring(1, s.Length - 2)
        Return s
    End Function

End Class
