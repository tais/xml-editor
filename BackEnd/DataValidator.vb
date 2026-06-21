Imports System.Data
Imports System.Text

''' <summary>
''' Read-only data sanity checks for a loaded data set. Reports lookup/reference values that
''' point at rows which don't exist (e.g. an item referencing an AMMOTYPE or ATTACHMENT id that
''' isn't defined) - the kind of dangling reference that loads fine but can break the game.
''' Purely diagnostic: it never modifies any data.
''' </summary>
Public Class DataValidator

    ' Columns whose names are tried (in order) to give a friendlier label for a flagged row.
    Private Shared ReadOnly NameColumns As String() = {"szLongItemName", "szItemName", "Name", "ShortName", "name"}

    ''' <summary>Validates the data set behind the given manager and returns a human-readable report.</summary>
    Public Shared Function Validate(ByVal dm As DataManager) As String
        Dim ds As DataSet = dm.Database.DataSet
        Dim report As New StringBuilder()
        Dim issueCount As Integer = 0

        report.AppendLine("Validation report - " & dm.Name)
        report.AppendLine(New String("="c, 60))
        report.AppendLine()

        For Each t As DataTable In ds.Tables
            Dim tableIssues As New List(Of String)

            For Each c As DataColumn In t.Columns
                Dim lookupName As String = c.GetStringProperty(ColumnProperty.Lookup_Table)
                If String.IsNullOrEmpty(lookupName) OrElse Not ds.Tables.Contains(lookupName) Then Continue For

                Dim valueColumn As String = c.GetStringProperty(ColumnProperty.Lookup_ValueColumn)
                Dim lookupTable As DataTable = ds.Tables(lookupName)
                If String.IsNullOrEmpty(valueColumn) OrElse Not lookupTable.Columns.Contains(valueColumn) Then Continue For
                ' Skip when the lookup table is empty/not loaded - otherwise every value would be
                ' flagged as "no such", flooding the report with false positives.
                If lookupTable.Rows.Count = 0 Then Continue For

                ' A column can be a self-reference (e.g. default attachments point back at ITEM);
                ' that is fine and is handled the same way - just compare against the lookup's values.
                Dim validValues As New HashSet(Of String)
                For Each lr As DataRow In lookupTable.Rows
                    If lr.RowState <> DataRowState.Deleted Then validValues.Add(lr(valueColumn).ToString())
                Next

                For Each r As DataRow In t.Rows
                    If r.RowState = DataRowState.Deleted Then Continue For
                    Dim v As Object = r(c)
                    If IsDBNull(v) Then Continue For

                    Dim vs As String = v.ToString()
                    ' Skip "none" sentinels (0 / negative) and blanks to avoid false positives.
                    Dim num As Double
                    If String.IsNullOrEmpty(vs) Then Continue For
                    If Double.TryParse(vs, num) AndAlso num <= 0 Then Continue For

                    If Not validValues.Contains(vs) Then
                        tableIssues.Add("  row " & RowLabel(t, r) & ": '" & c.ColumnName & "' = " & vs & "  ->  no such " & lookupName)
                        issueCount += 1
                    End If
                Next
            Next

            If tableIssues.Count > 0 Then
                Dim fileName As String = t.GetStringProperty(TableProperty.FileName)
                report.AppendLine(t.TableName & If(String.IsNullOrEmpty(fileName), "", " (" & fileName & ")") & ":")
                For Each issue As String In tableIssues
                    report.AppendLine(issue)
                Next
                report.AppendLine()
            End If
        Next

        report.AppendLine(New String("-"c, 60))
        If issueCount = 0 Then
            report.AppendLine("No reference problems found.")
        Else
            report.AppendLine(issueCount & " issue(s) found.")
        End If
        Return report.ToString()
    End Function

    ''' <summary>Best-effort friendly label for a row: primary-key value plus a name column if present.</summary>
    Private Shared Function RowLabel(ByVal t As DataTable, ByVal r As DataRow) As String
        Dim label As String = ""
        If t.PrimaryKey IsNot Nothing AndAlso t.PrimaryKey.Length > 0 Then
            Try
                label = r(t.PrimaryKey(0)).ToString()
            Catch
            End Try
        End If
        For Each nameCol As String In NameColumns
            If t.Columns.Contains(nameCol) Then
                Dim nm As String = r(t.Columns(nameCol)).ToString()
                If Not String.IsNullOrEmpty(nm) Then label &= " '" & nm & "'"
                Exit For
            End If
        Next
        Return label
    End Function

End Class
