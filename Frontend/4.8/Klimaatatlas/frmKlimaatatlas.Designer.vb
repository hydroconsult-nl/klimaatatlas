<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class frmKlimaatatlas
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmKlimaatatlas))
        Me.btnExecute = New System.Windows.Forms.Button()
        Me.prProgress = New System.Windows.Forms.ProgressBar()
        Me.lblProgress = New System.Windows.Forms.Label()
        Me.btnConfigFile = New System.Windows.Forms.Button()
        Me.txtConfigFile = New System.Windows.Forms.TextBox()
        Me.lblConfigFile = New System.Windows.Forms.Label()
        Me.dlgOpenFile = New System.Windows.Forms.OpenFileDialog()
        Me.MenuStrip1 = New System.Windows.Forms.MenuStrip()
        Me.ToolsToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.GISToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.SpatialJoinToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.SpatiallyInterpolatePointStatisticsToGeopackageToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.DatabaseToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.SpatialInterpolationToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.AboutToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.dlgSaveFile = New System.Windows.Forms.SaveFileDialog()
        Me.TabControl1 = New System.Windows.Forms.TabControl()
        Me.tabSettings = New System.Windows.Forms.TabPage()
        Me.tabMaatlatten = New System.Windows.Forms.TabPage()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.cmbRekenregels = New System.Windows.Forms.ComboBox()
        Me.tabRekenregels = New System.Windows.Forms.TabPage()
        Me.MenuStrip1.SuspendLayout()
        Me.TabControl1.SuspendLayout()
        Me.tabSettings.SuspendLayout()
        Me.tabMaatlatten.SuspendLayout()
        Me.SuspendLayout()
        '
        'btnExecute
        '
        Me.btnExecute.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnExecute.Location = New System.Drawing.Point(1146, 498)
        Me.btnExecute.Name = "btnExecute"
        Me.btnExecute.Size = New System.Drawing.Size(125, 65)
        Me.btnExecute.TabIndex = 0
        Me.btnExecute.Text = "Execute"
        Me.btnExecute.UseVisualStyleBackColor = True
        '
        'prProgress
        '
        Me.prProgress.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.prProgress.Location = New System.Drawing.Point(14, 539)
        Me.prProgress.Name = "prProgress"
        Me.prProgress.Size = New System.Drawing.Size(1113, 25)
        Me.prProgress.TabIndex = 1
        '
        'lblProgress
        '
        Me.lblProgress.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.lblProgress.AutoSize = True
        Me.lblProgress.Location = New System.Drawing.Point(14, 512)
        Me.lblProgress.Name = "lblProgress"
        Me.lblProgress.Size = New System.Drawing.Size(62, 16)
        Me.lblProgress.TabIndex = 2
        Me.lblProgress.Text = "Progress"
        '
        'btnConfigFile
        '
        Me.btnConfigFile.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnConfigFile.Location = New System.Drawing.Point(1215, 20)
        Me.btnConfigFile.Name = "btnConfigFile"
        Me.btnConfigFile.Size = New System.Drawing.Size(27, 26)
        Me.btnConfigFile.TabIndex = 5
        Me.btnConfigFile.Text = ".."
        Me.btnConfigFile.UseVisualStyleBackColor = True
        '
        'txtConfigFile
        '
        Me.txtConfigFile.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.txtConfigFile.Location = New System.Drawing.Point(124, 21)
        Me.txtConfigFile.Name = "txtConfigFile"
        Me.txtConfigFile.Size = New System.Drawing.Size(1081, 22)
        Me.txtConfigFile.TabIndex = 4
        '
        'lblConfigFile
        '
        Me.lblConfigFile.AutoSize = True
        Me.lblConfigFile.Location = New System.Drawing.Point(8, 24)
        Me.lblConfigFile.Name = "lblConfigFile"
        Me.lblConfigFile.Size = New System.Drawing.Size(68, 16)
        Me.lblConfigFile.TabIndex = 3
        Me.lblConfigFile.Text = "Config file:"
        '
        'dlgOpenFile
        '
        Me.dlgOpenFile.FileName = "dlgOpenFile"
        '
        'MenuStrip1
        '
        Me.MenuStrip1.ImageScalingSize = New System.Drawing.Size(24, 24)
        Me.MenuStrip1.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ToolsToolStripMenuItem, Me.DatabaseToolStripMenuItem, Me.AboutToolStripMenuItem})
        Me.MenuStrip1.Location = New System.Drawing.Point(0, 0)
        Me.MenuStrip1.Name = "MenuStrip1"
        Me.MenuStrip1.Padding = New System.Windows.Forms.Padding(5, 1, 0, 1)
        Me.MenuStrip1.Size = New System.Drawing.Size(1285, 26)
        Me.MenuStrip1.TabIndex = 4
        Me.MenuStrip1.Text = "MenuStrip1"
        '
        'ToolsToolStripMenuItem
        '
        Me.ToolsToolStripMenuItem.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {Me.GISToolStripMenuItem})
        Me.ToolsToolStripMenuItem.Name = "ToolsToolStripMenuItem"
        Me.ToolsToolStripMenuItem.Size = New System.Drawing.Size(58, 24)
        Me.ToolsToolStripMenuItem.Text = "Tools"
        '
        'GISToolStripMenuItem
        '
        Me.GISToolStripMenuItem.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {Me.SpatialJoinToolStripMenuItem, Me.SpatiallyInterpolatePointStatisticsToGeopackageToolStripMenuItem})
        Me.GISToolStripMenuItem.Name = "GISToolStripMenuItem"
        Me.GISToolStripMenuItem.Size = New System.Drawing.Size(114, 26)
        Me.GISToolStripMenuItem.Text = "GIS"
        '
        'SpatialJoinToolStripMenuItem
        '
        Me.SpatialJoinToolStripMenuItem.Name = "SpatialJoinToolStripMenuItem"
        Me.SpatialJoinToolStripMenuItem.Size = New System.Drawing.Size(550, 26)
        Me.SpatialJoinToolStripMenuItem.Text = "Spatial Join"
        '
        'SpatiallyInterpolatePointStatisticsToGeopackageToolStripMenuItem
        '
        Me.SpatiallyInterpolatePointStatisticsToGeopackageToolStripMenuItem.Name = "SpatiallyInterpolatePointStatisticsToGeopackageToolStripMenuItem"
        Me.SpatiallyInterpolatePointStatisticsToGeopackageToolStripMenuItem.Size = New System.Drawing.Size(550, 26)
        Me.SpatiallyInterpolatePointStatisticsToGeopackageToolStripMenuItem.Text = "Spatial interpolation of point statistics from database to geopackage"
        '
        'DatabaseToolStripMenuItem
        '
        Me.DatabaseToolStripMenuItem.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {Me.SpatialInterpolationToolStripMenuItem})
        Me.DatabaseToolStripMenuItem.Name = "DatabaseToolStripMenuItem"
        Me.DatabaseToolStripMenuItem.Size = New System.Drawing.Size(122, 24)
        Me.DatabaseToolStripMenuItem.Text = "GIS Operations"
        '
        'SpatialInterpolationToolStripMenuItem
        '
        Me.SpatialInterpolationToolStripMenuItem.Name = "SpatialInterpolationToolStripMenuItem"
        Me.SpatialInterpolationToolStripMenuItem.Size = New System.Drawing.Size(228, 26)
        Me.SpatialInterpolationToolStripMenuItem.Text = "Spatial Interpolation"
        '
        'AboutToolStripMenuItem
        '
        Me.AboutToolStripMenuItem.Name = "AboutToolStripMenuItem"
        Me.AboutToolStripMenuItem.Size = New System.Drawing.Size(64, 24)
        Me.AboutToolStripMenuItem.Text = "About"
        '
        'TabControl1
        '
        Me.TabControl1.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TabControl1.Controls.Add(Me.tabSettings)
        Me.TabControl1.Controls.Add(Me.tabMaatlatten)
        Me.TabControl1.Controls.Add(Me.tabRekenregels)
        Me.TabControl1.Location = New System.Drawing.Point(14, 29)
        Me.TabControl1.Name = "TabControl1"
        Me.TabControl1.SelectedIndex = 0
        Me.TabControl1.Size = New System.Drawing.Size(1257, 463)
        Me.TabControl1.TabIndex = 6
        '
        'tabSettings
        '
        Me.tabSettings.Controls.Add(Me.btnConfigFile)
        Me.tabSettings.Controls.Add(Me.lblConfigFile)
        Me.tabSettings.Controls.Add(Me.txtConfigFile)
        Me.tabSettings.Location = New System.Drawing.Point(4, 25)
        Me.tabSettings.Name = "tabSettings"
        Me.tabSettings.Padding = New System.Windows.Forms.Padding(3)
        Me.tabSettings.Size = New System.Drawing.Size(1249, 434)
        Me.tabSettings.TabIndex = 0
        Me.tabSettings.Text = "Instellingen"
        Me.tabSettings.UseVisualStyleBackColor = True
        '
        'tabMaatlatten
        '
        Me.tabMaatlatten.Controls.Add(Me.Label2)
        Me.tabMaatlatten.Controls.Add(Me.cmbRekenregels)
        Me.tabMaatlatten.Location = New System.Drawing.Point(4, 25)
        Me.tabMaatlatten.Name = "tabMaatlatten"
        Me.tabMaatlatten.Padding = New System.Windows.Forms.Padding(3)
        Me.tabMaatlatten.Size = New System.Drawing.Size(1249, 434)
        Me.tabMaatlatten.TabIndex = 1
        Me.tabMaatlatten.Text = "Maatlatten"
        Me.tabMaatlatten.UseVisualStyleBackColor = True
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(19, 16)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(81, 16)
        Me.Label2.TabIndex = 7
        Me.Label2.Text = "Rekenregel:"
        '
        'cmbRekenregels
        '
        Me.cmbRekenregels.FormattingEnabled = True
        Me.cmbRekenregels.Location = New System.Drawing.Point(166, 11)
        Me.cmbRekenregels.Name = "cmbRekenregels"
        Me.cmbRekenregels.Size = New System.Drawing.Size(342, 24)
        Me.cmbRekenregels.TabIndex = 0
        '
        'tabRekenregels
        '
        Me.tabRekenregels.Location = New System.Drawing.Point(4, 25)
        Me.tabRekenregels.Name = "tabRekenregels"
        Me.tabRekenregels.Size = New System.Drawing.Size(1249, 434)
        Me.tabRekenregels.TabIndex = 2
        Me.tabRekenregels.Text = "Rekenregels"
        Me.tabRekenregels.UseVisualStyleBackColor = True
        '
        'frmKlimaatatlas
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1285, 576)
        Me.Controls.Add(Me.TabControl1)
        Me.Controls.Add(Me.lblProgress)
        Me.Controls.Add(Me.prProgress)
        Me.Controls.Add(Me.btnExecute)
        Me.Controls.Add(Me.MenuStrip1)
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.MainMenuStrip = Me.MenuStrip1
        Me.Name = "frmKlimaatatlas"
        Me.Text = "Klimaatatlas"
        Me.WindowState = System.Windows.Forms.FormWindowState.Maximized
        Me.MenuStrip1.ResumeLayout(False)
        Me.MenuStrip1.PerformLayout()
        Me.TabControl1.ResumeLayout(False)
        Me.tabSettings.ResumeLayout(False)
        Me.tabSettings.PerformLayout()
        Me.tabMaatlatten.ResumeLayout(False)
        Me.tabMaatlatten.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents btnExecute As Button
    Friend WithEvents prProgress As ProgressBar
    Friend WithEvents lblProgress As Label
    Friend WithEvents btnConfigFile As Button
    Friend WithEvents txtConfigFile As TextBox
    Friend WithEvents lblConfigFile As Label
    Friend WithEvents dlgOpenFile As OpenFileDialog
    Friend WithEvents MenuStrip1 As MenuStrip
    Friend WithEvents ToolsToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents GISToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents SpatialJoinToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents dlgSaveFile As SaveFileDialog
    Friend WithEvents Flowcharts As GroupBox
    Friend WithEvents pnlFlowchart As Panel
    Friend WithEvents cmbIndicators As ComboBox
    Friend WithEvents TabControl1 As TabControl
    Friend WithEvents tabSettings As TabPage
    Friend WithEvents tabRekenregels As TabPage
    Friend WithEvents tabMaatlatten As TabPage
    Friend WithEvents Label2 As Label
    Friend WithEvents cmbRekenregels As ComboBox
    Friend WithEvents DatabaseToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents SpatialInterpolationToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents SpatiallyInterpolatePointStatisticsToGeopackageToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents AboutToolStripMenuItem As ToolStripMenuItem
End Class
