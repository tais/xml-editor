Imports System.Data

''' <summary>One dangling-reference problem found by <see cref="DataValidator"/>.</summary>
Public Class ValidationIssue
    Public Property TableName As String
    Public Property FileName As String
    Public Property RecordId As String
    Public Property RecordName As String
    Public Property ColumnName As String
    Public Property Value As String
    Public Property Problem As String
End Class

''' <summary>
''' Read-only data sanity checks for a loaded data set. Reports lookup/reference values that point
''' at rows which don't exist (e.g. an item referencing an AMMOTYPE or ATTACHMENT id that isn't
''' defined) - the kind of dangling reference that loads fine but can break the game. Purely
''' diagnostic: it never modifies any data.
''' </summary>
Public Class DataValidator

    ' Columns tried (in order) to give a friendly name for a flagged record.
    Private Shared ReadOnly NameColumns As String() = {"szLongItemName", "szItemName", "Name", "ShortName", "name"}

    ''' <summary>Scans the data set behind the manager and returns the issues found (empty if none).</summary>
    Public Shared Function Validate(ByVal dm As DataManager) As List(Of ValidationIssue)
        Dim ds As DataSet = dm.Database.DataSet
        Dim issues As New List(Of ValidationIssue)

        For Each t As DataTable In ds.Tables
            For Each c As DataColumn In t.Columns
                Dim lookupName As String = c.GetStringProperty(ColumnProperty.Lookup_Table)
                If String.IsNullOrEmpty(lookupName) OrElse Not ds.Tables.Contains(lookupName) Then Continue For

                Dim valueColumn As String = c.GetStringProperty(ColumnProperty.Lookup_ValueColumn)
                Dim lookupTable As DataTable = ds.Tables(lookupName)
                If String.IsNullOrEmpty(valueColumn) OrElse Not lookupTable.Columns.Contains(valueColumn) Then Continue For
                ' Skip when the lookup table is empty/not loaded, else every value would look invalid.
                If lookupTable.Rows.Count = 0 Then Continue For

                Dim validValues As New HashSet(Of String)
                For Each lr As DataRow In lookupTable.Rows
                    If lr.RowState <> DataRowState.Deleted Then validValues.Add(lr(valueColumn).ToString())
                Next

                For Each r As DataRow In t.Rows
                    If r.RowState = DataRowState.Deleted Then Continue For
                    Dim v As Object = r(c)
                    If IsDBNull(v) Then Continue For

                    Dim vs As String = v.ToString()
                    ' Skip blanks and "none" sentinels (0 / negative) to avoid false positives.
                    Dim num As Double
                    If String.IsNullOrEmpty(vs) Then Continue For
                    If Double.TryParse(vs, num) AndAlso num <= 0 Then Continue For

                    If Not validValues.Contains(vs) Then
                        issues.Add(New ValidationIssue With {
                            .TableName = t.TableName,
                            .FileName = t.GetStringProperty(TableProperty.FileName),
                            .RecordId = RecordId(t, r),
                            .RecordName = RecordName(t, r),
                            .ColumnName = c.ColumnName,
                            .Value = vs,
                            .Problem = "no such " & lookupName
                        })
                    End If
                Next
            Next
        Next
        Return issues
    End Function

    Private Shared Function RecordId(ByVal t As DataTable, ByVal r As DataRow) As String
        If t.PrimaryKey IsNot Nothing AndAlso t.PrimaryKey.Length > 0 Then
            Try
                Return r(t.PrimaryKey(0)).ToString()
            Catch
            End Try
        End If
        Return ""
    End Function

    Private Shared Function RecordName(ByVal t As DataTable, ByVal r As DataRow) As String
        For Each nameCol As String In NameColumns
            If t.Columns.Contains(nameCol) Then Return r(t.Columns(nameCol)).ToString()
        Next
        Return ""
    End Function

End Class
