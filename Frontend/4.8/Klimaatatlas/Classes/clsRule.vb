Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Data.SQLite
Imports System.IO
Imports System.Text.RegularExpressions
Imports Klimaatatlas.clsGeneralFunctions
Imports SpatialiteSharp
Public Class clsRule
    Friend Name As String
    Friend Benchmarks As New Dictionary(Of String, clsBenchmark)
    Friend EquationComponents As New List(Of clsEquationComponent)
    Friend Filter As New clsFilter()
    Private Setup As clsKlimaatatlas

    Public Sub New(ByRef mySetup As clsKlimaatatlas)
        Setup = mySetup
    End Sub

    Public Function Execute() As Boolean
        Try
            ' Load the SpatiaLite extension
            If Setup.GpkgCon.State <> ConnectionState.Open Then Setup.GpkgCon.Open()
            Setup.Log.WriteToDiagnosticsFile("Geopackage successfully opened.")

            Setup.GpkgCon.EnableExtensions(True)
            Setup.Log.WriteToDiagnosticsFile("Geopackage extensions enabled.")

            Setup.GpkgCon.LoadExtension("mod_spatialite")
            Setup.Log.WriteToDiagnosticsFile("Geopackage spatialite extension loaded.")

            For Each Scenario As clsScenario In Setup.Scenarios.Values

                Setup.Log.WriteToDiagnosticsFile("Processing scenario " & Scenario.Name)
                Me.Setup.Generalfunctions.UpdateProgressBar($"Processing rule {Name} for scenario {Scenario.Name}...", 0, 10, True)

                Using transaction As SQLiteTransaction = Setup.GpkgCon.BeginTransaction()
                    Setup.Log.WriteToDiagnosticsFile("SQLite transaction started")

                    Dim dt As New DataTable()
                    Dim columnsToUpdate As New List(Of String)    'keep track of the fields that are needed for the results of this rule

                    'establish a list of fields needed for this rule
                    Dim Fields As List(Of String) = getFieldNamesForScenario(Scenario)
                    Setup.Log.WriteToDiagnosticsFile("Field names collected for scenario " & Scenario.Name)

                    'establish a list of constants needed for this rule
                    Dim Constants As List(Of Double) = getConstantsForScenario(Scenario)
                    Setup.Log.WriteToDiagnosticsFile("Constants collected for scenario " & Scenario.Name)


                    'Ensure necessary columns exist before calculations and add them to the list
                    EnsureColumnExists(Setup.GpkgTable, $"{Scenario.Name}_{Name}", "REAL")
                    Setup.Log.WriteToDiagnosticsFile($"Made sure column {Scenario.Name}_{Name} exists")

                    Fields.Add($"{Scenario.Name}_{Name}")
                    columnsToUpdate.Add($"{Scenario.Name}_{Name}")

                    For Each Component As clsEquationComponent In EquationComponents
                        Setup.Log.WriteToDiagnosticsFile("Creating field for component " & Component.ResultsFieldName)
                        EnsureColumnExists(Setup.GpkgTable, $"{Scenario.Name}_{Component.ResultsFieldName}", "REAL")
                        Fields.Add($"{Scenario.Name}_{Component.ResultsFieldName}")
                        columnsToUpdate.Add($"{Scenario.Name}_{Component.ResultsFieldName}")
                        Setup.Log.WriteToDiagnosticsFile("Field for component " & Component.ResultsFieldName & " created.")
                    Next

                    If Filter.filterFieldName = "" Then
                        'no filter applied. Simply select all records from the table
                        ' Execute a query to retrieve all features and necessary fields
                        Using cmdSelect As New SQLiteCommand("SELECT fid, " & String.Join(", ", Fields) & " FROM " & Setup.GpkgTable & ";", Setup.GpkgCon)
                            Setup.Log.WriteToDiagnosticsFile("Executing query " & cmdSelect.CommandText & ")")
                            Using adapter As New SQLiteDataAdapter(cmdSelect)
                                adapter.Fill(dt)
                            End Using
                        End Using
                    Else
                        'a filter is applied. Select only the records that match the filter
                        Dim filterClause As String = ""
                        Select Case Filter.filterOperator.ToUpper()
                            Case "=", "<>", "<", ">", "<=", ">="
                                Using cmdSelect As New SQLiteCommand()
                                    cmdSelect.Connection = Setup.GpkgCon
                                    cmdSelect.CommandText = "SELECT fid, " & String.Join(", ", Fields) &
                                      " FROM " & Setup.GpkgTable &
                                      " WHERE " & Filter.filterFieldName & " " & Filter.filterOperator & " @filterValue;"
                                    cmdSelect.Parameters.AddWithValue("@filterValue", Filter.filterOperand)

                                    Setup.Log.WriteToDiagnosticsFile("Executing filtered query " & cmdSelect.CommandText & " with value " & Filter.filterOperand)
                                    Using adapter As New SQLiteDataAdapter(cmdSelect)
                                        adapter.Fill(dt)
                                    End Using
                                End Using
                            Case Else
                                Throw New Exception("Unsupported filter operator: " & Filter.filterOperator)
                        End Select
                    End If


                    Me.Setup.Generalfunctions.UpdateProgressBar($"Processing rule {Name} for scenario {Scenario.Name}...", 0, 10, True)
                    Dim iRow As Integer = 0, nRow As Integer = dt.Rows.Count

                    Setup.Log.WriteToDiagnosticsFile($"Number of rows to process for rule {Name}: " & dt.Rows.Count)
                    Setup.Log.WriteToDiagnosticsFile($"Number of components to process for rule {Name}: " & EquationComponents.Count)
                    For Each row As DataRow In dt.Rows
                        iRow += 1
                        Me.Setup.Generalfunctions.UpdateProgressBar("", iRow, nRow)
                        Dim fid As Integer = Convert.ToInt32(row("fid"))
                        Dim totalWeight As Double = 0
                        Dim ResultSum As Double = 0

                        'debugging section
                        'Debug.Print("Processing row " & iRow)
                        'If fid = 82741 Then Stop 'Broekvelden Vettebroek

                        For Each Component As clsEquationComponent In EquationComponents

                            ' Get the field name and transformation for this component. If not found, see if a constant value was assigned
                            Dim value As Object = Nothing
                            If Component.Benchmark.FieldNamesPerScenario.ContainsKey(Scenario.Name.Trim.ToUpper) Then
                                Dim FieldName As String = Component.Benchmark.FieldNamesPerScenario.Item(Scenario.Name.Trim.ToUpper)
                                Dim Transformation As String = Component.Benchmark.TransformationPerScenario.Item(Scenario.Name.Trim.ToUpper)
                                value = row(FieldName)
                                If IsDBNull(value) Then value = Component.Benchmark.nullValue
                                If Transformation <> "" Then
                                    If Not Setup.Generalfunctions.EvaluateSecondDegreePolynomeExpression(Transformation, value, value) Then
                                        Throw New Exception($"Error evaluating mathematical expression {Transformation}. Please check if your equation obeys the a * x^2 + b * x + c format convention.")
                                    End If
                                End If
                            ElseIf Component.Benchmark.ConstantsPerScenario.ContainsKey(Scenario.Name.Trim.ToUpper) Then
                                value = Component.Benchmark.ConstantsPerScenario.Item(Scenario.Name.Trim.ToUpper)
                            End If

                            If Not IsDBNull(value) Then
                                totalWeight += Component.Weight
                                Component.Result = Component.Benchmark.getResult(value) * Component.Weight
                                ResultSum += Component.Result
                            End If
                        Next

                        For Each Component As clsEquationComponent In EquationComponents
                            Dim contribution As Double = Component.Result / totalWeight

                            ' Assuming the column name is built using Scenario.Name, Component.ResultsFieldName
                            Dim columnName As String = $"{Scenario.Name}_{Component.ResultsFieldName}"

                            ' Save the contribution in the DataRow
                            If dt.Columns.Contains(columnName) Then
                                row(columnName) = contribution
                            Else
                                Throw New Exception($"Column '{columnName}' does not exist in the DataTable.")
                            End If
                        Next

                        'save the result for this rule + scenario in the DataRow
                        Dim resultColumnName As String = $"{Scenario.Name}_{Name}"
                        If dt.Columns.Contains(resultColumnName) Then
                            row(resultColumnName) = ResultSum / totalWeight
                        Else
                            Throw New Exception($"Column '{resultColumnName}' does not exist in the DataTable.")
                        End If
                    Next
                    Setup.Log.WriteToDiagnosticsFile("All rows processed.")

                    ' At the end of each scenario, write the results back to the database
                    Setup.Log.WriteToDiagnosticsFile("Writing results back to the database.")
                    For Each row As DataRow In dt.Rows
                        Using cmdUpdate As New SQLiteCommand(Setup.GpkgCon)
                            Dim updateSetClauses As New List(Of String)
                            Dim myfid As Integer = Convert.ToInt32(row("fid"))
                            For Each columnName In columnsToUpdate
                                If dt.Columns.Contains(columnName) Then
                                    updateSetClauses.Add($"{columnName} = @{columnName}")
                                    cmdUpdate.Parameters.AddWithValue($"@{columnName}", row(columnName))
                                End If
                            Next

                            cmdUpdate.CommandText = $"UPDATE {Setup.GpkgTable} SET {String.Join(", ", updateSetClauses)} WHERE fid = {myfid};"
                            cmdUpdate.ExecuteNonQuery()
                        End Using
                    Next
                    Setup.Log.WriteToDiagnosticsFile("Results written.")

                    transaction.Commit() ' Committing transaction after all rows are updated
                End Using
                Setup.Log.WriteToDiagnosticsFile("Scenario " & Scenario.Name & " processed.")
            Next

            Return True
        Catch ex As Exception
            Setup.Log.WriteToDiagnosticsFile("Error executing rule " & Name & ": " & ex.Message)
            Me.Setup.Log.AddError("Error executing rule " & Name & ": " & ex.Message)
            Return False
        End Try
    End Function





    Public Sub EnsureColumnExists(tableName As String, columnName As String, dataType As String)
        Dim columnExists As Boolean = False

        Using cmd As New SQLiteCommand($"PRAGMA table_info({tableName});", Setup.GpkgCon)
            Using reader As SQLiteDataReader = cmd.ExecuteReader()
                While reader.Read()
                    If String.Equals(reader("name").ToString(), columnName, StringComparison.OrdinalIgnoreCase) Then
                        columnExists = True
                        Exit While
                    End If
                End While
            End Using
        End Using

        If Not columnExists Then
            Using cmd As New SQLiteCommand($"ALTER TABLE {tableName} ADD COLUMN {columnName} {dataType};", Setup.GpkgCon)
                cmd.ExecuteNonQuery()
            End Using
        End If
    End Sub

    Public Function getFieldNamesForScenario(Scenario As clsScenario) As List(Of String)
        'returns the field names that are needed for this rule and scenario
        Dim Fields As New List(Of String)
        For Each Benchmark As clsBenchmark In Benchmarks.Values
            If Benchmark.FieldNamesPerScenario.ContainsKey(Scenario.Name.Trim.ToUpper) Then Fields.Add(Benchmark.FieldNamesPerScenario.Item(Scenario.Name.Trim.ToUpper))
        Next
        Return Fields
    End Function

    Public Function getConstantsForScenario(Scenario As clsScenario) As List(Of Double)
        'returns the constants that are needed for this rule and scenario
        Dim Constants As New List(Of Double)
        For Each Benchmark As clsBenchmark In Benchmarks.Values
            If Benchmark.ConstantsPerScenario.ContainsKey(Scenario.Name.Trim.ToUpper) Then Constants.Add(Benchmark.ConstantsPerScenario.Item(Scenario.Name.Trim.ToUpper))
        Next
        Return Constants
    End Function

End Class


Public Class clsEquationComponent
    Friend Benchmark As clsBenchmark                'de maatlat
    Friend Weight As Double                         'weging van deze maatlat
    Friend ResultsFieldName As String
    Friend ResultsField As clsSQLiteField
    Friend Result As Double
    Private Rule As clsRule
    Private Setup As clsKlimaatatlas

    Public Sub New(ByRef mySetup As clsKlimaatatlas, ByRef myRule As clsRule, myBenchmarkName As String, myWeight As Double, myResultsFieldName As String)
        Setup = mySetup
        Rule = myRule
        Benchmark = Rule.Benchmarks.Item(myBenchmarkName.Trim.ToUpper)
        Weight = myWeight
        ResultsFieldName = myResultsFieldName
    End Sub

    Public Function calculateResult(value As Object) As Boolean
        'this function calculates the result of this component for a given value
        'then applies the weight to it and writes it to the Result property
        Result = Benchmark.getResult(value) * Weight
        Return True
    End Function

End Class
