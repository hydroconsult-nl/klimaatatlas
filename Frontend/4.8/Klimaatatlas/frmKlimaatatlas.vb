Imports System.IO
Imports Newtonsoft.Json.Linq
Imports System.Windows.Forms
Imports MapWinGIS

Public Class frmKlimaatatlas
    Public Klimaatatlas As clsKlimaatatlas
    Public Flowchart As clsFlowchart
    Private Sub frmKlimaatatlas_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Klimaatatlas = New clsKlimaatatlas()
        Klimaatatlas.SetProgressBar(prProgress, lblProgress)

        'txtDatabase.Text = My.Settings.Database
        txtConfigFile.Text = My.Settings.Configfile

        Initialize()

        '' System.IO.File.Exists(txtDatabase.Text) AndAlso 
        If System.IO.File.Exists(txtConfigFile.Text) Then
            ReadConfiguration()
        End If

        ' Assuming you have a TabControl named TabControl1 with at least one TabPage
        AddHandler TabControl1.TabPages(1).Paint, AddressOf Benchmarks_Paint

    End Sub
    Private Sub Benchmarks_Paint(sender As Object, e As PaintEventArgs) Handles MyBase.Paint

        ' Initialize position
        Dim vPos As Integer = 50
        Dim labelWidth As Integer = 100  ' Set your desired label width

        'for now, pick the first rule to render
        If Klimaatatlas.Rules.ContainsKey(cmbRekenregels.Text.Trim.ToUpper) Then
            Dim Rule As clsRule = Klimaatatlas.Rules.Item(cmbRekenregels.Text.Trim.ToUpper)
            For Each Benchmark As clsBenchmark In Rule.Benchmarks.Values
                ' Create and configure Label
                Dim label As New System.Windows.Forms.Label()
                label.Text = Benchmark.Name  ' Assuming Name is a property of clsBenchmark
                label.Location = New System.Drawing.Point(20, vPos)
                label.Width = labelWidth
                label.TextAlign = ContentAlignment.MiddleLeft


                ' Add the label to the TabPage's Controls collection
                TabControl1.TabPages(1).Controls.Add(label)

                ' Set position for color scale rectangle (next to the label)
                Dim rect As New Rectangle(40 + labelWidth + 5, vPos, 300, 20) ' Adjust the starting X position and width as necessary
                Benchmark.colorScale.DrawColorScale(e, rect)  ' Assuming DrawColorScale is a method that draws the color scale

                ' Increment position for next item
                vPos += 50  ' Adjust as necessary for proper spacing
            Next
        End If

    End Sub



    Private Sub DrawingPanel_Paint(sender As Object, e As PaintEventArgs) Handles pnlFlowchart.Paint
        If Flowchart IsNot Nothing Then
            Flowchart.DrawFlowchart(e.Graphics)
        End If
    End Sub

    Private Sub Initialize()
        'set the database connection, read the configuration file and start processing each rule
        Klimaatatlas.SetProgressBar(prProgress, lblProgress)
        'If System.IO.File.Exists(txtDatabase.Text) Then
        '    Klimaatatlas.SetDatabaseConnection(txtDatabase.Text)
        '    Klimaatatlas.UpgradeDatabase()
        'End If
    End Sub

    Private Sub ReadConfiguration()
        Me.Klimaatatlas.Generalfunctions.UpdateProgressBar("Reading configuration...", 0, 10, True)
        Klimaatatlas.ReadConfigurationFile(txtConfigFile.Text)
        Me.Klimaatatlas.Generalfunctions.UpdateProgressBar("Populating rules...", 3, 10, True)
        Klimaatatlas.PopulateRules(cmbRekenregels)
        Me.Klimaatatlas.Generalfunctions.UpdateProgressBar("Populating scenarios...", 7, 10, True)
        Klimaatatlas.PopulateScenarios()
        Me.Klimaatatlas.Generalfunctions.UpdateProgressBar("Configuration successfully read", 0, 10, True)
    End Sub

    Private Sub btnExecute_Click(sender As Object, e As EventArgs) Handles btnExecute.Click

        Klimaatatlas.SetProgressBar(prProgress, lblProgress)
        Klimaatatlas.Log = New clsLog

        'store our paths for the next time
        'My.Settings.Database = txtDatabase.Text
        My.Settings.Configfile = txtConfigFile.Text
        My.Settings.Save()

        'initialize our diagnostics file. It will be written to the same dir as our config file
        Klimaatatlas.Log.SetDiagnosticsPath(Path.GetDirectoryName(txtConfigFile.Text) & "\klimaatatlas.dia")

        'read our configuration file
        Klimaatatlas.Log.WriteToDiagnosticsFile("Reading configuration file.")
        Call ReadConfiguration()
        Klimaatatlas.Log.WriteToDiagnosticsFile("Configuration file read.")

        'open a connection to our geopackage
        Klimaatatlas.Log.WriteToDiagnosticsFile("Setting geopackage connection.")
        Klimaatatlas.SetGeoPackageConnection()
        Klimaatatlas.Log.WriteToDiagnosticsFile("Geopackage connection set.")

        'process our rules
        Klimaatatlas.Log.WriteToDiagnosticsFile("Processing rules.")
        Klimaatatlas.ProcessRules()
        Klimaatatlas.Log.WriteToDiagnosticsFile("Rules processed.")

        'close the connection to the geopackage
        Klimaatatlas.Log.WriteToDiagnosticsFile("Closing geopackage.")
        Klimaatatlas.GpkgCon.Close()
        Klimaatatlas.Log.WriteToDiagnosticsFile("Geopackage closed.")


    End Sub

    Private Sub btnDatabase_Click(sender As Object, e As EventArgs)
        dlgOpenFile.Filter = "SQLite|*.db"
        Dim res As DialogResult = dlgOpenFile.ShowDialog
        If res = DialogResult.OK Then
            'txtDatabase.Text = dlgOpenFile.FileName
            Klimaatatlas.SetDatabaseConnection(dlgOpenFile.FileName) ' As New clsKlimaatatlas(jsonPath, connectionString, configContent)
            Klimaatatlas.UpgradeDatabase()
        End If
    End Sub

    Private Sub btnConfigFile_Click(sender As Object, e As EventArgs) Handles btnConfigFile.Click
        dlgOpenFile.Filter = "JSON|*.json"
        Dim res As DialogResult = dlgOpenFile.ShowDialog
        If res = DialogResult.OK Then
            txtConfigFile.Text = dlgOpenFile.FileName

            'remember this path for the next time we open our application
            My.Settings.Configfile = txtConfigFile.Text
            My.Settings.Save()

            'read the configuration
            ReadConfiguration()
        End If
    End Sub



    Private Sub cmbIndicators_SelectedValueChanged(sender As Object, e As EventArgs) Handles cmbIndicators.SelectedValueChanged
        'as a first test I would like to create a simple flowchart containing just two nodes and one connection
        Flowchart = New clsFlowchart()

        pnlFlowchart.Invalidate()
        pnlFlowchart.Update()


    End Sub

    Private Sub cmbIndicators_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbIndicators.SelectedIndexChanged

    End Sub

    Private Sub cmbRekenregels_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbRekenregels.SelectedIndexChanged
        ' Trigger Repaint
        Me.Refresh()
        ' Or you could invalidate and update the specific control where benchmarks are drawn
        ' YourControl.Invalidate()
        ' YourControl.Update()

    End Sub

    Private Sub AboutToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles AboutToolStripMenuItem.Click
        Dim myForm As New frmAbout
        myForm.ShowDialog()
    End Sub
End Class
