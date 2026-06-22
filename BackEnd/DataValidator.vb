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

    ' Internal/computed tables the editor builds at runtime - references to them aren't simple ids.
    Private Shared ReadOnly InternalLookupTables As String() = {"ITEMTOEXPLOSIVE"}

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
                ' Skip empty/not-loaded lookups and internal/computed tables - everything would look invalid.
                If lookupTable.Rows.Count = 0 Then Continue For
                If Array.IndexOf(InternalLookupTables, lookupName.ToUpperInvariant()) >= 0 Then Continue For

                ' Collect valid values, and detect a bit-flag lookup: all non-zero values are powers of
                ' two (with a real flag bit >= 4). Such columns store a bitwise COMBINATION (e.g.
                ' DrugType 5 = Adrenaline|Regeneration), so they're checked bitwise, not as a single id.
                Dim validValues As New HashSet(Of String)
                Dim allBits As ULong = 0
                Dim maxVal As ULong = 0
                Dim isBitFlag As Boolean = True
                For Each lr As DataRow In lookupTable.Rows
                    If lr.RowState = DataRowState.Deleted Then Continue For
                    Dim lvs As String = lr(valueColumn).ToString()
                    validValues.Add(lvs)
                    Dim n As ULong
                    If ULong.TryParse(lvs, n) Then
                        allBits = allBits Or n
                        If n > maxVal Then maxVal = n
                        If n <> 0UL AndAlso (n And (n - 1UL)) <> 0UL Then isBitFlag = False
                    Else
                        isBitFlag = False
                    End If
                Next
                If maxVal < 4UL Then isBitFlag = False   ' too small to tell flags from ids apart

                For Each r As DataRow In t.Rows
                    If r.RowState = DataRowState.Deleted Then Continue For
                    Dim v As Object = r(c)
                    If IsDBNull(v) Then Continue For

                    Dim vs As String = v.ToString()
                    ' Skip blanks and "none" sentinels (0 / negative) to avoid false positives.
                    Dim num As Double
                    If String.IsNullOrEmpty(vs) Then Continue For
                    If Double.TryParse(vs, num) AndAlso num <= 0 Then Continue For

                    Dim bad As Boolean
                    If isBitFlag Then
                        ' Valid if every set bit corresponds to a known flag.
                        Dim val As ULong
                        bad = ULong.TryParse(vs, val) AndAlso (val And Not allBits) <> 0UL
                    Else
                        bad = Not validValues.Contains(vs)
                    End If

                    If bad Then
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
