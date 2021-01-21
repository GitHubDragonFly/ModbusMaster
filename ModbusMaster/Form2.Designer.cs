namespace ModbusMaster
{
    partial class Form2
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = new System.ComponentModel.Container();

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form2));
            this.lblCharacter = new System.Windows.Forms.Label();
            this.cbCharacter = new System.Windows.Forms.ComboBox();
            this.lblBit = new System.Windows.Forms.Label();
            this.cbBit = new System.Windows.Forms.ComboBox();
            this.lblStringLength = new System.Windows.Forms.Label();
            this.cbStringLength = new System.Windows.Forms.ComboBox();
            this.lblModifier = new System.Windows.Forms.Label();
            this.cbModifier = new System.Windows.Forms.ComboBox();
            this.lblValidAddresses = new System.Windows.Forms.Label();
            this.lblAddress = new System.Windows.Forms.Label();
            this.lblIO = new System.Windows.Forms.Label();
            this.tbUserAddress = new System.Windows.Forms.TextBox();
            this.lblModbusAddress = new System.Windows.Forms.Label();
            this.tbResultingAddress = new System.Windows.Forms.TextBox();
            this.Button1 = new System.Windows.Forms.Button();
            this.cbIO = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // lblCharacter
            // 
            this.lblCharacter.AutoSize = true;
            this.lblCharacter.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.lblCharacter.Location = new System.Drawing.Point(402, 11);
            this.lblCharacter.Name = "lblCharacter";
            this.lblCharacter.Size = new System.Drawing.Size(53, 13);
            this.lblCharacter.TabIndex = 36;
            this.lblCharacter.Text = "Character";
            this.lblCharacter.MouseHover += new System.EventHandler(this.LabelCharacter_MouseHover);
            // 
            // cbCharacter
            // 
            this.cbCharacter.DropDownWidth = 35;
            this.cbCharacter.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cbCharacter.FormattingEnabled = true;
            this.cbCharacter.Items.AddRange(new object[] {
            " ",
            ".1",
            ".2",
            ".3",
            ".4",
            ".5",
            ".6",
            ".7",
            ".8",
            ".9",
            ".10",
            ".11",
            ".12",
            ".13",
            ".14",
            ".15",
            ".16",
            ".17",
            ".18",
            ".19",
            ".20",
            ".21",
            ".22",
            ".23",
            ".24",
            ".25",
            ".26",
            ".27",
            ".28",
            ".29",
            ".30",
            ".31",
            ".32",
            ".33",
            ".34",
            ".35",
            ".36",
            ".37",
            ".38",
            ".39",
            ".40",
            ".41",
            ".42",
            ".43",
            ".44",
            ".45",
            ".46",
            ".47",
            ".48",
            ".49",
            ".50",
            ".51",
            ".52",
            ".53",
            ".54",
            ".55",
            ".56",
            ".57",
            ".58",
            ".59",
            ".60",
            ".61",
            ".62",
            ".63",
            ".64",
            ".65",
            ".66",
            ".67",
            ".68",
            ".69",
            ".70",
            ".71",
            ".72",
            ".73",
            ".74",
            ".75",
            ".76",
            ".77",
            ".78",
            ".79",
            ".80",
            ".81",
            ".82",
            ".83",
            ".84",
            ".85",
            ".86",
            ".87",
            ".88",
            ".89",
            ".90",
            ".91",
            ".92",
            ".93",
            ".94",
            ".95",
            ".96",
            ".97",
            ".98",
            ".99",
            ".100"});
            this.cbCharacter.Location = new System.Drawing.Point(397, 28);
            this.cbCharacter.MaxDropDownItems = 6;
            this.cbCharacter.Name = "cbCharacter";
            this.cbCharacter.Size = new System.Drawing.Size(65, 33);
            this.cbCharacter.TabIndex = 35;
            this.cbCharacter.SelectedIndexChanged += new System.EventHandler(this.ComboBoxString_SelectedIndexChanged);
            // 
            // lblBit
            // 
            this.lblBit.AutoSize = true;
            this.lblBit.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.lblBit.Location = new System.Drawing.Point(212, 11);
            this.lblBit.Name = "lblBit";
            this.lblBit.Size = new System.Drawing.Size(19, 13);
            this.lblBit.TabIndex = 34;
            this.lblBit.Text = "Bit";
            this.lblBit.MouseHover += new System.EventHandler(this.LabelBit_MouseHover);
            // 
            // cbBit
            // 
            this.cbBit.DropDownWidth = 35;
            this.cbBit.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cbBit.FormattingEnabled = true;
            this.cbBit.Items.AddRange(new object[] {
            " ",
            ".0",
            ".1",
            ".2",
            ".3",
            ".4",
            ".5",
            ".6",
            ".7",
            ".8",
            ".9",
            ".10",
            ".11",
            ".12",
            ".13",
            ".14",
            ".15",
            ".16",
            ".17",
            ".18",
            ".19",
            ".20",
            ".21",
            ".22",
            ".23",
            ".24",
            ".25",
            ".26",
            ".27",
            ".28",
            ".29",
            ".30",
            ".31",
            ".32",
            ".33",
            ".34",
            ".35",
            ".36",
            ".37",
            ".38",
            ".39",
            ".40",
            ".41",
            ".42",
            ".43",
            ".44",
            ".45",
            ".46",
            ".47",
            ".48",
            ".49",
            ".50",
            ".51",
            ".52",
            ".53",
            ".54",
            ".55",
            ".56",
            ".57",
            ".58",
            ".59",
            ".60",
            ".61",
            ".62",
            ".63",
            ".64",
            ".65",
            ".66",
            ".67",
            ".68",
            ".69",
            ".70",
            ".71",
            ".72",
            ".73",
            ".74",
            ".75",
            ".76",
            ".77",
            ".78",
            ".79",
            ".80",
            ".81",
            ".82",
            ".83",
            ".84",
            ".85",
            ".86",
            ".87",
            ".88",
            ".89",
            ".90",
            ".91",
            ".92",
            ".93",
            ".94",
            ".95",
            ".96",
            ".97",
            ".98",
            ".99",
            ".100",
            ".101",
            ".102",
            ".103",
            ".104",
            ".105",
            ".106",
            ".107",
            ".108",
            ".109",
            ".110",
            ".111",
            ".112",
            ".113",
            ".114",
            ".115",
            ".116",
            ".117",
            ".118",
            ".119",
            ".120",
            ".121",
            ".122",
            ".123",
            ".124",
            ".125",
            ".126",
            ".127"});
            this.cbBit.Location = new System.Drawing.Point(195, 28);
            this.cbBit.MaxDropDownItems = 4;
            this.cbBit.Name = "cbBit";
            this.cbBit.Size = new System.Drawing.Size(59, 33);
            this.cbBit.TabIndex = 33;
            this.cbBit.SelectedIndexChanged += new System.EventHandler(this.ComboBoxBit_SelectedIndexChanged);
            // 
            // lblStringLength
            // 
            this.lblStringLength.AutoSize = true;
            this.lblStringLength.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.lblStringLength.Location = new System.Drawing.Point(322, 11);
            this.lblStringLength.Name = "lblStringLength";
            this.lblStringLength.Size = new System.Drawing.Size(70, 13);
            this.lblStringLength.TabIndex = 32;
            this.lblStringLength.Text = "String Length";
            // 
            // cbStringLength
            // 
            this.cbStringLength.DropDownWidth = 35;
            this.cbStringLength.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cbStringLength.FormattingEnabled = true;
            this.cbStringLength.Items.AddRange(new object[] {
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10",
            "11",
            "12",
            "13",
            "14",
            "15",
            "16",
            "17",
            "18",
            "19",
            "20",
            "21",
            "22",
            "23",
            "24",
            "25",
            "26",
            "27",
            "28",
            "29",
            "30",
            "31",
            "32",
            "33",
            "34",
            "35",
            "36",
            "37",
            "38",
            "39",
            "40",
            "41",
            "42",
            "43",
            "44",
            "45",
            "46",
            "47",
            "48",
            "49",
            "50",
            "51",
            "52",
            "53",
            "54",
            "55",
            "56",
            "57",
            "58",
            "59",
            "60",
            "61",
            "62",
            "63",
            "64",
            "65",
            "66",
            "67",
            "68",
            "69",
            "70",
            "71",
            "72",
            "73",
            "74",
            "75",
            "76",
            "77",
            "78",
            "79",
            "80",
            "81",
            "82",
            "83",
            "84",
            "85",
            "86",
            "87",
            "88",
            "89",
            "90",
            "91",
            "92",
            "93",
            "94",
            "95",
            "96",
            "97",
            "98",
            "99",
            "100"});
            this.cbStringLength.Location = new System.Drawing.Point(325, 28);
            this.cbStringLength.MaxDropDownItems = 6;
            this.cbStringLength.Name = "cbStringLength";
            this.cbStringLength.Size = new System.Drawing.Size(65, 33);
            this.cbStringLength.TabIndex = 31;
            this.cbStringLength.SelectedIndexChanged += new System.EventHandler(this.ComboBoxString_SelectedIndexChanged);
            // 
            // lblModifier
            // 
            this.lblModifier.AutoSize = true;
            this.lblModifier.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.lblModifier.Location = new System.Drawing.Point(270, 11);
            this.lblModifier.Name = "lblModifier";
            this.lblModifier.Size = new System.Drawing.Size(44, 13);
            this.lblModifier.TabIndex = 30;
            this.lblModifier.Text = "Modifier";
            this.lblModifier.MouseHover += new System.EventHandler(this.LabelModifier_MouseHover);
            // 
            // cbModifier
            // 
            this.cbModifier.DropDownWidth = 35;
            this.cbModifier.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cbModifier.FormattingEnabled = true;
            this.cbModifier.Items.AddRange(new object[] {
            " ",
            "U",
            "S",
            "F",
            "L",
            "UL",
            "FQ",
            "LQ",
            "UQ",
            "LO",
            "UO"});
            this.cbModifier.Location = new System.Drawing.Point(260, 28);
            this.cbModifier.MaxDropDownItems = 4;
            this.cbModifier.Name = "cbModifier";
            this.cbModifier.Size = new System.Drawing.Size(59, 33);
            this.cbModifier.TabIndex = 29;
            this.cbModifier.SelectedIndexChanged += new System.EventHandler(this.ComboBoxModifier_SelectedIndexChanged);
            // 
            // lblValidAddresses
            // 
            this.lblValidAddresses.AutoSize = true;
            this.lblValidAddresses.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblValidAddresses.ForeColor = System.Drawing.Color.SteelBlue;
            this.lblValidAddresses.Location = new System.Drawing.Point(68, 142);
            this.lblValidAddresses.Name = "lblValidAddresses";
            this.lblValidAddresses.Size = new System.Drawing.Size(355, 20);
            this.lblValidAddresses.TabIndex = 28;
            this.lblValidAddresses.Text = "Valid range: x00000 to x65534 (no offset applied)";
            this.lblValidAddresses.MouseHover += new System.EventHandler(this.LabelValidAddresses_MouseHover);
            // 
            // lblAddress
            // 
            this.lblAddress.AutoSize = true;
            this.lblAddress.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.lblAddress.Location = new System.Drawing.Point(100, 11);
            this.lblAddress.Name = "lblAddress";
            this.lblAddress.Size = new System.Drawing.Size(45, 13);
            this.lblAddress.TabIndex = 27;
            this.lblAddress.Text = "Address";
            // 
            // lblIO
            // 
            this.lblIO.AutoSize = true;
            this.lblIO.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.lblIO.Location = new System.Drawing.Point(30, 11);
            this.lblIO.Name = "lblIO";
            this.lblIO.Size = new System.Drawing.Size(23, 13);
            this.lblIO.TabIndex = 26;
            this.lblIO.Text = "I/O";
            // 
            // tbUserAddress
            // 
            this.tbUserAddress.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tbUserAddress.Location = new System.Drawing.Point(67, 27);
            this.tbUserAddress.MaxLength = 5;
            this.tbUserAddress.Name = "tbUserAddress";
            this.tbUserAddress.Size = new System.Drawing.Size(122, 35);
            this.tbUserAddress.TabIndex = 25;
            this.tbUserAddress.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.tbUserAddress.TextChanged += new System.EventHandler(this.TextBoxUserAddress_TextChanged);
            // 
            // lblModbusAddress
            // 
            this.lblModbusAddress.AutoSize = true;
            this.lblModbusAddress.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblModbusAddress.ForeColor = System.Drawing.Color.White;
            this.lblModbusAddress.Location = new System.Drawing.Point(22, 78);
            this.lblModbusAddress.Name = "lblModbusAddress";
            this.lblModbusAddress.Size = new System.Drawing.Size(68, 40);
            this.lblModbusAddress.TabIndex = 24;
            this.lblModbusAddress.Text = "Modbus\r\nAddress";
            // 
            // tbResultingAddress
            // 
            this.tbResultingAddress.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tbResultingAddress.ForeColor = System.Drawing.Color.Black;
            this.tbResultingAddress.Location = new System.Drawing.Point(99, 79);
            this.tbResultingAddress.Name = "tbResultingAddress";
            this.tbResultingAddress.ReadOnly = true;
            this.tbResultingAddress.Size = new System.Drawing.Size(278, 35);
            this.tbResultingAddress.TabIndex = 23;
            this.tbResultingAddress.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.tbResultingAddress.BackColorChanged += new System.EventHandler(this.TextBoxResultingAddress_BackColorChanged);
            this.tbResultingAddress.TextChanged += new System.EventHandler(this.TextBoxResultingAddress_TextChanged);
            // 
            // Button1
            // 
            this.Button1.BackColor = System.Drawing.Color.Gainsboro;
            this.Button1.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Button1.ForeColor = System.Drawing.Color.Blue;
            this.Button1.Location = new System.Drawing.Point(395, 79);
            this.Button1.Name = "Button1";
            this.Button1.Size = new System.Drawing.Size(64, 36);
            this.Button1.TabIndex = 22;
            this.Button1.Text = "OK";
            this.Button1.UseVisualStyleBackColor = false;
            // 
            // cbIO
            // 
            this.cbIO.DropDownWidth = 35;
            this.cbIO.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cbIO.FormattingEnabled = true;
            this.cbIO.Items.AddRange(new object[] {
            "0",
            "1",
            "3",
            "4"});
            this.cbIO.Location = new System.Drawing.Point(23, 28);
            this.cbIO.MaxDropDownItems = 4;
            this.cbIO.Name = "cbIO";
            this.cbIO.Size = new System.Drawing.Size(38, 33);
            this.cbIO.Sorted = true;
            this.cbIO.TabIndex = 21;
            this.cbIO.SelectedIndexChanged += new System.EventHandler(this.ComboBoxIO_SelectedIndexChanged);
            // 
            // Form2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.MidnightBlue;
            this.ClientSize = new System.Drawing.Size(484, 173);
            this.Controls.Add(this.lblCharacter);
            this.Controls.Add(this.cbCharacter);
            this.Controls.Add(this.lblBit);
            this.Controls.Add(this.cbBit);
            this.Controls.Add(this.lblStringLength);
            this.Controls.Add(this.cbStringLength);
            this.Controls.Add(this.lblModifier);
            this.Controls.Add(this.cbModifier);
            this.Controls.Add(this.lblValidAddresses);
            this.Controls.Add(this.lblAddress);
            this.Controls.Add(this.lblIO);
            this.Controls.Add(this.tbUserAddress);
            this.Controls.Add(this.lblModbusAddress);
            this.Controls.Add(this.tbResultingAddress);
            this.Controls.Add(this.Button1);
            this.Controls.Add(this.cbIO);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(500, 212);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(500, 212);
            this.Name = "Form2";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Select Modbus Address";
            this.Load += new System.EventHandler(this.Form2_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblCharacter;
        private System.Windows.Forms.ComboBox cbCharacter;
        private System.Windows.Forms.Label lblBit;
        private System.Windows.Forms.ComboBox cbBit;
        internal System.Windows.Forms.Label lblStringLength;
        private System.Windows.Forms.ComboBox cbStringLength;
        private System.Windows.Forms.Label lblModifier;
        private System.Windows.Forms.ComboBox cbModifier;
        internal System.Windows.Forms.Label lblValidAddresses;
        internal System.Windows.Forms.Label lblAddress;
        internal System.Windows.Forms.Label lblIO;
        private System.Windows.Forms.TextBox tbUserAddress;
        internal System.Windows.Forms.Label lblModbusAddress;
        private System.Windows.Forms.TextBox tbResultingAddress;
        internal System.Windows.Forms.Button Button1;
        private System.Windows.Forms.ComboBox cbIO;
    }
}