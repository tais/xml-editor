Imports System.Data

Public Class LookupFile
    Private Shared Sub AddLookupData(ByVal t As DataTable, ByVal id As Long, ByVal name As String)
        Dim r As DataRow = t.NewRow
        r("id") = id
        r("name") = name
        t.Rows.Add(r)
    End Sub

    Public Shared Sub AddLookupData(ByVal fileName As String, ByVal lookupTable As DataTable)
        Try
            Using xr As New Xml.XmlTextReader(fileName)
                Dim curNode As String = ""
                Dim curValue As String

                Dim name As String = Nothing
                Dim id As Long = Long.MinValue
                Dim nameSet As Boolean = False
                Dim idSet As Boolean = False

                While xr.Read
                    If xr.NodeType = Xml.XmlNodeType.Element Then
                        curNode = xr.Name
                    ElseIf xr.NodeType = Xml.XmlNodeType.Text Then
                        curValue = xr.Value
                        Select Case curNode
                            Case "id"
                                id = Long.Parse(curValue)
                                idSet = True
                            Case "name"
                                name = curValue
                                nameSet = True
                        End Select
                    End If

                    ' Both values for an entry have been read (track with flags so that an
                    ' empty/blank name is not silently dropped, unlike the old name <> Nothing test)
                    If nameSet AndAlso idSet Then
                        AddLookupData(lookupTable, id, name)

                        'Reset for the next entry
                        name = Nothing : id = Long.MinValue
                        nameSet = False : idSet = False
                    End If

                End While
            End Using
        Catch ex As Exception
            Throw New DataLoadException(ErrorHandler.DescribeLoadError(ex, fileName), ex)
        End Try
    End Sub

End Class
