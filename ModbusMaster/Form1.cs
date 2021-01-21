// * C# version by Godra
// *
// * This program is just another implementation of Modbus protocol in the form of a standalone Modbus Master application
// * supporting RTU/TCP/UDP/RTUoverTCP/RTUoverUDP/ASCIIoverRTU/ASCIIoverTCP/ASCIIoverUDP and also supporting:
// * Strings, Double/Single Floating point, Int 128/64/32/16, UInt 128/64/32/16 values as well as bit or character Reading/Writing.
// *
// * It is using nModbus .NET 3.5 libraries, Copyright (c) 2006 Scott Alexander ( https://code.google.com/p/nmodbus/ ),
// * licensed under MIT license ( http://opensource.org/licenses/mit-license.php ) and included in the Project.
// * See the README.txt file in the "Resources" folder.
// *
// * No offset is applied to addresses so the address 400000 might be representing 400001.
// *
// * User interface default per register value is signed integer range -32768 to 32767.
// * The library itself will be writing/reading unsigned integer values (0 to 65534) per register.
// *
// * Tested with its counterpart Modbus RTU/TCP/UDP/ASCIIoverRTU Slave Simulator.
// * The program appears to be fully functional but provided AS IS (feel free to modify it but preserve all these comments).
// *
// * This particular version was designed for use in Windows and it does not work as such in Mono.
// * RTU/ASCIIoverRTU modes should be used together with com0com program to provide virtual serial
// * port pairs for communication with Modbus slave.
// *
// * There is also a TextBox added to allow manual input of the serial port to be used (generally intended for Linux usage, tty0tty virtual ports).
// *
// * Resource (128-bit Integer numbers): https://en.wikipedia.org/wiki/128-bit_computing
// * Signed Integer range: −170,141,183,460,469,231,731,687,303,715,884,105,728 (−2^127) through 170,141,183,460,469,231,731,687,303,715,884,105,727 (2^127 − 1)
// * Unsigned Integer range: 0 through 340,282,366,920,938,463,463,374,607,431,768,211,455 (2^128 − 1)

using Modbus.Device;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Numerics;
using System.Windows.Forms;

namespace ModbusMaster
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

			SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.ContainerControl | ControlStyles.SupportsTransparentBackColor, true);
		}

		private Modbus.Device.ModbusMaster MMaster;
		private SerialPort SerPort;
		private readonly ContextMenuStrip ContextListBox = new ContextMenuStrip();
		private bool FlagContextListBoxSet;
		private readonly ContextMenuStrip ContextLabelComm = new ContextMenuStrip();
		private bool FlagContextLabelCommSet;
		private string StrMessage = "";
		private string ReadValue = "";
		private bool EnableMasterMessages = true;
		private bool EnableSlaveMessages = true;
		private readonly ToolTip AllToolTip = new ToolTip();
		private Thread ConnectionBckgndThread;
		private Thread Slave1AutoReadBckgndThread;
		private Thread Slave2AutoReadBckgndThread;
		private int m_ProtocolType;
		private int m_cbSwapWords64bitSelectedIndex;
		private int m_cbSwapWords128bitSelectedIndex;
		public bool m_Abort;
		private readonly object m_Lock = new object();

		private delegate void ListMasterRequests(string strRequest);
		private delegate void ListSlaveResponses(string strRequest);

		private struct PollInfo
		{
			internal int Index;
			internal byte SlaveID;
			internal string NumberOfElements;
			internal string PlcAddress;
			internal string FunctionCode;
			internal int BitNumber;
		}

		private static readonly List<PollInfo> Slave1PollAddressList = new List<PollInfo>();
		private static readonly List<PollInfo> Slave2PollAddressList = new List<PollInfo>();

		private struct AddressInfo
		{
			internal RadioButton RadioBtn;
			internal ComboBox SlaveID;
			internal TextBox PlcAddress;
			internal CheckBox CheckBoxRead;
			internal CheckBox CheckBoxWrite;
			internal TextBox NumberOfElements;
			internal TextBox ValuesToWrite;
			internal Button ButtonSend;
			internal Label LabelStatus;
		}

		private readonly AddressInfo[] AddressList = new AddressInfo[8];

		private readonly Connection TCPUDPConnection = new Connection();

		// Magic Forms Access in C#
		// Reference: https://visualstudiomagazine.com/articles/2016/10/01/magic-forms-access.aspx
		// Provides access to Form2 and its public variables

		static internal partial class MyForms
		{
			// ----- Internal, hidden storage for the magic form.
			private static ModbusMaster.Form2 InternalForm2 = null;

			public static ModbusMaster.Form2 Form2
			{
				get
				{
					// ----- Ensure a form always appears when requested.
					if (InternalForm2 == null)
						InternalForm2 = new ModbusMaster.Form2();
					return InternalForm2;
				}
				set
				{
					// ----- Allow clearing, but not overwriting, of form.
					if (value == null)
						InternalForm2 = null;
					else
						throw new Exception("That's a no-no.");
				}
			}
		}

		#region "Requests / Updates"

		private void MasterRequests(string str)
		{
			if (InvokeRequired)
				Invoke(new ListMasterRequests(MasterRequests), new object[] { str });
			else
			{
				ListBox1.Items.Add(str);

				// Add and show master requests in the application window
				if (ListBox1.Items.Count > 2048)
					ListBox1.Items.RemoveAt(0);

				ListBox1.SelectedIndex = ListBox1.Items.Count - 1;
			}
		}

		private void SlaveResponses(string str)
		{
			if (InvokeRequired)
				Invoke(new ListSlaveResponses(SlaveResponses), new object[] { str });
			else
			{
				ListBox2.Items.Add(str);

				// Add and show slave responses or errors in the application window
				if (ListBox2.Items.Count > 2048)
					ListBox2.Items.RemoveAt(0);

				ListBox2.SelectedIndex = ListBox2.Items.Count - 1;
			}
		}

		#endregion

		#region "Private Methods"

		private void Form1_Load(object sender, EventArgs e)
		{
			ContextListBox.ItemClicked += ContextListBox_ItemClicked;
			tbNofE0.LostFocus += TextBoxNofE_LostFocus;
			tbNofE1.LostFocus += TextBoxNofE_LostFocus;
			tbNofE2.LostFocus += TextBoxNofE_LostFocus;
			tbNofE3.LostFocus += TextBoxNofE_LostFocus;
			tbNofE4.LostFocus += TextBoxNofE_LostFocus;
			tbNofE5.LostFocus += TextBoxNofE_LostFocus;
			tbNofE6.LostFocus += TextBoxNofE_LostFocus;
			tbNofE7.LostFocus += TextBoxNofE_LostFocus;

			cbCommMode.SelectedIndex = 0;
			cbSwapWords64bit.SelectedIndex = 0;
			cbSwapWords128bit.SelectedIndex = 0;
			cbPollInterval1.SelectedIndex = 5;
			cbPollInterval2.SelectedIndex = 5;

			GetSerialPorts();

			cbBaud.SelectedIndex = 15;
			cbDataBits.SelectedIndex = 3;
			cbParity.SelectedIndex = 0;
			cbStopBits.SelectedIndex = 0;

			btnCloseRTU.BackColor = Color.Gainsboro;
			btnCloseTCPUDP.BackColor = Color.Gainsboro;

			for (int i = 1; i < 248; i++)
			{
				cbID0.Items.Add(i);
				cbID1.Items.Add(i);
			}
			cbID0.SelectedIndex = 0;
			cbID1.SelectedIndex = 1;

			int j = 0;

			while (j < 8)
			{
				AddressList[j] = new AddressInfo();

				foreach (Control ctrl in Controls)
				{
					if (ctrl is GroupBox gbox)
					{
						if ((j < 4 && gbox.Name == "gbSlave1") || (j > 3 && gbox.Name == "gbSlave2"))
						{
							foreach (Control Child in ctrl.Controls)
							{
								if (Child is ComboBox cbox)
								{
									if (j < 4 && cbox.Name.Equals("cbID0"))
										AddressList[j].SlaveID = cbox;
									else if (j > 3 && cbox.Name.Equals("cbID1"))
										AddressList[j].SlaveID = cbox;
								}
								else if (Child is RadioButton rbtn)
								{
									if (rbtn.Name.Equals("rb" + j))
										AddressList[j].RadioBtn = rbtn;
								}
								else if (Child is TextBox tbox)
								{
									if (tbox.Name.Equals("tbAddr" + j))
										AddressList[j].PlcAddress = tbox;
									else if (tbox.Name.Equals("tbNofE" + j))
										AddressList[j].NumberOfElements = tbox;
									else if (tbox.Name.Equals("tbVtoW" + j))
										AddressList[j].ValuesToWrite = tbox;
								}
								else if (Child is CheckBox chbox)
								{
									if (chbox.Name.Equals("chbRead" + j))
										AddressList[j].CheckBoxRead = chbox;
									else if (chbox.Name.Equals("chbWrite" + j))
										AddressList[j].CheckBoxWrite = chbox;
								}
								else if (Child is Button btn)
								{
									if (btn.Name.Equals("btnSend" + j))
										AddressList[j].ButtonSend = btn;
								}
								else if (Child is Label lbl)
								{
									if (lbl.Name.Equals("lblStatus" + j))
										AddressList[j].LabelStatus = lbl;
								}
							}
						}
					}
				}

				j += 1;
			}

			Focus();
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (chbAutoRead1.Checked)
				chbAutoRead1.Checked = false;

			if (chbAutoRead2.Checked)
				chbAutoRead2.Checked = false;

			int index = 0;
			while (index < Application.OpenForms.Count)
			{
				if (!ReferenceEquals(Application.OpenForms[index], this))
					Application.OpenForms[index].Close();

				index += 1;
			}

			if (SerPort != null)
			{
				if (SerPort.IsOpen)
					SerPort.Close();

				SerPort.Dispose();
			}

			ContextListBox.ItemClicked -= ContextListBox_ItemClicked;
			tbNofE0.LostFocus -= TextBoxNofE_LostFocus;
			tbNofE1.LostFocus -= TextBoxNofE_LostFocus;
			tbNofE2.LostFocus -= TextBoxNofE_LostFocus;
			tbNofE3.LostFocus -= TextBoxNofE_LostFocus;
			tbNofE4.LostFocus -= TextBoxNofE_LostFocus;
			tbNofE5.LostFocus -= TextBoxNofE_LostFocus;
			tbNofE6.LostFocus -= TextBoxNofE_LostFocus;
			tbNofE7.LostFocus -= TextBoxNofE_LostFocus;
		}

		private void LabelComm_MouseClick(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				lblComm.ContextMenuStrip = ContextLabelComm;

				if (!FlagContextLabelCommSet)
				{
					ContextLabelComm.Items.Add("- Most Labels have hints, just hover the mouse pointer over them.");
					ContextLabelComm.Items.Add("- Double available addresses for a single slave device with ID2 = ID1.");
					ContextLabelComm.Items.Add("- No offset is applied, so the address 400000 might be representing 400001.");
					ContextLabelComm.Items.Add("- Maximum number of coils to read or write is 2000 per transaction.");
					ContextLabelComm.Items.Add("- Maximum number of registers to read or write is 125 per transaction.");
					ContextLabelComm.Items.Add("- 1xxxxx and 3xxxxx addresses allow the Read operation only.");
					ContextLabelComm.Items.Add("- 0xxxxx and 4xxxxx addresses allow both Read and Write operation.");
					ContextLabelComm.Items.Add("- No Master or Slave messages will be listed for Automatic Reads.");
					ContextLabelComm.Items.Add("- Automatic Reads with very fast poll interval will increase the CPU usage.");
					ContextLabelComm.Items.Add("- Turning off Master and Slave messages should improve the overall performance.");
					ContextLabelComm.Items.Add("- Bit/Character Reading will have the Points box BackColor change to Yellow.");
					FlagContextLabelCommSet = true;
				}

				ContextLabelComm.Show(this, e.Location);
			}
		}

		private void ButtonOpenRTU_Click(object sender, EventArgs e)
		{
			m_Abort = false;

			SerPort = new SerialPort();

			if (!string.IsNullOrWhiteSpace(tbManualCOM.Text))
				SerPort.PortName = tbManualCOM.Text;
			else
				SerPort.PortName = cbPort.SelectedItem.ToString();

			SerPort.BaudRate = Convert.ToInt32(cbBaud.SelectedItem.ToString());
			SerPort.Parity = (Parity)cbParity.SelectedIndex;
			SerPort.DataBits = Convert.ToInt32(cbDataBits.SelectedItem.ToString());
			SerPort.StopBits = (StopBits)cbStopBits.SelectedIndex + 1;
			SerPort.ReadTimeout = 3000;
			SerPort.WriteTimeout = 3000;
			SerPort.Handshake = Handshake.None;
			SerPort.DtrEnable = true;
			SerPort.RtsEnable = true;
			try
			{
				SerPort.Open();
				
				if (m_ProtocolType == 0) //RTU
					MMaster = ModbusSerialMaster.CreateRtu(SerPort);
				else //ASCIIoverRTU
					MMaster = ModbusSerialMaster.CreateAscii(SerPort);

				MMaster.Transport.ReadTimeout = 3000;
				MMaster.Transport.WriteTimeout = 3000;

				Modbus.Device.ModbusMaster.ModbusMasterMessageReceived += ModbusMaster_MessageReceived;
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
				return;
			}

			chbAutoRead1.Enabled = true;
			chbAutoRead2.Enabled = true;
			btnOpenRTUASCII.Enabled = false;
			btnOpenRTUASCII.BackColor = Color.Gainsboro;
			btnCloseRTU.Enabled = true;
			btnCloseRTU.BackColor = Color.LightSteelBlue;
			btnRefresh.Enabled = false;
			btnRefresh.BackColor = Color.Gainsboro;

			cbPort.Enabled = false;
			cbBaud.Enabled = false;
			cbDataBits.Enabled = false;
			cbParity.Enabled = false;
			cbStopBits.Enabled = false;
			cbCommMode.Enabled = false;
			tbManualCOM.Enabled = false;

			lblMessage.Text = "Serial Port " + SerPort.PortName + " opened.";
		}

		private void ButtonOpenTCPUDP_Click(object sender, EventArgs e)
		{
			m_Abort = false;

			chbAutoRead1.Enabled = true;
			chbAutoRead2.Enabled = true;

			btnOpenTCPUDP.Enabled = false;
			btnOpenTCPUDP.BackColor = Color.Gainsboro;
			btnCloseTCPUDP.Enabled = true;
			btnCloseTCPUDP.BackColor = Color.LightSteelBlue;

			tbIP.Enabled = false;
			tbPort.Enabled = false;
			cbCommMode.Enabled = false;

			Connect();
		}

		private void ButtonCloseRTU_Click(object sender, EventArgs e)
		{
			m_Abort = true;

			if (chbAutoRead1.Checked)
				chbAutoRead1.Checked = false;

			chbAutoRead1.Enabled = false;

			if (chbAutoRead2.Checked)
				chbAutoRead2.Checked = false;

			chbAutoRead2.Enabled = false;

			lblMessage.Text = "Connection closed.";

			if (SerPort != null)
			{
				if (SerPort.IsOpen)
				{
					SerPort.DiscardInBuffer();
					SerPort.DiscardOutBuffer();
					SerPort.Close();
				}

				SerPort.Dispose();
			}

			if (MMaster != null)
			{
				MMaster.Dispose();
				MMaster = null;
			}

			btnOpenRTUASCII.Enabled = true;
			btnOpenRTUASCII.BackColor = Color.LightSteelBlue;
			btnCloseRTU.Enabled = false;
			btnCloseRTU.BackColor = Color.Gainsboro;
			btnRefresh.Enabled = true;
			btnRefresh.BackColor = Color.LightSteelBlue;

			cbPort.Enabled = true;
			cbBaud.Enabled = true;
			cbDataBits.Enabled = true;
			cbParity.Enabled = true;
			cbStopBits.Enabled = true;
			cbCommMode.Enabled = true;
			tbManualCOM.Enabled = true;
		}

		private void ButtonCloseTCPUDP_Click(object sender, EventArgs e)
		{
			m_Abort = true;

			if (chbAutoRead1.Checked)
				chbAutoRead1.Checked = false;

			chbAutoRead1.Enabled = false;

			if (chbAutoRead2.Checked)
				chbAutoRead2.Checked = false;

			chbAutoRead2.Enabled = false;

			lblMessage.Text = "Connection closed";

			if (MMaster != null)
			{
				MMaster.Dispose();
				MMaster = null;
			}

			btnOpenTCPUDP.Enabled = true;
			btnOpenTCPUDP.BackColor = Color.LightSteelBlue;
			btnCloseTCPUDP.Enabled = false;
			btnCloseTCPUDP.BackColor = Color.Gainsboro;

			tbIP.Enabled = true;
			tbPort.Enabled = true;
			cbCommMode.Enabled = true;
		}

		private void ButtonRefresh_Click(object sender, EventArgs e)
		{
			GetSerialPorts();
		}

		private void ComboBoxSwapWords64bit_SelectedIndexChanged(object sender, EventArgs e)
		{
			m_cbSwapWords64bitSelectedIndex = cbSwapWords64bit.SelectedIndex;
		}

		private void ComboBoxSwapWords128bit_SelectedIndexChanged(object sender, EventArgs e)
		{
			m_cbSwapWords128bitSelectedIndex = cbSwapWords128bit.SelectedIndex;
		}

		private void ComboBoxCommMode_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (cbCommMode.SelectedIndex == 0 || cbCommMode.SelectedIndex == 5) //RTU or ASCIIoverRTU
			{
				gbRTU.Enabled = true;
				gbRTU.BringToFront();
				gbTCP.Enabled = false;
				gbTCP.SendToBack();
			}
			else //TCP or RTUoverTCP or ASCIIoverTCP or UDP or RTUoverUDP or ASCIIoverUDP
			{
				gbTCP.Enabled = true;
				gbTCP.BringToFront();
				gbRTU.Enabled = false;
				gbRTU.SendToBack();
			}
			m_ProtocolType = cbCommMode.SelectedIndex;
		}

		private void ListBox_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				((ListBox)sender).ContextMenuStrip = ContextListBox;

				if (!FlagContextListBoxSet)
				{
					ContextListBox.Items.Add("Clear Messages");
					FlagContextListBoxSet = true;
				}

				foreach (ToolStripItem item in ContextListBox.Items)
				{
					item.Name = ((ListBox)sender).Name;
				}

				ContextListBox.Show(this, e.Location);
			}
		}

		private void ContextListBox_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{
			if (((ContextMenuStrip)sender).Items[0].Name == "ListBox1")
				ListBox1.Items.Clear();
			else
				ListBox2.Items.Clear();
		}

		private void ListBox1_DoubleClick(object sender, EventArgs e)
		{
			DialogResult dr = MessageBox.Show("Save logged messages to a log file?", "Save Log File", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);

			if (dr == DialogResult.Yes)
			{
				using (System.IO.StreamWriter SW = new System.IO.StreamWriter("ModbusMasterRequestsLog.txt", true))
				{
					foreach (string item in ListBox1.Items)
					{
						SW.WriteLine(item);
					}
				}

				MessageBox.Show("Modbus Master requests saved to ModbusMasterRequestsLog.txt file in the application folder.");
			}
		}

		private void ListBox2_DoubleClick(object sender, EventArgs e)
		{
			DialogResult dr = MessageBox.Show("Save logged messages to a log file?", "Save Log File", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);

			if (dr == DialogResult.Yes)
			{
				using (System.IO.StreamWriter SW = new System.IO.StreamWriter("ModbusSlaveResponsesLog.txt", true))
				{
					foreach (string item in ListBox2.Items)
					{
						SW.WriteLine(item);
					}
				}

				MessageBox.Show("Modbus Slave responses saved to ModbusSlaveResponsesLog.txt file in the application folder.");
			}
		}

		private void CheckBox1_CheckedChanged(object sender, EventArgs e)
		{
			if (CheckBox1.Checked)
			{
				EnableMasterMessages = true;
				ListBox1.Show();
			}
			else
			{
				EnableMasterMessages = false;
				ListBox1.Hide();
			}
		}

		private void CheckBox2_CheckedChanged(object sender, EventArgs e)
		{
			if (CheckBox2.Checked)
			{
				EnableSlaveMessages = true;
				ListBox2.Show();
			}
			else
			{
				EnableSlaveMessages = false;
				ListBox2.Hide();
			}
		}

		private void GetSerialPorts()
		{
			string[] portnames = SerialPort.GetPortNames();
			cbPort.Items.Clear();
			if (portnames.Length == 0)
			{
				cbPort.Items.Add("none found");
				btnOpenRTUASCII.Enabled = false;
				btnOpenRTUASCII.BackColor = Color.Gainsboro;
			}
			else
			{
				foreach (string sPort in portnames)
				{
					cbPort.Items.Add(sPort);
				}

				btnOpenRTUASCII.Enabled = true;
			}

			cbPort.Sorted = true;
			cbPort.SelectedIndex = 0;
		}

		private void TextBoxManualCOM_TextChanged(object sender, EventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(tbManualCOM.Text))
			{
				if (!btnOpenRTUASCII.Enabled)
					btnOpenRTUASCII.Enabled = true;

				cbPort.Enabled = false;
			}
			else
			{
				if (cbPort.SelectedItem.ToString() == "none found")
					btnOpenRTUASCII.Enabled = false;

				cbPort.Enabled = true;
			}
		}

		private void Connect()
		{
			if (!DesignMode)
			{
				if (ConnectionBckgndThread == null)
				{
					ConnectionBckgndThread = new Thread(ConnectionBckgndThreadTask) { IsBackground = true };
					ConnectionBckgndThread.Start();
					Modbus.Device.ModbusMaster.ModbusMasterMessageReceived += ModbusMaster_MessageReceived;
				}
			}
		}

		//Try (re)connecting
		private void ConnectionBckgndThreadTask()
		{
			lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "Trying to connect ..."; });

			EventWaitHandle ConnectHoldRelease = new EventWaitHandle(false, EventResetMode.AutoReset);
			object obj = null;

			while (obj == null)
			{
				try
				{
					if (m_Abort)
					{
						lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "Connection closed"; });
						break;
					}

					obj = TCPUDPConnection.Connect(tbIP.Text, tbPort.Text, m_ProtocolType);

					if (obj != null)
					{
						if (m_ProtocolType == 1) //TCP
							MMaster = ModbusIpMaster.CreateIp((TcpClient)obj);
						else if (m_ProtocolType == 3) //RTUoverTCP
							MMaster = ModbusSerialMaster.CreateRtu((TcpClient)obj);
						else if (m_ProtocolType == 6) //ASCIIoverTCP
							MMaster = ModbusSerialMaster.CreateAscii((TcpClient)obj);
						else if (m_ProtocolType == 2) //UDP
							MMaster = ModbusIpMaster.CreateIp((UdpClient)obj);
						else if (m_ProtocolType == 4) //RTUoverUDP
							MMaster = ModbusSerialMaster.CreateRtu((UdpClient)obj);
						else //ASCIIoverUDP
							MMaster = ModbusSerialMaster.CreateAscii((UdpClient)obj);

						MMaster.Transport.ReadTimeout = 1500;
						MMaster.Transport.WriteTimeout = 1500;

						lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "Connection established"; });

						break;
					}
				}
				catch (Exception ex)
				{
					lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = ex.Message; });
				}

				ConnectHoldRelease.WaitOne(2000);
			}

			ConnectionBckgndThread = null;
		}

		private void ModbusMaster_MessageReceived(object sender, Modbus.ModbusMessagesEventArgs e)
		{
			if (m_ProtocolType == 0 || m_ProtocolType == 5) //RTU or ASCIIoverRTU
				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = e.Message; });
			else
			{
				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = e.Message; });

				if (ConnectionBckgndThread == null)
					Connect();
			}
		}

		private void MessageReceived(object sender, CommMessageEventArgs e)
		{
			lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = e.Message; });
		}

		#endregion

		#region "Master / Slave Methods"

		//Radio buttons to clear the address
		private void RadioButtonReset_Click(object sender, EventArgs e)
		{
			RadioButton sndr = (RadioButton)sender;
			int sndrIndex = Convert.ToInt32(sndr.Name.Substring(sndr.Name.Length - 1));
			AddressList[sndrIndex].PlcAddress.Text = "";
			AddressList[sndrIndex].CheckBoxRead.Checked = false;
			AddressList[sndrIndex].CheckBoxRead.Enabled = false;
			AddressList[sndrIndex].CheckBoxWrite.Checked = false;
			AddressList[sndrIndex].CheckBoxWrite.Enabled = false;
			AddressList[sndrIndex].NumberOfElements.Text = "1";
			AddressList[sndrIndex].NumberOfElements.Enabled = false;
			AddressList[sndrIndex].NumberOfElements.BackColor = Color.White;
			AddressList[sndrIndex].ValuesToWrite.Text = "0";
			AddressList[sndrIndex].ValuesToWrite.ReadOnly = true;
			AddressList[sndrIndex].ButtonSend.BackColor = Color.Gainsboro;
			AddressList[sndrIndex].ButtonSend.Enabled = false;
			AddressList[sndrIndex].LabelStatus.BackColor = Color.White;
		}

		private void TextBoxAddress_MouseClick(object sender, MouseEventArgs e)
		{
			MyForms.Form2.Txt = ((TextBox)sender).Text;
			DialogResult dg = MyForms.Form2.ShowDialog();
			if (dg == DialogResult.OK)
				((TextBox)sender).Text = MyForms.Form2.ResultingText.Trim();
		}

		private void TextBoxAddress_TextChanged(object sender, EventArgs e)
		{
			TextBox sndr = (TextBox)sender;
			int sndrIndex = Convert.ToInt32(sndr.Name.Substring(sndr.Name.Length - 1));

			if (!string.IsNullOrWhiteSpace(sndr.Text))
			{
				//Enable/Disable corresponding controls
				if (sndr.Text.StartsWith("0") || sndr.Text.StartsWith("4")) //The Address starts with "0" or "4"
				{
					AddressList[sndrIndex].CheckBoxRead.Enabled = true;
					AddressList[sndrIndex].CheckBoxRead.Checked = true;
					AddressList[sndrIndex].CheckBoxWrite.Enabled = true;
					AddressList[sndrIndex].CheckBoxWrite.Checked = false;
					AddressList[sndrIndex].NumberOfElements.Enabled = true;
					AddressList[sndrIndex].NumberOfElements.Text = "1";
				}
				else //The Address starts with "1" or "3"
				{
					AddressList[sndrIndex].CheckBoxRead.Enabled = true;
					AddressList[sndrIndex].CheckBoxRead.Checked = true;
					AddressList[sndrIndex].CheckBoxWrite.Enabled = false;
				}

				if (AddressList[sndrIndex].PlcAddress.Text.IndexOfAny(new char[] { '.' }) != -1)
					AddressList[sndrIndex].NumberOfElements.BackColor = Color.Yellow;
				else
					AddressList[sndrIndex].NumberOfElements.BackColor = Color.White;

				AddressList[sndrIndex].ValuesToWrite.Text = "0";
			}
		}

		private void CheckBoxRead_EnabledChanged(object sender, EventArgs e)
		{
			CheckBox sndr = (CheckBox)sender;

			if (sndr.Enabled)
				sndr.Visible = true;
			else
				sndr.Visible = false;
		}

		private void CheckBoxRead_Click(object sender, EventArgs e)
		{
			CheckBox sndr = (CheckBox)sender;
			int sndrIndex = Convert.ToInt32(sndr.Name.Substring(sndr.Name.Length - 1));

			if (!sndr.Checked)
			{
				sndr.Checked = true;
				AddressList[sndrIndex].LabelStatus.BackColor = Color.White;
			}
		}

		private void CheckBoxRead_CheckedChanged(object sender, EventArgs e)
		{
			CheckBox sndr = (CheckBox)sender;
			int sndrIndex = Convert.ToInt32(sndr.Name.Substring(sndr.Name.Length - 1));

			if (sndr.Checked)
			{
				AddressList[sndrIndex].CheckBoxWrite.Checked = false;
				AddressList[sndrIndex].NumberOfElements.Enabled = true;
				if (AddressList[sndrIndex].PlcAddress.Text.IndexOfAny(new char[] { '.' }) != -1)
					AddressList[sndrIndex].NumberOfElements.BackColor = Color.Yellow;
				else
					AddressList[sndrIndex].NumberOfElements.BackColor = Color.White;
				AddressList[sndrIndex].ValuesToWrite.Text = "0";
				AddressList[sndrIndex].ValuesToWrite.ReadOnly = true;
				AddressList[sndrIndex].ButtonSend.Enabled = true;
				AddressList[sndrIndex].ButtonSend.BackColor = Color.LightSteelBlue;
			}
			else
			{
				if (!AddressList[sndrIndex].CheckBoxWrite.Checked)
				{
					AddressList[sndrIndex].ButtonSend.BackColor = Color.Gainsboro;
					AddressList[sndrIndex].ButtonSend.Enabled = false;
				}
			}
		}

		private void CheckBoxWrite_EnabledChanged(object sender, EventArgs e)
		{
			CheckBox sndr = (CheckBox)sender;
			if (sndr.Enabled)
				sndr.Visible = true;
			else
				sndr.Visible = false;
		}

		private void CheckBoxWrite_Click(object sender, EventArgs e)
		{
			CheckBox sndr = (CheckBox)sender;
			int sndrIndex = Convert.ToInt32(sndr.Name.Substring(sndr.Name.Length - 1));

			if (!sndr.Checked)
			{
				sndr.Checked = true;
				AddressList[sndrIndex].LabelStatus.BackColor = Color.White;
			}
		}

		private void CheckBoxWrite_CheckedChanged(object sender, EventArgs e)
		{
			CheckBox sndr = (CheckBox)sender;
			int sndrIndex = Convert.ToInt32(sndr.Name.Substring(sndr.Name.Length - 1));

			if (sndr.Checked)
			{
				AddressList[sndrIndex].CheckBoxRead.Checked = false;
				AddressList[sndrIndex].NumberOfElements.Enabled = true;
				if (AddressList[sndrIndex].PlcAddress.Text.IndexOfAny(new char[] { '.' }) != -1)
					AddressList[sndrIndex].NumberOfElements.BackColor = Color.Yellow;
				else
					AddressList[sndrIndex].NumberOfElements.BackColor = Color.White;
				AddressList[sndrIndex].ValuesToWrite.Text = "0";
				AddressList[sndrIndex].ValuesToWrite.ReadOnly = false;
				AddressList[sndrIndex].ButtonSend.Enabled = true;
				AddressList[sndrIndex].ButtonSend.BackColor = Color.LightSteelBlue;
			}
		}

		private void CheckBoxSwapWords_CheckedChanged(object sender, EventArgs e)
		{
			if (chbSwapWords.Checked)
			{
				cbSwapWords64bit.Enabled = true;
				cbSwapWords128bit.Enabled = true;
			}
			else
			{
				cbSwapWords64bit.Enabled = false;
				cbSwapWords128bit.Enabled = false;
			}
		}

		private void TextBoxNofE_LostFocus(object sender, EventArgs e)
		{
			TextBox sndr = (TextBox)sender;
			if (int.TryParse(sndr.Text, out _))
			{
				if (Convert.ToInt32(sndr.Text) < 1)
				{
					MessageBox.Show("The number of elements has to be Integer number greater or equal to 1!");
					sndr.Text = "1";
					return;
				}
			}
			else
			{
				MessageBox.Show("The number of elements has to be Integer number greater or equal to 1!");
				sndr.Text = "1";
				return;
			}
		}

		private void TextBoxVtoW_Click(object sender, EventArgs e)
		{
			((TextBox)sender).SelectAll(); //Select current text
		}

		private void ButtonSend_Click(object sender, EventArgs e)
		{
			Button sndr = (Button)sender;
			int sndrIndex = Convert.ToInt32(sndr.Name.Substring(sndr.Name.Length - 1));

			if (MMaster == null)
			{
				MessageBox.Show("Make sure the connection is established and with a correct Protocol Type.");
				lblMessage.Text = String.Format(System.Globalization.CultureInfo.InvariantCulture, "Make sure the connection is established and with a correct Protocol Type");
				AddressList[sndrIndex].LabelStatus.BackColor = Color.Red;

				return;
			}

			//Set message parameters by checking the corresponding controls
			byte slaveID = Convert.ToByte(AddressList[sndrIndex].SlaveID.SelectedItem.ToString());
			var numOfPoints = Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text);
			string startAddress = AddressList[sndrIndex].PlcAddress.Text;

			int BitNumber = -1;
			var tempAddress = startAddress;
			if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
			{
				if (tempAddress.IndexOfAny(new char[] { 'F' }) != -1)
				{
					BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1, (tempAddress.IndexOf("F") - tempAddress.IndexOf(".") - 1)));
					startAddress = tempAddress.Substring(0, tempAddress.IndexOf(".")) + tempAddress.Substring(tempAddress.IndexOf("F"));
				}
				else if (tempAddress.IndexOfAny(new char[] { 'U' }) != -1)
				{
					BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1, (tempAddress.IndexOf("U") - tempAddress.IndexOf(".") - 1)));
					startAddress = tempAddress.Substring(0, tempAddress.IndexOf(".")) + tempAddress.Substring(tempAddress.IndexOf("U"));
				}
				else if (tempAddress.IndexOfAny(new char[] { 'L' }) != -1)
				{
					BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1, (tempAddress.IndexOf("L") - tempAddress.IndexOf(".") - 1)));
					startAddress = tempAddress.Substring(0, tempAddress.IndexOf(".")) + tempAddress.Substring(tempAddress.IndexOf("L"));
				}
				else if (tempAddress.IndexOfAny(new char[] { 'S' }) != -1)
				{
					BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1)) - 1;
					startAddress = tempAddress.Substring(0, tempAddress.IndexOf("."));
				}
				else
				{
					BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1));
					startAddress = tempAddress.Substring(0, tempAddress.IndexOf("."));
				}
			}

			if (!AddressCheckPassed(tempAddress, BitNumber, Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text), sndrIndex))
				return;

			string functionCode = null;
			if (AddressList[sndrIndex].CheckBoxRead.Checked)
				functionCode = "Read";
			else
				functionCode = "Write";

			bool[] responsesBoolean = null;
			ushort[] responsesUShort = null;
			float[] floatValue = null;
			double[] dfloatValue = null;

			//Perform Read/Write operation(s) applicable to selected address

			if (startAddress.StartsWith("0") || startAddress.StartsWith("1")) //Applicable operations: "0" = Read/Write, "1" = Read only
			{
				if ((Convert.ToInt32(startAddress.Substring(1)) + numOfPoints) > 65535)
				{
					AddressList[sndrIndex].ValuesToWrite.Text = "Limits of the array exceeded";
					return;
				}
				if (functionCode == "Read")
				{
					try
					{
						//Set the corresponding status label back color to Yellow = Processing
						AddressList[sndrIndex].LabelStatus.BackColor = Color.Yellow;

						if (startAddress.StartsWith("0"))
						{
							if (EnableMasterMessages)
								MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} coils starting at address {2}.", slaveID, numOfPoints, Convert.ToInt32(startAddress.Substring(1))));

							responsesBoolean = MMaster.ReadCoils(slaveID, Convert.ToUInt16(startAddress.Substring(1)), Convert.ToUInt16(numOfPoints));
						}
						else
						{
							if (EnableMasterMessages)
								MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} inputs starting at address {2}.", slaveID, numOfPoints, Convert.ToInt32(startAddress.Substring(1))));

							responsesBoolean = MMaster.ReadInputs(slaveID, Convert.ToUInt16(startAddress.Substring(1)), Convert.ToUInt16(numOfPoints));
						}

						if (responsesBoolean.Length > 0)
						{
							for (int i = 0; i < responsesBoolean.Length; i++)
							{
								StrMessage += responsesBoolean[i];

								if (i != responsesBoolean.Length - 1)
									StrMessage += ", ";
							}
						}

						if (EnableSlaveMessages)
							SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, "{ " + StrMessage + " }"));

						AddressList[sndrIndex].ValuesToWrite.Text = "{ " + StrMessage + " }";
						StrMessage = "";

						//Set the corresponding status label back color to Green = Success
						AddressList[sndrIndex].LabelStatus.BackColor = Color.Green;
					}
					catch (Exception ex)
					{
						ExceptionsAndActions(ex);
						AddressList[sndrIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[sndrIndex].ValuesToWrite.Text = "No Response or Error"; });
						//Set the corresponding status label back color to Red = Failed
						AddressList[sndrIndex].LabelStatus.BackColor = Color.Red;
						return;
					}
				}
				else // FunctionCode = "Write"
				{
					bool[] valBool = null;
					//Split string based on comma delimiter
					var val = AddressList[sndrIndex].ValuesToWrite.Text.Split(new char[] { ',' });

					if (val.Length > 1 && val.Length != Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text))
					{
						MessageBox.Show("The number of provided values to write does not match the number of points!" + Environment.NewLine + "For multiple targets, either enter a single value or the exact number of comma separated values.");
						sndr.Text = "0";
						return;
					}
					else if (val.Length == 1 && Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text) > 1)
					{
						valBool = new bool[Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text)];

						for (int i = 0; i < Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text); i++)
						{
							if (val[0] == "0")
								valBool[i] = false;
							else if (val[0] == "1")
								valBool[i] = true;
							else
								valBool[i] = Convert.ToBoolean(val[0].Trim());
						}
					}
					else
					{
						valBool = new bool[val.Length];

						for (int i = 0; i < val.Length; i++)
						{
							if (val[i] == "0")
								valBool[i] = false;
							else if (val[i] == "1")
								valBool[i] = true;
							else
								valBool[i] = Convert.ToBoolean(val[i].Trim());
						}
					}
					try
					{
						//Set the corresponding status label back color to Yellow = Processing
						AddressList[sndrIndex].LabelStatus.BackColor = Color.Yellow;

						if (numOfPoints > 1)
						{
							if (EnableMasterMessages)
								MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} coils starting at address {2}.", slaveID, numOfPoints, Convert.ToInt32(startAddress.Substring(1))));

							MMaster.WriteMultipleCoils(slaveID, Convert.ToUInt16(startAddress), valBool);
						}
						else
						{
							if (EnableMasterMessages)
								MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write single coil {1} starting at address {2}.", slaveID, val[0], Convert.ToInt32(startAddress.Substring(1))));

							MMaster.WriteSingleCoil(slaveID, Convert.ToUInt16(startAddress), valBool[0]);
						}

						if (EnableSlaveMessages)
							SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

						//Set the corresponding status label back color to Green = Success
						AddressList[sndrIndex].LabelStatus.BackColor = Color.Green;
					}
					catch (Exception ex)
					{
						ExceptionsAndActions(ex);

						//Set the corresponding status label back color to Red = Failed
						AddressList[sndrIndex].LabelStatus.BackColor = Color.Red;
						return;
					}
				}
			}
			else if (startAddress.StartsWith("3") || startAddress.StartsWith("4")) //Applicable operations: "3" = Read only, "4" = Read/Write
			{
				if (functionCode == "Read")
				{
					try
					{
						//Set the corresponding status label back color to Yellow = Processing
						AddressList[sndrIndex].LabelStatus.BackColor = Color.Yellow;

						if (tempAddress.IndexOfAny(new char[] { 'O' }) != -1) // *** Process 128-bit Addresses ***
						{
							if (tempAddress.IndexOfAny(new char[] { 'L' }) != -1) //LO
							{
								string pollAddress = null;
								pollAddress = startAddress.Substring(1, startAddress.IndexOf("L") - 1);
								if (startAddress.StartsWith("3"))
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (Int128 bits).", slaveID, 8, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), 8);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (Int128 bits).", slaveID, 8 * numOfPoints, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(8 * numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (Int128).", slaveID, 8 * numOfPoints, Convert.ToInt32(pollAddress)));

										responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(8 * numOfPoints));
									}
								}
								else
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (Int128 bits).", slaveID, 8, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), 8);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (Int128 bits).", slaveID, 8 * numOfPoints, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(8 * numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (Int128).", slaveID, 8 * numOfPoints, Convert.ToInt32(pollAddress)));

										responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(8 * numOfPoints));
									}
								}

								if (responsesUShort.Length > 0)
								{
									if (!chbSwapBytes.Checked)
										responsesUShort = SwapBytesFunction(responsesUShort);

									if (chbSwapWords.Checked)
										responsesUShort = SwapWords128Function(responsesUShort);

									byte[] valueBytes = new byte[16];

									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											for (int j = 0; j < 8; j++)
											{
												var bytes = BitConverter.GetBytes(responsesUShort[j]);
												valueBytes[j * 2] = bytes[0];
												valueBytes[j * 2 + 1] = bytes[1];
											}

											string binaryString = "";

											for (int k = 15; k > -1; k -= 1)
											{
												binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
											}

											for (int i = 0; i < numOfPoints; i++)
											{
												StrMessage += ExtractInt128Bit(binaryString, BitNumber + i);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
										}
										else
										{
											for (int i = 0; i < numOfPoints; i++)
											{
												for (int j = 0; j < 8; j++)
												{
													var bytes = BitConverter.GetBytes(responsesUShort[i * 8 + j]);
													valueBytes[j * 2] = bytes[0];
													valueBytes[j * 2 + 1] = bytes[1];
												}

												string binaryString = "";

												for (int k = 15; k > -1; k -= 1)
												{
													binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
												}

												StrMessage += ExtractInt128Bit(binaryString, BitNumber);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
										}
									}
									else
									{
										for (int i = 0; i < numOfPoints; i++)
										{
											for (int j = 0; j < 8; j++)
											{
												var bytes = BitConverter.GetBytes(responsesUShort[i * 8 + j]);
												valueBytes[j * 2] = bytes[0];
												valueBytes[j * 2 + 1] = bytes[1];
											}

											string binaryString = "";

											for (int k = 15; k > -1; k -= 1)
											{
												binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
											}

											StrMessage += BitConverterInt128(binaryString).ToString();

											if (i != numOfPoints - 1)
												StrMessage += ", ";
										}
									}
								}
							}
							else //UO
							{
								string pollAddress = null;
								pollAddress = startAddress.Substring(1, startAddress.IndexOf("U") - 1);
								if (startAddress.StartsWith("3"))
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (UInt128 bits).", slaveID, 8, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), 8);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (UInt128 bits).", slaveID, 8 * numOfPoints, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(8 * numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (UInt128).", slaveID, 8 * numOfPoints, Convert.ToInt32(pollAddress)));

										responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(8 * numOfPoints));
									}
								}
								else
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (UInt128 bits).", slaveID, 8, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), 8);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (UInt128 bits).", slaveID, 8 * numOfPoints, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(8 * numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (UInt128).", slaveID, 8 * numOfPoints, Convert.ToInt32(pollAddress)));

										responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(8 * numOfPoints));
									}
								}

								if (responsesUShort.Length > 0)
								{
									if (!chbSwapBytes.Checked)
										responsesUShort = SwapBytesFunction(responsesUShort);

									if (chbSwapWords.Checked)
										responsesUShort = SwapWords128Function(responsesUShort);

									byte[] valueBytes = new byte[16];

									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											for (int j = 0; j < 8; j++)
											{
												var bytes = BitConverter.GetBytes(responsesUShort[j]);
												valueBytes[j * 2] = bytes[0];
												valueBytes[j * 2 + 1] = bytes[1];
											}

											string binaryString = "";

											for (int k = 15; k > -1; k -= 1)
											{
												binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
											}

											for (int i = 0; i < numOfPoints; i++)
											{
												StrMessage += ExtractInt128Bit(binaryString, BitNumber + i);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
										}
										else
										{
											for (int i = 0; i < numOfPoints; i++)
											{
												for (int j = 0; j < 8; j++)
												{
													var bytes = BitConverter.GetBytes(responsesUShort[i * 8 + j]);
													valueBytes[j * 2] = bytes[0];
													valueBytes[j * 2 + 1] = bytes[1];
												}

												string binaryString = "";

												for (int k = 15; k > -1; k -= 1)
												{
													binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
												}

												StrMessage += ExtractInt128Bit(binaryString, BitNumber);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
										}
									}
									else
									{
										for (int i = 0; i < numOfPoints; i++)
										{
											for (int j = 0; j < 8; j++)
											{
												var bytes = BitConverter.GetBytes(responsesUShort[i * 8 + j]);
												valueBytes[j * 2] = bytes[0];
												valueBytes[j * 2 + 1] = bytes[1];
											}

											string binaryString = "";

											for (int k = 15; k > -1; k -= 1)
											{
												binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
											}

											StrMessage += BitConverterUInt128(binaryString).ToString();

											if (i != numOfPoints - 1)
												StrMessage += ", ";
										}
									}
								}
							}
						}
						else if (tempAddress.IndexOfAny(new char[] { 'Q' }) != -1) // *** Process 64-bit Addresses ***
						{
							if (tempAddress.IndexOfAny(new char[] { 'F' }) != -1) //FQ
							{
								if (chbBitReading.Checked && tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									dfloatValue = new double[1];
								else
									dfloatValue = new double[Convert.ToInt32(numOfPoints)];

								var pollAddress = startAddress.Substring(1, startAddress.IndexOf("F") - 1);

								if (startAddress.StartsWith("3"))
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (float64 bits).", slaveID, 4, Convert.ToInt32(pollAddress)));

											dfloatValue = MMaster.ReadFloat64InputRegisters(slaveID, Convert.ToUInt16(pollAddress), 1, chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (float64 bits).", slaveID, 4 * numOfPoints, Convert.ToInt32(pollAddress)));

											dfloatValue = MMaster.ReadFloat64InputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (float64).", slaveID, 4 * numOfPoints, Convert.ToInt32(pollAddress)));

										dfloatValue = MMaster.ReadFloat64InputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));
									}
								}
								else
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (float64 bits).", slaveID, 4, Convert.ToInt32(pollAddress)));

											dfloatValue = MMaster.ReadFloat64HoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), 1, chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (float64 bits).", slaveID, 4 * numOfPoints, Convert.ToInt32(pollAddress)));

											dfloatValue = MMaster.ReadFloat64HoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (float64).", slaveID, 4 * numOfPoints, Convert.ToInt32(pollAddress)));

										dfloatValue = MMaster.ReadFloat64HoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));
									}
								}

								if (dfloatValue.Length > 0)
								{
									for (int i = 0; i < numOfPoints; i++)
									{
										if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
										{
											if (chbBitReading.Checked)
												StrMessage += ExtractInt64Bit(BitConverter.ToInt64(BitConverter.GetBytes(dfloatValue[0]), 0), BitNumber + i);
											else
												StrMessage += ExtractInt64Bit(BitConverter.ToInt64(BitConverter.GetBytes(dfloatValue[i]), 0), BitNumber);

											if (i != numOfPoints - 1)
												StrMessage += ", ";
										}
										else
										{
											StrMessage += dfloatValue[i];

											if (i != numOfPoints - 1)
												StrMessage += ", ";
										}
									}
								}
							}
							else if (tempAddress.IndexOfAny(new char[] { 'L' }) != -1) //LQ
							{
								string pollAddress = null;
								pollAddress = startAddress.Substring(1, startAddress.IndexOf("L") - 1);
								if (startAddress.StartsWith("3"))
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (integer64 bits).", slaveID, 4, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), 4);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (integer64 bits).", slaveID, 4 * numOfPoints, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(4 * numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (integer64).", slaveID, 4 * numOfPoints, Convert.ToInt32(pollAddress)));

										responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(4 * numOfPoints));
									}
								}
								else
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (integer64 bits).", slaveID, 4, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), 4);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (integer64 bits).", slaveID, 4 * numOfPoints, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(4 * numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (integer64).", slaveID, 4 * numOfPoints, Convert.ToInt32(pollAddress)));

										responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(4 * numOfPoints));
									}
								}

								if (responsesUShort.Length > 0)
								{
									if (!chbSwapBytes.Checked)
										responsesUShort = SwapBytesFunction(responsesUShort);

									if (chbSwapWords.Checked)
										responsesUShort = SwapWords64Function(responsesUShort);

									byte[] valueBytes = new byte[8];

									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											for (int j = 0; j < 4; j++)
											{
												var bytes = BitConverter.GetBytes(responsesUShort[j]);
												valueBytes[j * 2] = bytes[0];
												valueBytes[j * 2 + 1] = bytes[1];
											}

											for (int i = 0; i < numOfPoints; i++)
											{
												StrMessage += ExtractInt64Bit(BitConverter.ToInt64(valueBytes, 0), BitNumber + i);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
										}
										else
										{
											for (int i = 0; i < numOfPoints; i++)
											{
												for (int j = 0; j < 4; j++)
												{
													var bytes = BitConverter.GetBytes(responsesUShort[i * 4 + j]);
													valueBytes[j * 2] = bytes[0];
													valueBytes[j * 2 + 1] = bytes[1];
												}

												StrMessage += ExtractInt64Bit(BitConverter.ToInt64(valueBytes, 0), BitNumber);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
										}
									}
									else
									{
										for (int i = 0; i < numOfPoints; i++)
										{
											for (int j = 0; j < 4; j++)
											{
												var bytes = BitConverter.GetBytes(responsesUShort[i * 4 + j]);
												valueBytes[j * 2] = bytes[0];
												valueBytes[j * 2 + 1] = bytes[1];
											}

											StrMessage += BitConverter.ToInt64(valueBytes, 0).ToString();

											if (i != numOfPoints - 1)
												StrMessage += ", ";
										}
									}
								}
							}
							else //UQ
							{
								string pollAddress = null;
								pollAddress = startAddress.Substring(1, startAddress.IndexOf("U") - 1);
								if (startAddress.StartsWith("3"))
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (uinteger64 bits).", slaveID, 4, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), 4);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (uinteger64 bits).", slaveID, 4 * numOfPoints, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(4 * numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (uinteger64).", slaveID, 4 * numOfPoints, Convert.ToInt32(pollAddress)));

										responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(4 * numOfPoints));
									}
								}
								else
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (uinteger64 bits).", slaveID, 4, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), 4);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (uinteger64 bits).", slaveID, 4 * numOfPoints, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(4 * numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (uinteger64).", slaveID, 4 * numOfPoints, Convert.ToInt32(pollAddress)));

										responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(4 * numOfPoints));
									}
								}

								if (responsesUShort.Length > 0)
								{
									if (!chbSwapBytes.Checked)
										responsesUShort = SwapBytesFunction(responsesUShort);

									if (chbSwapWords.Checked)
										responsesUShort = SwapWords64Function(responsesUShort);

									byte[] valueBytes = new byte[8];

									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											for (int j = 0; j < 4; j++)
											{
												var bytes = BitConverter.GetBytes(responsesUShort[j]);
												valueBytes[j * 2] = bytes[0];
												valueBytes[j * 2 + 1] = bytes[1];
											}

											for (int i = 0; i < numOfPoints; i++)
											{
												StrMessage += ExtractInt64Bit(Convert.ToInt64(BitConverter.ToUInt64(valueBytes, 0)), BitNumber + i);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
										}
										else
										{
											for (int i = 0; i < numOfPoints; i++)
											{
												for (int j = 0; j < 4; j++)
												{
													var bytes = BitConverter.GetBytes(responsesUShort[i * 4 + j]);
													valueBytes[j * 2] = bytes[0];
													valueBytes[j * 2 + 1] = bytes[1];
												}

												StrMessage += ExtractInt64Bit(Convert.ToInt64(BitConverter.ToUInt64(valueBytes, 0)), BitNumber);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
										}
									}
									else
									{
										for (int i = 0; i < numOfPoints; i++)
										{
											for (int j = 0; j < 4; j++)
											{
												var bytes = BitConverter.GetBytes(responsesUShort[i * 4 + j]);
												valueBytes[j * 2] = bytes[0];
												valueBytes[j * 2 + 1] = bytes[1];
											}

											StrMessage += BitConverter.ToUInt64(valueBytes, 0).ToString();

											if (i != numOfPoints - 1)
												StrMessage += ", ";
										}
									}
								}
							}
						}
						else if (tempAddress.IndexOfAny(new char[] { 'S' }) != -1)
						{
							int numRegisters = 0;
							int registerAddress = Convert.ToInt32(tempAddress.Substring(1, tempAddress.IndexOf("S") - 1));
							if (startAddress.StartsWith("3"))
							{
								if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
								{
									numRegisters = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf("S") + 1, tempAddress.IndexOf(".") - tempAddress.IndexOf("S") - 1));
									if (chbBitReading.Checked)
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (string chars).", slaveID, numRegisters, registerAddress));

										responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numRegisters));
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (string chars).", slaveID, numOfPoints * numRegisters, registerAddress));

										responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints * numRegisters));
									}
								}
								else
								{
									numRegisters = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf("S") + 1));
									if (EnableMasterMessages)
										MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (string).", slaveID, numOfPoints * numRegisters, registerAddress));

									responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints * numRegisters));
								}
							}
							else
							{
								if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
								{
									numRegisters = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf("S") + 1, tempAddress.IndexOf(".") - tempAddress.IndexOf("S") - 1));
									if (chbBitReading.Checked)
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (string chars).", slaveID, numRegisters, registerAddress));

										responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numRegisters));
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (string chars).", slaveID, numOfPoints * numRegisters, registerAddress));

										responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints * numRegisters));
									}
								}
								else
								{
									numRegisters = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf("S") + 1));
									if (EnableMasterMessages)
										MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (string).", slaveID, numOfPoints * numRegisters, registerAddress));

									responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints * numRegisters));
								}
							}
							if (responsesUShort.Length > 0)
							{
								if (!chbSwapBytes.Checked)
									responsesUShort = SwapBytesFunction(responsesUShort);

								if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
								{
									var intValues = new string[1];

									for (int i = 0; i < numOfPoints; i++)
									{
										if (chbBitReading.Checked)
											intValues[0] = BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[BitNumber + i]), 0).ToString();
										else
											intValues[0] = BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[BitNumber + i * numRegisters]), 0).ToString();

										StrMessage += ConvertStringOfIntegersToString(intValues).ToString().Trim();

										if (i != numOfPoints - 1)
											StrMessage += ", ";
									}
								}
								else
								{
									for (int i = 0; i < numOfPoints; i++)
									{
										var intValues = new string[numRegisters];

										for (int j = 0; j < numRegisters; j++)
										{
											intValues[j] = BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[j + i * numRegisters]), 0).ToString();
										}

										StrMessage += ConvertStringOfIntegersToString(intValues).ToString().Trim();

										if (i != numOfPoints - 1)
											StrMessage += ", ";
									}
								}
							}
						}
						else if (tempAddress.IndexOfAny(new char[] { 'F' }) != -1)
						{
							if (chbBitReading.Checked && tempAddress.IndexOfAny(new char[] { '.' }) != -1)
								floatValue = new float[1];
							else
								floatValue = new float[Convert.ToInt32(numOfPoints)];

							var pollAddress = startAddress.Substring(1, startAddress.IndexOf("F") - 1);

							if (startAddress.StartsWith("3"))
							{
								if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
								{
									if (chbBitReading.Checked)
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (float bits).", slaveID, 2, Convert.ToInt32(pollAddress)));

										floatValue = MMaster.ReadFloat32InputRegisters(slaveID, Convert.ToUInt16(pollAddress), 1, chbSwapBytes.Checked, chbSwapWords.Checked);
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (float bits).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(pollAddress)));

										floatValue = MMaster.ReadFloat32InputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked);
									}
								}
								else
								{
									if (EnableMasterMessages)
										MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (float).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(pollAddress)));

									floatValue = MMaster.ReadFloat32InputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked);
								}
							}
							else
							{
								if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
								{
									if (chbBitReading.Checked)
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (float bits).", slaveID, 2, Convert.ToInt32(pollAddress)));

										floatValue = MMaster.ReadFloat32HoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), 1, chbSwapBytes.Checked, chbSwapWords.Checked);
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (float bits).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(pollAddress)));

										floatValue = MMaster.ReadFloat32HoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked);
									}
								}
								else
								{
									if (EnableMasterMessages)
										MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (float).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(pollAddress)));

									floatValue = MMaster.ReadFloat32HoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked);
								}
							}

							if (floatValue.Length > 0)
							{
								for (int i = 0; i < numOfPoints; i++)
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
											StrMessage += ExtractInt32Bit(BitConverter.ToInt32(BitConverter.GetBytes(floatValue[0]), 0), BitNumber + i);
										else
											StrMessage += ExtractInt32Bit(BitConverter.ToInt32(BitConverter.GetBytes(floatValue[i]), 0), BitNumber);

										if (i != numOfPoints - 1)
											StrMessage += ", ";
									}
									else
									{
										StrMessage += floatValue[i];

										if (i != numOfPoints - 1)
											StrMessage += ", ";
									}
								}
							}
						}
						else if (tempAddress.IndexOfAny(new char[] { 'L' }) != -1)
						{
							string pollAddress = null;
							if (tempAddress.IndexOfAny(new char[] { 'U' }) != -1)
							{
								pollAddress = startAddress.Substring(1, startAddress.IndexOf("U") - 1);

								if (startAddress.StartsWith("3"))
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (uinteger bits).", slaveID, 2, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), 2);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (uinteger bits).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(2 * numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (uinteger).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(pollAddress)));

										responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(2 * numOfPoints));
									}
								}
								else
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked && tempAddress.IndexOfAny(new char[] { '.' }) != -1)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (uinteger bits).", slaveID, 2, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), 2);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (uinteger bits).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(2 * numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (uinteger).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(pollAddress)));

										responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(2 * numOfPoints));
									}
								}
							}
							else
							{
								pollAddress = startAddress.Substring(1, startAddress.IndexOf("L") - 1);

								if (startAddress.StartsWith("3"))
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (integer bits).", slaveID, 2, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), 2);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (integer bits).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(2 * numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (integer).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(pollAddress)));

										responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(2 * numOfPoints));
									}
								}
								else
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (integer bits).", slaveID, 2, Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), 2);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (integer bits).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(pollAddress)));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(2 * numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (integer).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(pollAddress)));

										responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(pollAddress), Convert.ToUInt16(2 * numOfPoints));
									}
								}
							}

							if (responsesUShort.Length > 0)
							{
								if (!chbSwapBytes.Checked)
									responsesUShort = SwapBytesFunction(responsesUShort);

								if (chbSwapWords.Checked)
									responsesUShort = SwapWordsFunction(responsesUShort);

								byte[] valueBytes = new byte[4];

								if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
								{
									if (chbBitReading.Checked)
									{
										for (int j = 0; j < 2; j++)
										{
											var bytes = BitConverter.GetBytes(responsesUShort[j]);
											valueBytes[j * 2] = bytes[0];
											valueBytes[j * 2 + 1] = bytes[1];
										}

										for (int i = 0; i < numOfPoints; i++)
										{
											if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
											{
												StrMessage += ExtractInt32Bit(Convert.ToInt32(BitConverter.ToUInt32(valueBytes, 0)), BitNumber + i);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
											else
											{
												StrMessage += ExtractInt32Bit(BitConverter.ToInt32(valueBytes, 0), BitNumber + i);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
										}
									}
									else
									{
										for (int i = 0; i < numOfPoints; i++)
										{
											for (int j = 0; j < 2; j++)
											{
												var bytes = BitConverter.GetBytes(responsesUShort[i * 2 + j]);
												valueBytes[j * 2] = bytes[0];
												valueBytes[j * 2 + 1] = bytes[1];
											}

											if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
											{
												StrMessage += ExtractInt32Bit(Convert.ToInt32(BitConverter.ToUInt32(valueBytes, 0)), BitNumber);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
											else
											{
												StrMessage += ExtractInt32Bit(BitConverter.ToInt32(valueBytes, 0), BitNumber);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
										}
									}
								}
								else
								{
									for (int i = 0; i < numOfPoints; i++)
									{
										for (int j = 0; j < 2; j++)
										{
											var bytes = BitConverter.GetBytes(responsesUShort[i * 2 + j]);
											valueBytes[j * 2] = bytes[0];
											valueBytes[j * 2 + 1] = bytes[1];
										}

										if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
											StrMessage += BitConverter.ToUInt32(valueBytes, 0).ToString();
										else
											StrMessage += BitConverter.ToInt32(valueBytes, 0).ToString();

										if (i != numOfPoints - 1)
											StrMessage += ", ";
									}
								}
							}
						}
						else
						{
							if (startAddress.StartsWith("3"))
							{
								if (tempAddress.IndexOfAny(new char[] { 'U' }) != -1)
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (ushort bits).", slaveID, 1, Convert.ToInt32(startAddress.Substring(1, startAddress.Length - 2))));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.Length - 2)), 1);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (ushort bits).", slaveID, Convert.ToInt32(numOfPoints), Convert.ToInt32(startAddress.Substring(1, startAddress.Length - 2))));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.Length - 2)), Convert.ToUInt16(numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (ushort).", slaveID, Convert.ToInt32(numOfPoints), Convert.ToInt32(startAddress.Substring(1, startAddress.Length - 2))));

										responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.Length - 2)), Convert.ToUInt16(numOfPoints));
									}
								}
								else
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (short bits).", slaveID, 1, Convert.ToInt32(startAddress.Substring(1))));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1)), 1);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (short bits).", slaveID, Convert.ToInt32(numOfPoints), Convert.ToInt32(startAddress.Substring(1))));

											responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1)), Convert.ToUInt16(numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} input registers starting at address {2} (short).", slaveID, Convert.ToInt32(numOfPoints), Convert.ToInt32(startAddress.Substring(1))));

										responsesUShort = MMaster.ReadInputRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1)), Convert.ToUInt16(numOfPoints));
									}
								}
							}
							else
							{
								if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (ushort bits).", slaveID, 1, Convert.ToInt32(startAddress.Substring(1, startAddress.Length - 2))));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.Length - 2)), 1);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (ushort bits).", slaveID, Convert.ToInt32(numOfPoints), Convert.ToInt32(startAddress.Substring(1, startAddress.Length - 2))));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.Length - 2)), Convert.ToUInt16(numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (ushort).", slaveID, Convert.ToInt32(numOfPoints), Convert.ToInt32(startAddress.Substring(1, startAddress.Length - 2))));

										responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.Length - 2)), Convert.ToUInt16(numOfPoints));
									}
								}
								else
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (short bits).", slaveID, 1, Convert.ToInt32(startAddress.Substring(1))));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1)), 1);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (short bits).", slaveID, Convert.ToInt32(numOfPoints), Convert.ToInt32(startAddress.Substring(1))));

											responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1)), Convert.ToUInt16(numOfPoints));
										}
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Read {1} holding registers starting at address {2} (short).", slaveID, Convert.ToInt32(numOfPoints), Convert.ToInt32(startAddress.Substring(1))));

										responsesUShort = MMaster.ReadHoldingRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1)), Convert.ToUInt16(numOfPoints));
									}
								}
							}
							if (responsesUShort.Length > 0)
							{
								if (!chbSwapBytes.Checked)
									responsesUShort = SwapBytesFunction(responsesUShort);

								for (int i = 0; i < numOfPoints; i++)
								{
									if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
									{
										if (chbBitReading.Checked)
										{
											if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
											{
												StrMessage += ExtractInt32Bit(BitConverter.ToUInt16(BitConverter.GetBytes(responsesUShort[0]), 0), BitNumber + i);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
											else
											{
												StrMessage += ExtractInt32Bit(BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[0]), 0), BitNumber + i);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
										}
										else
										{
											if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
											{
												StrMessage += ExtractInt32Bit(BitConverter.ToUInt16(BitConverter.GetBytes(responsesUShort[i]), 0), BitNumber);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
											else
											{
												StrMessage += ExtractInt32Bit(BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[i]), 0), BitNumber);

												if (i != numOfPoints - 1)
													StrMessage += ", ";
											}
										}
									}
									else
									{
										if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
											StrMessage += responsesUShort[i].ToString();
										else
											StrMessage += BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[i]), 0).ToString();

										if (i != numOfPoints - 1)
											StrMessage += ", ";
									}
								}
							}
						}

						if (EnableSlaveMessages)
							SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, "{ " + StrMessage + " }"));

						AddressList[sndrIndex].ValuesToWrite.Text = "{ " + StrMessage + " }";
						StrMessage = "";

						//Set the corresponding status label back color to Green = Success
						AddressList[sndrIndex].LabelStatus.BackColor = Color.Green;
					}
					catch (Exception ex)
					{
						ExceptionsAndActions(ex);
						AddressList[sndrIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[sndrIndex].ValuesToWrite.BackColor = Color.Salmon; });
						AddressList[sndrIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[sndrIndex].ValuesToWrite.Text = "No Response or Error"; });
						//Set the corresponding status label back color to Red = Failed
						AddressList[sndrIndex].LabelStatus.BackColor = Color.Red;
						return;
					}
				}
				else //FunctionCode = "Write", applicable for addresses that start with "4"
				{
					if (string.IsNullOrWhiteSpace(AddressList[sndrIndex].ValuesToWrite.Text))
					{
						MessageBox.Show("No values to write are provided!");
						return;
					}

					//Split string based on comma delimiter
					var values = AddressList[sndrIndex].ValuesToWrite.Text.Split(new char[] { ',' });

					if (ValuesToWriteCheckPassed(sndrIndex, values))
					{
						try
						{
							//Set the corresponding status label back color to Yellow = Processing
							AddressList[sndrIndex].LabelStatus.BackColor = Color.Yellow;

							ushort[] valUShort = null;
							floatValue = new float[numOfPoints];
							dfloatValue = new double[numOfPoints];

							if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
							{
								string SlaveTag = "";
								if (sndrIndex < 4)
									SlaveTag = "Slave1";
								else
									SlaveTag = "Slave2";

								ushort And_Mask = 0;
								ushort Or_Mask = 0;
								int startRegister = 0;
								int shiftRegister = 0;
								int realRegister = 0;
								int tempBitNumber = 0;
								string[] val2change = new string[] { };

								if (startAddress.IndexOfAny(new char[] { 'O' }) != -1) // *** Process 128-bit Addresses ***
								{
									if (!chbFC22.Checked)
									{
										if (chbBitReading.Checked) //Set the read value to variable ReadValue
											UpdateHoldingRegisters(SlaveTag, slaveID, startAddress, 1, sndrIndex, 0, true);
										else
											UpdateHoldingRegisters(SlaveTag, slaveID, startAddress, numOfPoints, sndrIndex, 0, true);

										val2change = ReadValue.Split(new char[] { ',' }); //Split string based on comma delimiter
									}
									else
									{
										startRegister = Convert.ToInt32(Math.Floor((double)(BitNumber / 16)));

										if (!chbSwapBytes.Checked)
										{
											if (BitNumber < 8 || (BitNumber > 15 && BitNumber < 24) || (BitNumber > 31 && BitNumber < 40) || (BitNumber > 47 && BitNumber < 56) || (BitNumber > 63 && BitNumber < 72) || (BitNumber > 79 && BitNumber < 88) || (BitNumber > 95 && BitNumber < 104) || (BitNumber > 111 && BitNumber < 120))
												tempBitNumber = BitNumber - startRegister * 16 + 8;
											else
												tempBitNumber = BitNumber - startRegister * 16 - 8;
										}
										else
											tempBitNumber = BitNumber - startRegister * 16;
									}

									if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LO
									{
										if (chbFC22.Checked)
										{
											if (chbBitReading.Checked)
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister + shiftRegister, 128);

													if (tempBitNumber + i > 15)
														tempBitNumber = -i;

													And_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0" || values[0].Trim() == "False" || values[0].Trim() == "false")
															Or_Mask = 0;
														else
															Or_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));
													}
													else
														Or_Mask += Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber + i)));

													if ((BitNumber + i) == 15 || (BitNumber + i) == 31 || (BitNumber + i) == 47 || (BitNumber + i) == 63 || (BitNumber + i) == 79 || (BitNumber + i) == 95 || (BitNumber + i) == 111 || i == (numOfPoints - 1))
													{
														if (EnableMasterMessages)
															MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (Int128 bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1) + realRegister), BitNumber));

														MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("L") - 1) + realRegister), (ushort)~And_Mask, Or_Mask);

														if (EnableSlaveMessages)
															SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

														And_Mask = 0;
														Or_Mask = 0;
														shiftRegister += 1;
													}
												}
											}
											else
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister, 128);

													And_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0" || values[0].Trim() == "False" || values[0].Trim() == "false")
															Or_Mask = 0;
														else
															Or_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));
													}
													else
														Or_Mask = Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber)));

													if (EnableMasterMessages)
														MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (Int128 bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1) + realRegister + i * 8), BitNumber));

													MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("L") - 1) + realRegister + i * 8), (ushort)~And_Mask, Or_Mask);

													if (EnableSlaveMessages)
														SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

													And_Mask = 0;
													Or_Mask = 0;
												}
											}
										}
										else
										{
											if (chbBitReading.Checked)
											{
												valUShort = new ushort[8];
												BigInteger tempReadValue = BigInteger.Parse(val2change[0]);

												for (int i = 0; i < numOfPoints; i++)
												{
													if (values.Length == 1)
														tempReadValue = ChangeUInt128Bit(tempReadValue, BitNumber + i, values[0].Trim());
													else
														tempReadValue = ChangeUInt128Bit(tempReadValue, BitNumber + i, values[i].Trim());
												}

												byte[] bytes = new byte[17];
												Array.Copy(tempReadValue.ToByteArray(), bytes, tempReadValue.ToByteArray().Length);

												valUShort[0] = BitConverter.ToUInt16(bytes, 0);
												valUShort[1] = BitConverter.ToUInt16(bytes, 2);
												valUShort[2] = BitConverter.ToUInt16(bytes, 4);
												valUShort[3] = BitConverter.ToUInt16(bytes, 6);
												valUShort[4] = BitConverter.ToUInt16(bytes, 8);
												valUShort[5] = BitConverter.ToUInt16(bytes, 10);
												valUShort[6] = BitConverter.ToUInt16(bytes, 12);
												valUShort[7] = BitConverter.ToUInt16(bytes, 14);
											}
											else
											{
												valUShort = new ushort[8 * val2change.Length];
												byte[] bytes = new byte[17];

												for (int i = 0; i < val2change.Length; i++)
												{
													if (values.Length == 1)
													{
														Array.Copy(ChangeUInt128Bit(BigInteger.Parse(val2change[i]), BitNumber, values[0].Trim()).ToByteArray(), bytes, ChangeUInt128Bit(BigInteger.Parse(val2change[i]), BitNumber, values[0].Trim()).ToByteArray().Length);
														valUShort[8 * i] = BitConverter.ToUInt16(bytes, 0);
														valUShort[8 * i + 1] = BitConverter.ToUInt16(bytes, 2);
														valUShort[8 * i + 2] = BitConverter.ToUInt16(bytes, 4);
														valUShort[8 * i + 3] = BitConverter.ToUInt16(bytes, 6);
														valUShort[8 * i + 4] = BitConverter.ToUInt16(bytes, 8);
														valUShort[8 * i + 5] = BitConverter.ToUInt16(bytes, 10);
														valUShort[8 * i + 6] = BitConverter.ToUInt16(bytes, 12);
														valUShort[8 * i + 7] = BitConverter.ToUInt16(bytes, 14);
													}
													else
													{
														Array.Copy(ChangeUInt128Bit(BigInteger.Parse(val2change[i]), BitNumber, values[i].Trim()).ToByteArray(), bytes, ChangeUInt128Bit(BigInteger.Parse(val2change[i]), BitNumber, values[i].Trim()).ToByteArray().Length);
														valUShort[8 * i] = BitConverter.ToUInt16(bytes, 0);
														valUShort[8 * i + 1] = BitConverter.ToUInt16(bytes, 2);
														valUShort[8 * i + 2] = BitConverter.ToUInt16(bytes, 4);
														valUShort[8 * i + 3] = BitConverter.ToUInt16(bytes, 6);
														valUShort[8 * i + 4] = BitConverter.ToUInt16(bytes, 8);
														valUShort[8 * i + 5] = BitConverter.ToUInt16(bytes, 10);
														valUShort[8 * i + 6] = BitConverter.ToUInt16(bytes, 12);
														valUShort[8 * i + 7] = BitConverter.ToUInt16(bytes, 14);
													}
												}
											}

											if (!chbSwapBytes.Checked)
												valUShort = SwapBytesFunction(valUShort);

											if (chbSwapWords.Checked)
												valUShort = SwapWords128Function(valUShort);

											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (Int128 bit {3}).", slaveID, 8 * val2change.Length, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1)), BitNumber));

											MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("L") - 1)), valUShort);

											if (EnableSlaveMessages)
												SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
										}
									}
									else //UO
									{
										if (chbFC22.Checked)
										{
											if (chbBitReading.Checked)
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister + shiftRegister, 128);

													if (tempBitNumber + i > 15)
														tempBitNumber = -i;

													And_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));
													}
													else
														Or_Mask += Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber + i)));

													if ((BitNumber + i) == 15 || (BitNumber + i) == 31 || (BitNumber + i) == 47 || (BitNumber + i) == 63 || (BitNumber + i) == 79 || (BitNumber + i) == 95 || (BitNumber + i) == 111 || i == (numOfPoints - 1))
													{
														if (EnableMasterMessages)
															MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (UInt128 bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + realRegister), BitNumber));

														MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + realRegister), (ushort)~And_Mask, Or_Mask);

														if (EnableSlaveMessages)
															SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

														And_Mask = 0;
														Or_Mask = 0;
														shiftRegister += 1;
													}
												}
											}
											else
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister, 128);

													And_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));
													}
													else
														Or_Mask = Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber)));

													if (EnableMasterMessages)
														MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (UInt128 bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + realRegister + i * 8), BitNumber));

													MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + realRegister + i * 8), (ushort)~And_Mask, Or_Mask);

													if (EnableSlaveMessages)
														SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

													And_Mask = 0;
													Or_Mask = 0;
												}
											}
										}
										else
										{
											if (chbBitReading.Checked)
											{
												valUShort = new ushort[8];

												BigInteger tempReadValue = BigInteger.Parse(val2change[0]);

												for (int i = 0; i < numOfPoints; i++)
												{
													if (values.Length == 1)
														tempReadValue = ChangeUInt128Bit(tempReadValue, BitNumber + i, values[0].Trim());
													else
														tempReadValue = ChangeUInt128Bit(tempReadValue, BitNumber + i, values[i].Trim());
												}

												byte[] bytes = new byte[17];
												Array.Copy(tempReadValue.ToByteArray(), bytes, tempReadValue.ToByteArray().Length);

												valUShort[0] = BitConverter.ToUInt16(bytes, 0);
												valUShort[1] = BitConverter.ToUInt16(bytes, 2);
												valUShort[2] = BitConverter.ToUInt16(bytes, 4);
												valUShort[3] = BitConverter.ToUInt16(bytes, 6);
												valUShort[4] = BitConverter.ToUInt16(bytes, 8);
												valUShort[5] = BitConverter.ToUInt16(bytes, 10);
												valUShort[6] = BitConverter.ToUInt16(bytes, 12);
												valUShort[7] = BitConverter.ToUInt16(bytes, 14);
											}
											else
											{
												valUShort = new ushort[8 * val2change.Length];

												for (int i = 0; i < val2change.Length; i++)
												{
													if (values.Length == 1)
													{
														byte[] bytes = new byte[17];
														Array.Copy(ChangeUInt128Bit(BigInteger.Parse(val2change[i]), BitNumber, values[0].Trim()).ToByteArray(), bytes, ChangeUInt128Bit(BigInteger.Parse(val2change[i]), BitNumber, values[0].Trim()).ToByteArray().Length);
														valUShort[8 * i] = BitConverter.ToUInt16(bytes, 0);
														valUShort[8 * i + 1] = BitConverter.ToUInt16(bytes, 2);
														valUShort[8 * i + 2] = BitConverter.ToUInt16(bytes, 4);
														valUShort[8 * i + 3] = BitConverter.ToUInt16(bytes, 6);
														valUShort[8 * i + 4] = BitConverter.ToUInt16(bytes, 8);
														valUShort[8 * i + 5] = BitConverter.ToUInt16(bytes, 10);
														valUShort[8 * i + 6] = BitConverter.ToUInt16(bytes, 12);
														valUShort[8 * i + 7] = BitConverter.ToUInt16(bytes, 14);
													}
													else
													{
														byte[] bytes = new byte[17];
														Array.Copy(ChangeUInt128Bit(BigInteger.Parse(val2change[i]), BitNumber, values[i].Trim()).ToByteArray(), bytes, ChangeUInt128Bit(BigInteger.Parse(val2change[i]), BitNumber, values[0].Trim()).ToByteArray().Length);
														valUShort[8 * i] = BitConverter.ToUInt16(bytes, 0);
														valUShort[8 * i + 1] = BitConverter.ToUInt16(bytes, 2);
														valUShort[8 * i + 2] = BitConverter.ToUInt16(bytes, 4);
														valUShort[8 * i + 3] = BitConverter.ToUInt16(bytes, 6);
														valUShort[8 * i + 4] = BitConverter.ToUInt16(bytes, 8);
														valUShort[8 * i + 5] = BitConverter.ToUInt16(bytes, 10);
														valUShort[8 * i + 6] = BitConverter.ToUInt16(bytes, 12);
														valUShort[8 * i + 7] = BitConverter.ToUInt16(bytes, 14);
													}
												}
											}

											if (!chbSwapBytes.Checked)
												valUShort = SwapBytesFunction(valUShort);

											if (chbSwapWords.Checked)
												valUShort = SwapWords128Function(valUShort);

											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (UInt128 bit {3}).", slaveID, 8 * val2change.Length, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1)), BitNumber));

											MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("U") - 1)), valUShort);

											if (EnableSlaveMessages)
												SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
										}
									}
								}
								else if (startAddress.IndexOfAny(new char[] { 'Q' }) != -1) // *** Process 64-bit Addresses ***
								{
									if (!chbFC22.Checked)
									{
										if (chbBitReading.Checked) //Set the read value to variable ReadValue
											UpdateHoldingRegisters(SlaveTag, slaveID, startAddress, 1, sndrIndex, 0, true);
										else
											UpdateHoldingRegisters(SlaveTag, slaveID, startAddress, numOfPoints, sndrIndex, 0, true);

										val2change = ReadValue.Split(new char[] { ',' }); //Split string based on comma delimiter
									}
									else
									{
										startRegister = (int)Math.Floor((double)(BitNumber / 16));

										if (!chbSwapBytes.Checked)
										{
											if (BitNumber < 8 || (BitNumber > 15 && BitNumber < 24) || (BitNumber > 31 && BitNumber < 40) || (BitNumber > 47 && BitNumber < 56))
												tempBitNumber = BitNumber - startRegister * 16 + 8;
											else
												tempBitNumber = BitNumber - startRegister * 16 - 8;
										}
										else
											tempBitNumber = BitNumber - startRegister * 16;
									}

									if (startAddress.IndexOfAny(new char[] { 'F' }) != -1) //FQ
									{
										if (chbFC22.Checked)
										{
											if (chbBitReading.Checked)
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister + shiftRegister, 64);

													if (tempBitNumber + i > 15)
														tempBitNumber = -i;

													And_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));
													}
													else
													{
														Or_Mask += Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber + i)));
													}
													if ((BitNumber + i) == 15 || (BitNumber + i) == 31 || (BitNumber + i) == 47 || i == (numOfPoints - 1))
													{
														if (EnableMasterMessages)
															MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (float64 bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("F") - 1) + realRegister), BitNumber));

														MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("F") - 1) + realRegister), (ushort)~And_Mask, Or_Mask);

														if (EnableSlaveMessages)
															SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

														And_Mask = 0;
														Or_Mask = 0;
														shiftRegister += 1;
													}
												}
											}
											else
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister, 64);

													And_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask = Convert.ToUInt16(Math.Pow(2, (tempBitNumber)));
													}
													else
														Or_Mask = Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber)));

													if (EnableMasterMessages)
														MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (float64 bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("F") - 1) + realRegister + i * 4), BitNumber));

													MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("F") - 1) + realRegister + i * 4), (ushort)~And_Mask, Or_Mask);

													if (EnableSlaveMessages)
														SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

													And_Mask = 0;
													Or_Mask = 0;
												}
											}
										}
										else
										{
											dfloatValue = new double[val2change.Length];
											for (int i = 0; i < val2change.Length; i++)
											{
												if (chbBitReading.Checked)
												{
													dfloatValue[i] = Convert.ToDouble(val2change[0]);
													if (values.Length == 1)
													{
														for (int j = 0; j < numOfPoints; j++)
														{
															dfloatValue[i] = BitConverter.ToDouble(BitConverter.GetBytes(ChangeInt64Bit(BitConverter.ToInt64(BitConverter.GetBytes(dfloatValue[i]), 0), BitNumber + j, values[0].Trim())), 0);
														}
													}
													else
													{
														for (int j = 0; j < numOfPoints; j++)
														{
															dfloatValue[i] = BitConverter.ToDouble(BitConverter.GetBytes(ChangeInt64Bit(BitConverter.ToInt64(BitConverter.GetBytes(dfloatValue[i]), 0), BitNumber + j, values[j].Trim())), 0);
														}
													}
												}
												else
												{
													if (values.Length == 1)
														dfloatValue[i] = BitConverter.ToDouble(BitConverter.GetBytes(ChangeInt64Bit(BitConverter.ToInt64(BitConverter.GetBytes(Convert.ToDouble(val2change[i])), 0), BitNumber, values[0].Trim())), 0);
													else
														dfloatValue[i] = BitConverter.ToDouble(BitConverter.GetBytes(ChangeInt64Bit(BitConverter.ToInt64(BitConverter.GetBytes(Convert.ToDouble(val2change[i])), 0), BitNumber, values[i].Trim())), 0);
												}
											}

											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (float64 bit {3}).", slaveID, 4 * val2change.Length, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("F") - 1)), BitNumber));

											MMaster.WriteFloat64Registers(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("F") - 1)), dfloatValue, chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));

											if (EnableSlaveMessages)
												SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
										}
									}
									else if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LQ
									{
										if (chbFC22.Checked)
										{
											if (chbBitReading.Checked)
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister + shiftRegister, 64);

													if (tempBitNumber + i > 15)
														tempBitNumber = -i;

													And_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask += Convert.ToUInt16(Math.Pow(2, (tempBitNumber + i)));
													}
													else
														Or_Mask += Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber + i)));

													if ((BitNumber + i) == 15 || (BitNumber + i) == 31 || (BitNumber + i) == 47 || i == (numOfPoints - 1))
													{
														if (EnableMasterMessages)
															MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (integer64 bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1) + realRegister), BitNumber));

														MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("L") - 1) + realRegister), (ushort)~And_Mask, Or_Mask);

														if (EnableSlaveMessages)
															SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

														And_Mask = 0;
														Or_Mask = 0;
														shiftRegister += 1;
													}
												}
											}
											else
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister, 64);

													And_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));
													}
													else
														Or_Mask = Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber)));

													if (EnableMasterMessages)
														MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (integer64 bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1) + realRegister + i * 4), BitNumber));

													MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("L") - 1) + realRegister + i * 4), (ushort)~And_Mask, Or_Mask);

													if (EnableSlaveMessages)
														SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

													And_Mask = 0;
													Or_Mask = 0;
												}
											}
										}
										else
										{
											if (chbBitReading.Checked)
											{
												valUShort = new ushort[4];
												long tempReadValue = Convert.ToInt64(val2change[0]);
												for (int i = 0; i < numOfPoints; i++)
												{
													if (values.Length == 1)
														tempReadValue = ChangeInt64Bit(tempReadValue, BitNumber + i, values[0].Trim());
													else
														tempReadValue = ChangeInt64Bit(tempReadValue, BitNumber + i, values[i].Trim());
												}
												var bytes = BitConverter.GetBytes(tempReadValue);
												valUShort[0] = BitConverter.ToUInt16(bytes, 0);
												valUShort[1] = BitConverter.ToUInt16(bytes, 2);
												valUShort[2] = BitConverter.ToUInt16(bytes, 4);
												valUShort[3] = BitConverter.ToUInt16(bytes, 6);
											}
											else
											{
												valUShort = new ushort[4 * val2change.Length];
												for (int i = 0; i < val2change.Length; i++)
												{
													if (values.Length == 1)
													{
														var bytes = BitConverter.GetBytes(ChangeInt64Bit(Convert.ToInt64(val2change[i]), BitNumber, values[0].Trim()));
														valUShort[4 * i] = BitConverter.ToUInt16(bytes, 0);
														valUShort[4 * i + 1] = BitConverter.ToUInt16(bytes, 2);
														valUShort[4 * i + 2] = BitConverter.ToUInt16(bytes, 4);
														valUShort[4 * i + 3] = BitConverter.ToUInt16(bytes, 6);
													}
													else
													{
														var bytes = BitConverter.GetBytes(ChangeInt64Bit(Convert.ToInt64(val2change[i]), BitNumber, values[i].Trim()));
														valUShort[4 * i] = BitConverter.ToUInt16(bytes, 0);
														valUShort[4 * i + 1] = BitConverter.ToUInt16(bytes, 2);
														valUShort[4 * i + 2] = BitConverter.ToUInt16(bytes, 4);
														valUShort[4 * i + 3] = BitConverter.ToUInt16(bytes, 6);
													}
												}
											}

											if (!chbSwapBytes.Checked)
												valUShort = SwapBytesFunction(valUShort);

											if (chbSwapWords.Checked)
												valUShort = SwapWords64Function(valUShort);

											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (integer64 bit {3}).", slaveID, 4 * val2change.Length, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1)), BitNumber));

											MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("L") - 1)), valUShort);

											if (EnableSlaveMessages)
												SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
										}
									}
									else //UQ
									{
										if (chbFC22.Checked)
										{
											if (chbBitReading.Checked)
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister + shiftRegister, 64);

													if (tempBitNumber + i > 15)
														tempBitNumber = -i;

													And_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));
													}
													else
														Or_Mask += Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber + i)));

													if ((BitNumber + i) == 15 || (BitNumber + i) == 31 || (BitNumber + i) == 47 || i == (numOfPoints - 1))
													{
														if (EnableMasterMessages)
															MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (uinteger64 bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + realRegister), BitNumber));

														MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + realRegister), (ushort)~And_Mask, Or_Mask);

														if (EnableSlaveMessages)
															SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

														And_Mask = 0;
														Or_Mask = 0;
														shiftRegister += 1;
													}
												}
											}
											else
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister, 64);

													And_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));
													}
													else
														Or_Mask = Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber)));

													if (EnableMasterMessages)
														MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (uinteger64 bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + realRegister + i * 4), BitNumber));

													MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + realRegister + i * 4), (ushort)~And_Mask, Or_Mask);

													if (EnableSlaveMessages)
														SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

													And_Mask = 0;
													Or_Mask = 0;
												}
											}
										}
										else
										{
											if (chbBitReading.Checked)
											{
												valUShort = new ushort[4];
												long tempReadValue = Convert.ToInt64(Convert.ToUInt64(val2change[0]));
												for (int i = 0; i < numOfPoints; i++)
												{
													if (values.Length == 1)
														tempReadValue = ChangeInt64Bit(tempReadValue, BitNumber + i, values[0].Trim());
													else
														tempReadValue = ChangeInt64Bit(tempReadValue, BitNumber + i, values[i].Trim());
												}
												var bytes = BitConverter.GetBytes(tempReadValue);
												valUShort[0] = BitConverter.ToUInt16(bytes, 0);
												valUShort[1] = BitConverter.ToUInt16(bytes, 2);
												valUShort[2] = BitConverter.ToUInt16(bytes, 4);
												valUShort[3] = BitConverter.ToUInt16(bytes, 6);
											}
											else
											{
												valUShort = new ushort[4 * val2change.Length];
												for (int i = 0; i < val2change.Length; i++)
												{
													if (values.Length == 1)
													{
														var bytes = BitConverter.GetBytes(ChangeInt64Bit(Convert.ToInt64(Convert.ToUInt64(val2change[i])), BitNumber, values[0].Trim()));
														valUShort[4 * i] = BitConverter.ToUInt16(bytes, 0);
														valUShort[4 * i + 1] = BitConverter.ToUInt16(bytes, 2);
														valUShort[4 * i + 2] = BitConverter.ToUInt16(bytes, 4);
														valUShort[4 * i + 3] = BitConverter.ToUInt16(bytes, 6);
													}
													else
													{
														var bytes = BitConverter.GetBytes(ChangeInt64Bit(Convert.ToInt64(Convert.ToUInt64(val2change[i])), BitNumber, values[i].Trim()));
														valUShort[4 * i] = BitConverter.ToUInt16(bytes, 0);
														valUShort[4 * i + 1] = BitConverter.ToUInt16(bytes, 2);
														valUShort[4 * i + 2] = BitConverter.ToUInt16(bytes, 4);
														valUShort[4 * i + 3] = BitConverter.ToUInt16(bytes, 6);
													}
												}
											}

											if (!chbSwapBytes.Checked)
												valUShort = SwapBytesFunction(valUShort);

											if (chbSwapWords.Checked)
												valUShort = SwapWords64Function(valUShort);

											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (uinteger64 bit {3}).", slaveID, 4 * val2change.Length, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1)), BitNumber));

											MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("U") - 1)), valUShort);

											if (EnableSlaveMessages)
												SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
										}
									}
								}
								else if (startAddress.IndexOfAny(new char[] { 'S' }) != -1)
								{
									if (chbBitReading.Checked)
									{
										valUShort = new ushort[numOfPoints];
										if (values.Length == 1)
										{
											if (values[0].Trim().Length > 1)
											{
												MessageBox.Show("Single character required for write operation!");
												lblMessage.Text = "Single character required for write operation!";
												//Set the corresponding status label back color to Red = Failed
												AddressList[sndrIndex].LabelStatus.BackColor = Color.Red;
												return;
											}
											else
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													valUShort[i] = Convert.ToUInt16(System.Text.Encoding.Default.GetBytes(values[0].Trim())[0]);
												}
											}
										}
										else
										{
											for (int i = 0; i < numOfPoints; i++)
											{
												if (values[i].Trim().Length > 1)
												{
													MessageBox.Show("Single character required for write operation!");
													lblMessage.Text = "Single character required for write operation!";
													//Set the corresponding status label back color to Red = Failed
													AddressList[sndrIndex].LabelStatus.BackColor = Color.Red;
													return;
												}
												else
													valUShort[i] = Convert.ToUInt16(System.Text.Encoding.Default.GetBytes(values[i].Trim())[0]);
											}
										}

										if (!chbSwapBytes.Checked)
											valUShort = SwapBytesFunction(valUShort);

										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (string char {3}).", slaveID, numOfPoints, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("S") - 1)), BitNumber));

										MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("S") - 1)) + BitNumber), valUShort);

										if (EnableSlaveMessages)
											SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
									}
									else
									{
										var numRegisters = Convert.ToInt32(startAddress.Substring(startAddress.IndexOf("S") + 1));
										for (int i = 0; i < numOfPoints; i++)
										{
											if (values.Length == 1 && Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text) > 1)
												valUShort = new ushort[1] { Convert.ToUInt16(System.Text.Encoding.Default.GetBytes(values[0].Trim())[0]) };
											else
												valUShort = new ushort[1] { Convert.ToUInt16(System.Text.Encoding.Default.GetBytes(values[i].Trim())[0]) };

											if (!chbSwapBytes.Checked)
												valUShort = SwapBytesFunction(valUShort);

											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (string char {3}).", slaveID, numRegisters * Convert.ToInt32(numOfPoints), Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("S") - 1)), BitNumber));

											MMaster.WriteSingleRegister(slaveID, Convert.ToUInt16(Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("S") - 1)) + BitNumber + i * numRegisters), valUShort[0]);

											if (EnableSlaveMessages)
												SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
										}
									}
								}
								else
								{
									if (!chbFC22.Checked)
									{
										if (chbBitReading.Checked) //Set the read value to variable ReadValue
											UpdateHoldingRegisters(SlaveTag, slaveID, startAddress, 1, sndrIndex, 0, true);
										else
											UpdateHoldingRegisters(SlaveTag, slaveID, startAddress, numOfPoints, sndrIndex, 0, true);

										val2change = ReadValue.Split(new char[] { ',' }); //Split string based on comma delimiter
									}
									else
									{
										startRegister = Convert.ToInt32(Math.Floor((double)(BitNumber / 16)));

										if (!chbSwapBytes.Checked)
										{
											if (BitNumber < 8 || (BitNumber > 15 && BitNumber < 24))
												tempBitNumber = BitNumber - startRegister * 16 + 8;
											else
												tempBitNumber = BitNumber - startRegister * 16 - 8;
										}
										else
											tempBitNumber = BitNumber - startRegister * 16;
									}

									if (tempAddress.IndexOfAny(new char[] { 'F' }) != -1)
									{
										if (chbFC22.Checked)
										{
											if (chbBitReading.Checked)
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister + shiftRegister, 32);

													if (tempBitNumber + i > 15)
														tempBitNumber = -i;

													And_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));
													}
													else
														Or_Mask += Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber + i)));

													if ((BitNumber + i) == 15 || (BitNumber + i) == 31 || (BitNumber + i) == 47 || i == (numOfPoints - 1))
													{
														if (EnableMasterMessages)
															MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (float bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("F") - 1) + realRegister), BitNumber));

														MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("F") - 1) + realRegister), (ushort)~And_Mask, Or_Mask);

														if (EnableSlaveMessages)
															SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

														And_Mask = 0;
														Or_Mask = 0;
														shiftRegister += 1;
													}
												}
											}
											else
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister, 64);

													And_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask = Convert.ToUInt16(Math.Pow(2, (tempBitNumber)));
													}
													else
														Or_Mask = Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber)));

													if (EnableMasterMessages)
														MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (float bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("F") - 1) + realRegister + i * 2), BitNumber));

													MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("F") - 1) + realRegister + i * 2), (ushort)~And_Mask, Or_Mask);

													if (EnableSlaveMessages)
														SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

													And_Mask = 0;
													Or_Mask = 0;
												}
											}
										}
										else
										{
											floatValue = new float[val2change.Length];
											for (int i = 0; i < val2change.Length; i++)
											{
												if (chbBitReading.Checked)
												{
													floatValue[i] = Convert.ToSingle(val2change[0]);
													if (values.Length == 1)
													{
														for (int j = 0; j < numOfPoints; j++)
														{
															floatValue[i] = BitConverter.ToSingle(BitConverter.GetBytes(ChangeInt32Bit(BitConverter.ToInt32(BitConverter.GetBytes(floatValue[i]), 0), BitNumber + j, values[0].Trim())), 0);
														}
													}
													else
													{
														for (int j = 0; j < numOfPoints; j++)
														{
															floatValue[i] = BitConverter.ToSingle(BitConverter.GetBytes(ChangeInt32Bit(BitConverter.ToInt32(BitConverter.GetBytes(floatValue[i]), 0), BitNumber + j, values[j].Trim())), 0);
														}
													}
												}
												else
												{
													if (values.Length == 1)
														floatValue[i] = BitConverter.ToSingle(BitConverter.GetBytes(ChangeInt32Bit(BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(val2change[i])), 0), BitNumber, values[0].Trim())), 0);
													else
														floatValue[i] = BitConverter.ToSingle(BitConverter.GetBytes(ChangeInt32Bit(BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(val2change[i])), 0), BitNumber, values[i].Trim())), 0);
												}
											}

											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (float bit {3}).", slaveID, 2 * val2change.Length, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("F") - 1)), BitNumber));

											MMaster.WriteFloat32Registers(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("F") - 1)), floatValue, chbSwapBytes.Checked, chbSwapWords.Checked);

											if (EnableSlaveMessages)
												SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
										}
									}
									else if (tempAddress.IndexOfAny(new char[] { 'U' }) != -1)
									{
										if (tempAddress.IndexOfAny(new char[] { 'L' }) != -1)
										{
											if (chbFC22.Checked)
											{
												if (chbBitReading.Checked)
												{
													for (int i = 0; i < numOfPoints; i++)
													{
														realRegister = RegisterCheck(startRegister + shiftRegister, 32);

														if (tempBitNumber + i > 15)
															tempBitNumber = -i;

														And_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));

														if (values.Length == 1)
														{
															if (values[0].Trim() == "0")
																Or_Mask = 0;
															else
																Or_Mask += Convert.ToUInt16(Math.Pow(2, (tempBitNumber + i)));
														}
														else
															Or_Mask += Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber + i)));

														if ((BitNumber + i) == 15 || (BitNumber + i) == 31 || i == (numOfPoints - 1))
														{
															if (EnableMasterMessages)
																MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (uinteger bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + realRegister), BitNumber));

															MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + realRegister), (ushort)~And_Mask, Or_Mask);

															if (EnableSlaveMessages)
																SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

															And_Mask = 0;
															Or_Mask = 0;
															shiftRegister += 1;
														}
													}
												}
												else
												{
													for (int i = 0; i < numOfPoints; i++)
													{
														realRegister = RegisterCheck(startRegister, 32);

														And_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));

														if (values.Length == 1)
														{
															if (values[0].Trim() == "0")
																Or_Mask = 0;
															else
																Or_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));
														}
														else
															Or_Mask = Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber)));

														if (EnableMasterMessages)
															MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (uinteger bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + realRegister + i * 2), BitNumber));

														MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + realRegister + i * 2), (ushort)~And_Mask, Or_Mask);

														if (EnableSlaveMessages)
															SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

														And_Mask = 0;
														Or_Mask = 0;
													}
												}
											}
											else
											{
												if (chbBitReading.Checked)
												{
													valUShort = new ushort[2];
													int tempReadValue = Convert.ToInt32(Convert.ToUInt32(val2change[0]));
													for (int i = 0; i < numOfPoints; i++)
													{
														if (values.Length == 1)
															tempReadValue = ChangeInt32Bit(tempReadValue, BitNumber + i, values[0].Trim());
														else
															tempReadValue = ChangeInt32Bit(tempReadValue, BitNumber + i, values[i].Trim());
													}
													var bytes = BitConverter.GetBytes(tempReadValue);
													valUShort[0] = BitConverter.ToUInt16(bytes, 0);
													valUShort[1] = BitConverter.ToUInt16(bytes, 2);
												}
												else
												{
													valUShort = new ushort[2 * val2change.Length];
													for (int i = 0; i < val2change.Length; i++)
													{
														if (values.Length == 1)
														{
															var bytes = BitConverter.GetBytes(ChangeInt32Bit(Convert.ToInt32(Convert.ToUInt32(val2change[i])), BitNumber, values[0].Trim()));
															valUShort[2 * i] = BitConverter.ToUInt16(bytes, 0);
															valUShort[2 * i + 1] = BitConverter.ToUInt16(bytes, 2);
														}
														else
														{
															var bytes = BitConverter.GetBytes(ChangeInt32Bit(Convert.ToInt32(Convert.ToUInt32(val2change[i])), BitNumber, values[i].Trim()));
															valUShort[2 * i] = BitConverter.ToUInt16(bytes, 0);
															valUShort[2 * i + 1] = BitConverter.ToUInt16(bytes, 2);
														}
													}
												}

												if (!chbSwapBytes.Checked)
													valUShort = SwapBytesFunction(valUShort);

												if (chbSwapWords.Checked)
													valUShort = SwapWordsFunction(valUShort);

												if (EnableMasterMessages)
													MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (uinteger bit {3}).", slaveID, 2 * val2change.Length, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1)), BitNumber));

												MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("U") - 1)), valUShort);

												if (EnableSlaveMessages)
													SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
											}
										}
										else
										{
											if (chbFC22.Checked)
											{
												if (chbBitReading.Checked)
												{
													for (int i = 0; i < numOfPoints; i++)
													{
														And_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));

														if (values.Length == 1)
														{
															if (values[0].Trim() == "0")
																Or_Mask = 0;
															else
																Or_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));
														}
														else
															Or_Mask += Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber + i)));

														if ((BitNumber + i) == 15 || i == (numOfPoints - 1))
														{
															if (EnableMasterMessages)
																MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (ushort bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1)), BitNumber));

															MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("U") - 1)), (ushort)~And_Mask, Or_Mask);

															if (EnableSlaveMessages)
																SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

															And_Mask = 0;
															Or_Mask = 0;
														}
													}
												}
												else
												{
													for (int i = 0; i < numOfPoints; i++)
													{
														And_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));

														if (values.Length == 1)
														{
															if (values[0].Trim() == "0")
																Or_Mask = 0;
															else
																Or_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));
														}
														else
															Or_Mask = Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber)));

														if (EnableMasterMessages)
															MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (ushort bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + i * 2), BitNumber));

														MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("U") - 1) + i * 2), (ushort)~And_Mask, Or_Mask);

														if (EnableSlaveMessages)
															SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

														And_Mask = 0;
														Or_Mask = 0;
													}
												}
											}
											else
											{
												if (chbBitReading.Checked)
												{
													valUShort = new ushort[1];
													int tempReadValue = Convert.ToInt32(Convert.ToUInt16(val2change[0]));
													for (int i = 0; i < numOfPoints; i++)
													{
														if (values.Length == 1)
															tempReadValue = ChangeInt32Bit(tempReadValue, BitNumber + i, values[0].Trim());
														else
															tempReadValue = ChangeInt32Bit(tempReadValue, BitNumber + i, values[i].Trim());
													}
													valUShort[0] = Convert.ToUInt16(tempReadValue);
												}
												else
												{
													valUShort = new ushort[val2change.Length];
													for (int i = 0; i < numOfPoints; i++)
													{
														if (values.Length == 1)
															valUShort[i] = Convert.ToUInt16(ChangeInt32Bit(Convert.ToInt32(Convert.ToUInt16(val2change[i])), BitNumber, values[0].Trim()));
														else
															valUShort[i] = Convert.ToUInt16(ChangeInt32Bit(Convert.ToInt32(Convert.ToUInt16(val2change[i])), BitNumber, values[i].Trim()));
													}
												}

												if (!chbSwapBytes.Checked)
													valUShort = SwapBytesFunction(valUShort);

												if (EnableMasterMessages)
													MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (ushort bit {3}).", slaveID, val2change.Length, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1)), BitNumber));

												MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("U") - 1)), valUShort);

												if (EnableSlaveMessages)
													SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
											}
										}
									}
									else if (tempAddress.IndexOfAny(new char[] { 'L' }) != -1)
									{
										if (chbFC22.Checked)
										{
											if (chbBitReading.Checked)
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister + shiftRegister, 32);

													if (tempBitNumber + i > 15)
														tempBitNumber = -i;

													And_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));
													}
													else
														Or_Mask += Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber + i)));

													if ((BitNumber + i) == 15 || (BitNumber + i) == 31 || i == (numOfPoints - 1))
													{
														if (EnableMasterMessages)
															MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (integer bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1) + realRegister), BitNumber));

														MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("L") - 1) + realRegister), (ushort)~And_Mask, Or_Mask);

														if (EnableSlaveMessages)
															SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

														And_Mask = 0;
														Or_Mask = 0;
														shiftRegister += 1;
													}
												}
											}
											else
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													realRegister = RegisterCheck(startRegister, 32);

													And_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));
													}
													else
														Or_Mask = Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber)));

													if (EnableMasterMessages)
														MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (integer bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1) + realRegister + i * 2), BitNumber));

													MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("L") - 1) + realRegister + i * 2), (ushort)~And_Mask, Or_Mask);

													if (EnableSlaveMessages)
														SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

													And_Mask = 0;
													Or_Mask = 0;
												}
											}
										}
										else
										{
											if (chbBitReading.Checked)
											{
												valUShort = new ushort[2];
												int tempReadValue = Convert.ToInt32(val2change[0]);
												for (int i = 0; i < numOfPoints; i++)
												{
													if (values.Length == 1)
														tempReadValue = ChangeInt32Bit(tempReadValue, BitNumber + i, values[0].Trim());
													else
														tempReadValue = ChangeInt32Bit(tempReadValue, BitNumber + i, values[i].Trim());
												}
												var bytes = BitConverter.GetBytes(tempReadValue);
												valUShort[0] = BitConverter.ToUInt16(bytes, 0);
												valUShort[1] = BitConverter.ToUInt16(bytes, 2);
											}
											else
											{
												valUShort = new ushort[2 * val2change.Length];
												for (int i = 0; i < val2change.Length; i++)
												{
													if (values.Length == 1)
													{
														var bytes = BitConverter.GetBytes(ChangeInt32Bit(Convert.ToInt32(val2change[i]), BitNumber, values[0].Trim()));
														valUShort[2 * i] = BitConverter.ToUInt16(bytes, 0);
														valUShort[2 * i + 1] = BitConverter.ToUInt16(bytes, 2);
													}
													else
													{
														var bytes = BitConverter.GetBytes(ChangeInt32Bit(Convert.ToInt32(val2change[i]), BitNumber, values[i].Trim()));
														valUShort[2 * i] = BitConverter.ToUInt16(bytes, 0);
														valUShort[2 * i + 1] = BitConverter.ToUInt16(bytes, 2);
													}
												}
											}

											if (!chbSwapBytes.Checked)
												valUShort = SwapBytesFunction(valUShort);

											if (chbSwapWords.Checked)
												valUShort = SwapWordsFunction(valUShort);

											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (integer bit {3}).", slaveID, 2 * val2change.Length, Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1)), BitNumber));

											MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.IndexOf("L") - 1)), valUShort);

											if (EnableSlaveMessages)
												SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
										}
									}
									else
									{
										if (chbFC22.Checked)
										{
											if (chbBitReading.Checked)
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													And_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask += Convert.ToUInt16(Math.Pow(2, tempBitNumber + i));
													}
													else
														Or_Mask += Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber + i)));

													if ((BitNumber + i) == 15 || i == (numOfPoints - 1))
													{
														if (EnableMasterMessages)
															MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (short bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1)), BitNumber));

														MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1)), (ushort)~And_Mask, Or_Mask);

														if (EnableSlaveMessages)
															SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

														And_Mask = 0;
														Or_Mask = 0;
													}
												}
											}
											else
											{
												for (int i = 0; i < numOfPoints; i++)
												{
													And_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));

													if (values.Length == 1)
													{
														if (values[0].Trim() == "0")
															Or_Mask = 0;
														else
															Or_Mask = Convert.ToUInt16(Math.Pow(2, tempBitNumber));
													}
													else
														Or_Mask = Convert.ToUInt16(Convert.ToInt32(values[i].Trim()) * Convert.ToInt32(Math.Pow(2, tempBitNumber)));

													if (EnableMasterMessages)
														MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Masked bit write holding register at address {1} (short bit {2}).", slaveID, Convert.ToInt32(startAddress.Substring(1) + i * 2), BitNumber));

													MMaster.MaskWriteRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1) + i * 2), (ushort)~And_Mask, Or_Mask);

													if (EnableSlaveMessages)
														SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

													And_Mask = 0;
													Or_Mask = 0;
												}
											}
										}
										else
										{
											if (chbBitReading.Checked)
											{
												valUShort = new ushort[1];
												int tempReadValue = Convert.ToInt32(Convert.ToInt16(val2change[0]));
												for (int i = 0; i < numOfPoints; i++)
												{
													if (values.Length == 1)
														tempReadValue = ChangeInt16Bit(Convert.ToInt16(tempReadValue), BitNumber + i, values[0].Trim());
													else
														tempReadValue = ChangeInt16Bit(Convert.ToInt16(tempReadValue), BitNumber + i, values[i].Trim());
												}
												valUShort[0] = BitConverter.ToUInt16(BitConverter.GetBytes(tempReadValue), 0);
											}
											else
											{
												valUShort = new ushort[val2change.Length];
												for (int i = 0; i < numOfPoints; i++)
												{
													if (values.Length == 1)
														valUShort[i] = Convert.ToUInt16(ChangeInt32Bit(Convert.ToInt32(Convert.ToInt16(val2change[i])), BitNumber, values[0].Trim()));
													else
														valUShort[i] = Convert.ToUInt16(ChangeInt32Bit(Convert.ToInt32(Convert.ToInt16(val2change[i])), BitNumber, values[i].Trim()));
												}
											}

											if (!chbSwapBytes.Checked)
												valUShort = SwapBytesFunction(valUShort);

											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (short bit {3}).", slaveID, val2change.Length, Convert.ToInt32(startAddress.Substring(1)), BitNumber));

											MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1)), valUShort);

											if (EnableSlaveMessages)
												SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
										}
									}
								}
							}
							else
							{
								if (startAddress.IndexOfAny(new char[] { 'O' }) != -1)
								{
									if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LO
									{
										string writeAddress = startAddress.Substring(1, startAddress.IndexOf("L") - 1);
										BigInteger bigInt = 0;

										if (values.Length == 1)
										{
											valUShort = new ushort[8 * Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text)];
											
											try
											{
												if (Convert.ToInt32(values[0][0]) == 8722 || Convert.ToInt32(values[0][0]) == 45)
												{
													if (BigInteger.TryParse(values[0].Substring(1), out bigInt))
													{
														bigInt = BigInteger.Parse("340282366920938463463374607431768211456") - bigInt;

														for (int i = 0; i < Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text); i++)
														{
															byte[] valBytes = new byte[17];
															Array.Copy(bigInt.ToByteArray(), valBytes, bigInt.ToByteArray().Length);
															for (int j = 0; j < 8; j++)
															{
																valUShort[8 * i + j] = BitConverter.ToUInt16(valBytes, 2 * j);
															}
														}
													}
												}
												else
												{
													if (BigInteger.TryParse(values[0], out bigInt))
													{
														for (int i = 0; i < Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text); i++)
														{
															byte[] valBytes = new byte[17];
															Array.Copy(bigInt.ToByteArray(), valBytes, bigInt.ToByteArray().Length);

															for (int j = 0; j < 8; j++)
															{
																valUShort[8 * i + j] = BitConverter.ToUInt16(valBytes, 2 * j);
															}
														}
													}
												}
											}
											catch (Exception ex)
											{
												MessageBox.Show(ex.Message);
											}
										}
										else
										{
											valUShort = new ushort[8 * values.Length];

											for (int i = 0; i < values.Length; i++)
											{
												try
												{
													if (Convert.ToInt32(values[i][0]) == 8722 || Convert.ToInt32(values[i][0]) == 45)
													{
														if (BigInteger.TryParse(values[i].Substring(1), out bigInt))
														{
															bigInt = BigInteger.Parse("340282366920938463463374607431768211456") - bigInt;
															byte[] valBytes = new byte[17];
															Array.Copy(bigInt.ToByteArray(), valBytes, bigInt.ToByteArray().Length);

															for (int j = 0; j < 8; j++)
															{
																valUShort[8 * i + j] = BitConverter.ToUInt16(valBytes, 2 * j);
															}
														}
													}
													else
													{
														if (BigInteger.TryParse(values[i], out bigInt))
														{
															byte[] valBytes = new byte[17];
															Array.Copy(bigInt.ToByteArray(), valBytes, bigInt.ToByteArray().Length);

															for (int j = 0; j < 8; j++)
															{
																valUShort[8 * i + j] = BitConverter.ToUInt16(valBytes, 2 * j);
															}
														}
													}
												}
												catch (Exception ex)
												{
													MessageBox.Show(ex.Message);
												}
											}
										}

										if (!chbSwapBytes.Checked)
											valUShort = SwapBytesFunction(valUShort);

										if (chbSwapWords.Checked)
											valUShort = SwapWords128Function(valUShort);

										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (Int128).", slaveID, 8 * numOfPoints, Convert.ToInt32(writeAddress)));

										MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(writeAddress), valUShort);

										if (EnableSlaveMessages)
											SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
									}
									else //UO
									{
										string writeAddress = startAddress.Substring(1, startAddress.IndexOf("U") - 1);
										BigInteger bigInt = 0;

										if (values.Length == 1)
										{
											valUShort = new ushort[8 * Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text)];

											try
											{
												if (BigInteger.TryParse(values[0], out bigInt))
												{
													for (int i = 0; i < Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text); i++)
													{
														byte[] valBytes = new byte[17];
														Array.Copy(bigInt.ToByteArray(), valBytes, bigInt.ToByteArray().Length);
														for (int j = 0; j < 8; j++)
														{
															valUShort[8 * i + j] = BitConverter.ToUInt16(valBytes, 2 * j);
														}
													}
												}
											}
											catch (Exception ex)
											{
												MessageBox.Show(ex.Message);
											}
										}
										else
										{
											valUShort = new ushort[8 * values.Length];

											for (int i = 0; i < values.Length; i++)
											{
												try
												{
													if (BigInteger.TryParse(values[i], out bigInt))
													{
														byte[] valBytes = new byte[17];
														Array.Copy(bigInt.ToByteArray(), valBytes, bigInt.ToByteArray().Length);

														for (int j = 0; j < 8; j++)
														{
															valUShort[8 * i + j] = BitConverter.ToUInt16(valBytes, 2 * j);
														}
													}
												}
												catch (Exception ex)
												{
													MessageBox.Show(ex.Message);
												}
											}
										}

										if (!chbSwapBytes.Checked)
											valUShort = SwapBytesFunction(valUShort);

										if (chbSwapWords.Checked)
											valUShort = SwapWords128Function(valUShort);

										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (UInt128).", slaveID, 8 * numOfPoints, Convert.ToInt32(writeAddress)));

										MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(writeAddress), valUShort);

										if (EnableSlaveMessages)
											SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
									}
								}
								else if (startAddress.IndexOfAny(new char[] { 'Q' }) != -1)
								{
									if (startAddress.IndexOfAny(new char[] { 'F' }) != -1) //FQ
									{
										startAddress = startAddress.Substring(0, startAddress.IndexOf("F"));

										if (values.Length == 1)
										{
											for (int i = 0; i < Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text); i++)
											{
												dfloatValue[i] = Convert.ToDouble(values[0]);
											}
										}
										else
										{
											for (int i = 0; i < values.Length; i++)
											{
												dfloatValue[i] = Convert.ToDouble(values[i]);
											}
										}

										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (float64).", slaveID, 4 * numOfPoints, Convert.ToInt32(startAddress.Substring(1))));

										MMaster.WriteFloat64Registers(slaveID, Convert.ToUInt16(startAddress.Substring(1)), dfloatValue, chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));

										if (EnableSlaveMessages)
											SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
									}
									else if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LQ
									{
										string writeAddress = startAddress.Substring(1, startAddress.IndexOf("L") - 1);

										if (values.Length == 1)
										{
											valUShort = new ushort[4 * Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text)];

											for (int i = 0; i < Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text); i++)
											{
												byte[] valBytes = null;
												valBytes = BitConverter.GetBytes(Convert.ToInt64(values[0]));

												for (int j = 0; j < 4; j++)
												{
													valUShort[4 * i + j] = BitConverter.ToUInt16(valBytes, 2 * j);
												}
											}
										}
										else
										{
											valUShort = new ushort[4 * values.Length];

											for (int i = 0; i < values.Length; i++)
											{
												byte[] valBytes = null;
												valBytes = BitConverter.GetBytes(Convert.ToInt64(values[i]));
												for (int j = 0; j < 4; j++)
												{
													valUShort[4 * i + j] = BitConverter.ToUInt16(valBytes, 2 * j);
												}
											}
										}

										if (!chbSwapBytes.Checked)
											valUShort = SwapBytesFunction(valUShort);

										if (chbSwapWords.Checked)
											valUShort = SwapWords64Function(valUShort);

										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (integer64).", slaveID, 4 * numOfPoints, Convert.ToInt32(writeAddress)));

										MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(writeAddress), valUShort);

										if (EnableSlaveMessages)
											SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
									}
									else //UQ
									{
										string writeAddress = startAddress.Substring(1, startAddress.IndexOf("U") - 1);

										if (values.Length == 1)
										{
											valUShort = new ushort[4 * Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text)];
											for (int i = 0; i < Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text); i++)
											{
												byte[] valBytes = null;
												valBytes = BitConverter.GetBytes(Convert.ToUInt64(values[0]));
												for (int j = 0; j < 4; j++)
												{
													valUShort[4 * i + j] = BitConverter.ToUInt16(valBytes, 2 * j);
												}
											}
										}
										else
										{
											valUShort = new ushort[4 * values.Length];
											for (int i = 0; i < values.Length; i++)
											{
												byte[] valBytes = null;
												valBytes = BitConverter.GetBytes(Convert.ToUInt64(values[i]));
												for (int j = 0; j < 4; j++)
												{
													valUShort[4 * i + j] = BitConverter.ToUInt16(valBytes, 2 * j);
												}
											}
										}

										if (!chbSwapBytes.Checked)
											valUShort = SwapBytesFunction(valUShort);

										if (chbSwapWords.Checked)
											valUShort = SwapWords64Function(valUShort);

										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (uinteger64).", slaveID, 4 * numOfPoints, Convert.ToInt32(writeAddress)));

										MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(writeAddress), valUShort);

										if (EnableSlaveMessages)
											SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
									}
								}
								else if (startAddress.IndexOfAny(new char[] { 'S' }) != -1)
								{
									var numRegisters = Convert.ToInt32(startAddress.Substring(startAddress.IndexOf("S") + 1));

									if (values.Length == 1 && Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text) > 1)
									{
										valUShort = new ushort[numRegisters * Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text)];

										for (int i = 0; i < Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text); i++)
										{
											var data = ConvertStringToStringOfUShorts(values[0].Trim());
											if (data.Length > numRegisters)
												Array.ConstrainedCopy(data, 0, valUShort, i * numRegisters, numRegisters);
											else
												Array.ConstrainedCopy(data, 0, valUShort, i * numRegisters, data.Length);
										}
									}
									else
									{
										valUShort = new ushort[numRegisters * values.Length];
										for (int i = 0; i < values.Length; i++)
										{
											var data = ConvertStringToStringOfUShorts(values[i].Trim());

											if (data.Length > numRegisters)
											{
												var dr = MessageBox.Show("The string length is longer than the required number of string registers." + Environment.NewLine + "Continue writing truncated string?", "Message", MessageBoxButtons.YesNo);
												if (dr == DialogResult.Yes)
													Array.ConstrainedCopy(data, 0, valUShort, i * numRegisters, numRegisters);
												else
												{
													lblMessage.Text = "User aborted the operation!";
													//Set the corresponding status label back color to Red = Failed
													AddressList[sndrIndex].LabelStatus.BackColor = Color.Red;
													return;
												}
											}
											else
												Array.ConstrainedCopy(data, 0, valUShort, i * numRegisters, data.Length);
										}
									}

									if (!chbSwapBytes.Checked)
										valUShort = SwapBytesFunction(valUShort);

									if (EnableMasterMessages)
										MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (string).", slaveID, numRegisters * Convert.ToInt32(numOfPoints), Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("S") - 1))));

									if (MMaster != null)
										MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("S") - 1))), valUShort);

									if (EnableSlaveMessages)
										SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));

								}
								else if (startAddress.IndexOfAny(new char[] { 'F' }) != -1)
								{
									startAddress = startAddress.Substring(0, startAddress.Length - 1);

									if (values.Length == 1 && Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text) > 1)
									{
										for (int i = 0; i < Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text); i++)
										{
											floatValue[i] = Convert.ToSingle(values[0]);
										}
									}
									else
									{
										for (int i = 0; i < values.Length; i++)
										{
											floatValue[i] = Convert.ToSingle(values[i]);
										}
									}

									if (EnableMasterMessages)
										MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (float).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(startAddress.Substring(1))));

									MMaster.WriteFloat32Registers(slaveID, Convert.ToUInt16(startAddress.Substring(1)), floatValue, chbSwapBytes.Checked, chbSwapWords.Checked);

									if (EnableSlaveMessages)
										SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
								}
								else if (startAddress.IndexOfAny(new char[] { 'L' }) != -1)
								{
									string writeAddress = null;

									if (startAddress.Contains("UL"))
										writeAddress = startAddress.Substring(0, startAddress.Length - 2);
									else
										writeAddress = startAddress.Substring(0, startAddress.Length - 1);

									if (values.Length == 1 && Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text) > 1)
									{
										valUShort = new ushort[2 * Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text)];

										for (int i = 0; i < Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text); i++)
										{
											byte[] valBytes = null;

											if (startAddress.Contains("UL"))
												valBytes = BitConverter.GetBytes(Convert.ToUInt32(values[0]));
											else
												valBytes = BitConverter.GetBytes(Convert.ToInt32(values[0]));

											for (int j = 0; j < 2; j++)
											{
												valUShort[2 * i + j] = BitConverter.ToUInt16(valBytes, 2 * j);
											}
										}
									}
									else
									{
										valUShort = new ushort[2 * values.Length];

										for (int i = 0; i < values.Length; i++)
										{
											byte[] valBytes = null;

											if (startAddress.Contains("UL"))
												valBytes = BitConverter.GetBytes(Convert.ToUInt32(values[i]));
											else
												valBytes = BitConverter.GetBytes(Convert.ToInt32(values[i]));

											for (int j = 0; j < 2; j++)
											{
												valUShort[2 * i + j] = BitConverter.ToUInt16(valBytes, 2 * j);
											}
										}
									}

									if (!chbSwapBytes.Checked)
										valUShort = SwapBytesFunction(valUShort);

									if (chbSwapWords.Checked)
										valUShort = SwapWordsFunction(valUShort);

									if (startAddress.Contains("UL"))
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (uinteger).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(writeAddress.Substring(1))));
									}
									else
									{
										if (EnableMasterMessages)
											MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (integer).", slaveID, 2 * Convert.ToInt32(numOfPoints), Convert.ToInt32(writeAddress.Substring(1))));
									}

									MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(writeAddress.Substring(1)), valUShort);

									if (EnableSlaveMessages)
										SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
								}
								else
								{
									if (values.Length == 1 && Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text) > 1)
									{
										valUShort = new ushort[Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text)];

										for (int i = 0; i < Convert.ToInt32(AddressList[sndrIndex].NumberOfElements.Text); i++)
										{
											if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
												valUShort[i] = Convert.ToUInt16(values[0]);
											else
												valUShort[i] = BitConverter.ToUInt16(BitConverter.GetBytes(Convert.ToInt32(values[0])), 0);
										}
									}
									else
									{
										valUShort = new ushort[values.Length];
										for (int i = 0; i < values.Length; i++)
										{
											if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
												valUShort[i] = Convert.ToUInt16(values[i]);
											else
												valUShort[i] = BitConverter.ToUInt16(BitConverter.GetBytes(Convert.ToInt32(values[i])), 0);
										}
									}

									if (!chbSwapBytes.Checked)
										valUShort = SwapBytesFunction(valUShort);

									if (numOfPoints > 1)
									{
										if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (ushort).", slaveID, numOfPoints, Convert.ToInt32(startAddress.Substring(1, startAddress.Length - 2))));

											MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.Length - 2)), valUShort);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write {1} holding registers starting at address {2} (short).", slaveID, numOfPoints, Convert.ToInt32(startAddress.Substring(1))));

											MMaster.WriteMultipleRegisters(slaveID, Convert.ToUInt16(startAddress.Substring(1)), valUShort);
										}
									}
									else
									{
										if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write single holding register {1} starting at address {2} (ushort).", slaveID, valUShort[0], Convert.ToInt32(startAddress.Substring(1, startAddress.Length - 2))));

											MMaster.WriteSingleRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1, startAddress.Length - 2)), valUShort[0]);
										}
										else
										{
											if (EnableMasterMessages)
												MasterRequests(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: Write single holding register {1} starting at address {2} (short).", slaveID, valUShort[0], Convert.ToInt32(startAddress.Substring(1))));

											MMaster.WriteSingleRegister(slaveID, Convert.ToUInt16(startAddress.Substring(1)), valUShort[0]);
										}
									}

									if (EnableSlaveMessages)
										SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Slv{0}: {1}", slaveID, MMaster.Transport.SlaveResponse));
								}
							}
						}
						catch (Exception ex)
						{
							ExceptionsAndActions(ex);

							//Set the corresponding status label back color to Red = Failed
							AddressList[sndrIndex].LabelStatus.BackColor = Color.Red;
							return;
						}
					}
					else
					{
						//Set the corresponding status label back color to Red = Failed
						AddressList[sndrIndex].LabelStatus.BackColor = Color.Red;
						return;
					}
				}
			}

			AddressList[sndrIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[sndrIndex].ValuesToWrite.BackColor = Color.White; });

			//Set the corresponding status label back color to Green = Success
			AddressList[sndrIndex].LabelStatus.BackColor = Color.Green;

			lblMessage.Text = "Comms Okay";
		}

		private void ExceptionsAndActions(Exception ex)
		{
			if (ex is System.NullReferenceException)
			{
				MessageBox.Show(ex.Message + Environment.NewLine + "Make sure the connection is established and with a correct Protocol Type.");
				if (EnableSlaveMessages)
					SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Not connected"));
				lblMessage.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Make sure the connection is established and with a correct Protocol Type");
				return;
			}
			else if (ex is System.OverflowException)
			{
				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = ex.Message; });
			}
			else
			{
				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "No Response or Error"); });
			}

			if (ex is System.InvalidOperationException || ex is System.IO.IOException)
			{
				try
				{
					//Not RTU or ASCIIoverRTU
					if (!(m_ProtocolType == 0 || m_ProtocolType == 5))
					{
						Connect();
					}
				}
				catch (Exception ext)
				{
					MessageBox.Show(ext.Message);
					return;
				}
			}

			if (EnableSlaveMessages)
				SlaveResponses(string.Format(System.Globalization.CultureInfo.InvariantCulture, "No Response or Error"));
		}

		#endregion

		#region "AutoRead"

		private void ExceptionsAndActionsAutoRead(Exception ex)
		{
			if (ex is NullReferenceException)
			{
				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Check connection & Protocol Type"); });
				return;
			}
			else if (ex is OverflowException)
				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = ex.Message; });
			else
				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "No Response or Error"); });

			if (ex is InvalidOperationException || ex is System.IO.IOException)
			{
				try
				{
					if (!(m_ProtocolType == 0 || m_ProtocolType == 5)) //RTU or ASCIIoverRTU
						Connect();
				}
				catch (Exception ext)
				{
					MessageBox.Show(ext.Message);
					return;
				}
			}
		}

		private void CheckBoxAutoRead1_CheckedChanged(object sender, EventArgs e)
		{
			if (chbAutoRead1.Checked)
			{
				for (int i = 0; i < 4; i++)
				{
					if (AddressList[i].CheckBoxRead.Checked)
					{
						PollInfo tmpPI = new PollInfo
						{
							Index = i,
							SlaveID = Convert.ToByte(AddressList[i].SlaveID.SelectedItem.ToString()),
							NumberOfElements = AddressList[i].NumberOfElements.Text
						};

						if (AddressList[i].PlcAddress.Text.StartsWith("0"))
							tmpPI.FunctionCode = "1";
						else if (AddressList[i].PlcAddress.Text.StartsWith("1"))
							tmpPI.FunctionCode = "2";
						else if (AddressList[i].PlcAddress.Text.StartsWith("3"))
							tmpPI.FunctionCode = "4";
						else //PlcAddress starts with "4"
							tmpPI.FunctionCode = "3";

						if (AddressList[i].PlcAddress.Text.IndexOfAny(new char[] { '.' }) != -1)
						{
							var tempAddress = AddressList[i].PlcAddress.Text;

							if (tempAddress.IndexOfAny(new char[] { 'F' }) != -1)
							{
								tmpPI.BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1, (tempAddress.IndexOf("F") - tempAddress.IndexOf(".") - 1)));
								tmpPI.PlcAddress = tempAddress.Substring(0, tempAddress.IndexOf(".")) + tempAddress.Substring(tempAddress.IndexOf("F"));
							}
							else if (tempAddress.IndexOfAny(new char[] { 'U' }) != -1)
							{
								tmpPI.BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1, (tempAddress.IndexOf("U") - tempAddress.IndexOf(".") - 1)));
								tmpPI.PlcAddress = tempAddress.Substring(0, tempAddress.IndexOf(".")) + tempAddress.Substring(tempAddress.IndexOf("U"));
							}
							else if (tempAddress.IndexOfAny(new char[] { 'L' }) != -1)
							{
								tmpPI.BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1, (tempAddress.IndexOf("L") - tempAddress.IndexOf(".") - 1)));
								tmpPI.PlcAddress = tempAddress.Substring(0, tempAddress.IndexOf(".")) + tempAddress.Substring(tempAddress.IndexOf("L"));
							}
							else if (tempAddress.IndexOfAny(new char[] { 'S' }) != -1)
							{
								tmpPI.BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1)) - 1;
								tmpPI.PlcAddress = tempAddress.Substring(0, tempAddress.IndexOf("."));
							}
							else
							{
								tmpPI.BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1));
								tmpPI.PlcAddress = tempAddress.Substring(0, tempAddress.IndexOf("."));
							}
						}
						else
						{
							tmpPI.BitNumber = -1;
							tmpPI.PlcAddress = AddressList[i].PlcAddress.Text;
						}

						if (!AddressCheckPassed(AddressList[i].PlcAddress.Text, tmpPI.BitNumber, Convert.ToInt32(AddressList[i].NumberOfElements.Text), i))
							return;

						Slave1PollAddressList.Add(tmpPI);

						AddressList[i].RadioBtn.Enabled = false;
						AddressList[i].PlcAddress.Enabled = false;
						AddressList[i].CheckBoxRead.Enabled = false;
						if (AddressList[i].PlcAddress.Text.StartsWith("0") || AddressList[i].PlcAddress.Text.StartsWith("4"))
							AddressList[i].CheckBoxWrite.Enabled = false;
						AddressList[i].NumberOfElements.Enabled = false;
						AddressList[i].ButtonSend.Enabled = false;
						AddressList[i].ButtonSend.BackColor = Color.Gainsboro;
					}
				}
				if (Slave1PollAddressList.Count == 0)
				{
					MessageBox.Show("No address selected for automatic reading!");
					chbAutoRead1.Checked = false;
					return;
				}

				if (Slave1AutoReadBckgndThread == null)
				{
					Slave1AutoReadBckgndThread = new Thread(Slave1AutoRead) { IsBackground = true };
					Slave1AutoReadBckgndThread.Start();
				}
			}
			else
			{
				if (SerPort != null)
				{
					if (SerPort.IsOpen)
					{
						SerPort.DiscardInBuffer();
						SerPort.DiscardOutBuffer();
					}
				}
			}
		}

		private void CheckBoxAutoRead2_CheckedChanged(object sender, EventArgs e)
		{
			if (chbAutoRead2.Checked)
			{
				for (int i = 4; i < 8; i++)
				{
					if (AddressList[i].CheckBoxRead.Checked)
					{
						PollInfo tmpPI = new PollInfo
						{
							Index = i,
							SlaveID = Convert.ToByte(AddressList[i].SlaveID.SelectedItem.ToString()),
							NumberOfElements = AddressList[i].NumberOfElements.Text
						};

						if (AddressList[i].PlcAddress.Text.StartsWith("0"))
							tmpPI.FunctionCode = "1";
						else if (AddressList[i].PlcAddress.Text.StartsWith("1"))
							tmpPI.FunctionCode = "2";
						else if (AddressList[i].PlcAddress.Text.StartsWith("3"))
							tmpPI.FunctionCode = "4";
						else //PlcAddress starts with "4"
							tmpPI.FunctionCode = "3";

						if (AddressList[i].PlcAddress.Text.IndexOfAny(new char[] { '.' }) != -1)
						{
							var tempAddress = AddressList[i].PlcAddress.Text;

							if (tempAddress.IndexOfAny(new char[] { 'F' }) != -1)
							{
								tmpPI.BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1, (tempAddress.IndexOf("F") - tempAddress.IndexOf(".") - 1)));
								tmpPI.PlcAddress = tempAddress.Substring(0, tempAddress.IndexOf(".")) + tempAddress.Substring(tempAddress.IndexOf("F"));
							}
							else if (tempAddress.IndexOfAny(new char[] { 'U' }) != -1)
							{
								tmpPI.BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1, (tempAddress.IndexOf("U") - tempAddress.IndexOf(".") - 1)));
								tmpPI.PlcAddress = tempAddress.Substring(0, tempAddress.IndexOf(".")) + tempAddress.Substring(tempAddress.IndexOf("U"));
							}
							else if (tempAddress.IndexOfAny(new char[] { 'L' }) != -1)
							{
								tmpPI.BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1, (tempAddress.IndexOf("L") - tempAddress.IndexOf(".") - 1)));
								tmpPI.PlcAddress = tempAddress.Substring(0, tempAddress.IndexOf(".")) + tempAddress.Substring(tempAddress.IndexOf("L"));
							}
							else if (tempAddress.IndexOfAny(new char[] { 'S' }) != -1)
							{
								tmpPI.BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1)) - 1;
								tmpPI.PlcAddress = tempAddress.Substring(0, tempAddress.IndexOf("."));
							}
							else
							{
								tmpPI.BitNumber = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf(".") + 1));
								tmpPI.PlcAddress = tempAddress.Substring(0, tempAddress.IndexOf("."));
							}
						}
						else
						{
							tmpPI.BitNumber = -1;
							tmpPI.PlcAddress = AddressList[i].PlcAddress.Text;
						}

						if (!AddressCheckPassed(AddressList[i].PlcAddress.Text, tmpPI.BitNumber, Convert.ToInt32(AddressList[i].NumberOfElements.Text), i))
							return;

						Slave2PollAddressList.Add(tmpPI);

						AddressList[i].RadioBtn.Enabled = false;
						AddressList[i].PlcAddress.Enabled = false;
						AddressList[i].CheckBoxRead.Enabled = false;
						if (AddressList[i].PlcAddress.Text.StartsWith("0") || AddressList[i].PlcAddress.Text.StartsWith("4"))
							AddressList[i].CheckBoxWrite.Enabled = false;
						AddressList[i].NumberOfElements.Enabled = false;
						AddressList[i].ButtonSend.Enabled = false;
						AddressList[i].ButtonSend.BackColor = Color.Gainsboro;
					}
				}
				if (Slave2PollAddressList.Count == 0)
				{
					MessageBox.Show("No address selected for automatic reading!");
					chbAutoRead2.Checked = false;
					return;
				}

				if (Slave2AutoReadBckgndThread == null)
				{
					Slave2AutoReadBckgndThread = new Thread(Slave2AutoRead) { IsBackground = true };
					Slave2AutoReadBckgndThread.Start();
				}
			}
			else
			{
				if (SerPort != null)
				{
					if (SerPort.IsOpen)
					{
						SerPort.DiscardInBuffer();
						SerPort.DiscardOutBuffer();
					}
				}
			}
		}

		private void Slave1AutoRead()
		{
			EventWaitHandle Update1HoldRelease = new EventWaitHandle(false, EventResetMode.AutoReset);
			int i = 0;
			int holdTime1 = 0;
			int pollInterval1 = 0;

			cbPollInterval1.Invoke((MethodInvoker)delegate { pollInterval1 = Convert.ToInt32(cbPollInterval1.SelectedItem.ToString()); });

			while (chbAutoRead1.Checked)
			{
				try
				{
					if (pollInterval1 > 0) // Allow 5ms for each read
						holdTime1 = Convert.ToInt32(Math.Floor((double)(pollInterval1 / Slave1PollAddressList.Count))) - 5;

					i = 0;

					while (i < Slave1PollAddressList.Count)
					{
						if (ConnectionBckgndThread == null)
						{
							try
							{
								if (MMaster != null)
								{
									if (Slave1PollAddressList[i].FunctionCode == "1")
										UpdateCoils(Slave1PollAddressList[i].SlaveID, Slave1PollAddressList[i].PlcAddress, Convert.ToInt32(Slave1PollAddressList[i].NumberOfElements), Slave1PollAddressList[i].Index);
									else if (Slave1PollAddressList[i].FunctionCode == "2")
										UpdateInputs(Slave1PollAddressList[i].SlaveID, Slave1PollAddressList[i].PlcAddress, Convert.ToInt32(Slave1PollAddressList[i].NumberOfElements), Slave1PollAddressList[i].Index);
									else if (Slave1PollAddressList[i].FunctionCode == "3")
										UpdateHoldingRegisters("Slave1", Slave1PollAddressList[i].SlaveID, Slave1PollAddressList[i].PlcAddress, Convert.ToInt32(Slave1PollAddressList[i].NumberOfElements), Slave1PollAddressList[i].Index, i, false);
									else
										UpdateInputRegisters("Slave1", Slave1PollAddressList[i].SlaveID, Slave1PollAddressList[i].PlcAddress, Convert.ToInt32(Slave1PollAddressList[i].NumberOfElements), Slave1PollAddressList[i].Index, i);
								}
								else
								{
									AddressList[Slave1PollAddressList[i].Index].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[Slave1PollAddressList[i].Index].ValuesToWrite.Text = "Not connected"; });
									lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "Not connected"; });
									Update1HoldRelease.WaitOne(500);
									break;
								}
							}
							catch (Exception ex)
							{
								ExceptionsAndActionsAutoRead(ex);
								break;
							}

							if (holdTime1 > 0)
								Update1HoldRelease.WaitOne(holdTime1);
						}
						else
							Update1HoldRelease.WaitOne(500);

						i += 1;
					}
				}
				catch (Exception)
				{
					//Do nothing
				}
			}

			if (Slave1PollAddressList.Count > 0)
			{
				foreach (PollInfo item in Slave1PollAddressList)
				{
					AddressList[item.Index].RadioBtn.Invoke((MethodInvoker)delegate { AddressList[item.Index].RadioBtn.Enabled = true; });
					AddressList[item.Index].PlcAddress.Invoke((MethodInvoker)delegate { AddressList[item.Index].PlcAddress.Enabled = true; });
					AddressList[item.Index].CheckBoxRead.Invoke((MethodInvoker)delegate { AddressList[item.Index].CheckBoxRead.Enabled = true; });
					if (AddressList[item.Index].PlcAddress.Text.StartsWith("0") || AddressList[item.Index].PlcAddress.Text.StartsWith("4"))
						AddressList[item.Index].CheckBoxWrite.Invoke((MethodInvoker)delegate { AddressList[item.Index].CheckBoxWrite.Enabled = true; });
					AddressList[item.Index].NumberOfElements.Invoke((MethodInvoker)delegate { AddressList[item.Index].NumberOfElements.Enabled = true; });
					if (AddressList[item.Index].PlcAddress.Text.IndexOfAny(new char[] { '.' }) != -1)
						AddressList[item.Index].NumberOfElements.Invoke((MethodInvoker)delegate { AddressList[item.Index].NumberOfElements.BackColor = Color.Yellow; });
					else
						AddressList[item.Index].NumberOfElements.Invoke((MethodInvoker)delegate { AddressList[item.Index].NumberOfElements.BackColor = Color.White; });
					AddressList[item.Index].ButtonSend.Invoke((MethodInvoker)delegate { AddressList[item.Index].ButtonSend.Enabled = true; });
					AddressList[item.Index].ButtonSend.Invoke((MethodInvoker)delegate { AddressList[item.Index].ButtonSend.BackColor = Color.LightSteelBlue; });
				}

				Slave1PollAddressList.Clear();
			}

			Slave1AutoReadBckgndThread = null;
		}

		private void Slave2AutoRead()
		{
			EventWaitHandle Update2HoldRelease = new EventWaitHandle(false, EventResetMode.AutoReset);
			int j = 0;
			int holdTime2 = 0;
			int pollInterval2 = 0;

			cbPollInterval2.Invoke((MethodInvoker)delegate { pollInterval2 = Convert.ToInt32(cbPollInterval2.SelectedItem.ToString()); });

			while (chbAutoRead2.Checked)
			{
				try
				{
					if (pollInterval2 > 0) // Allow 5ms for each read
						holdTime2 = Convert.ToInt32(Math.Floor((double)(pollInterval2 / Slave2PollAddressList.Count))) - 5;

					j = 0;

					while (j < Slave2PollAddressList.Count)
					{
						if (ConnectionBckgndThread == null)
						{
							try
							{
								if (MMaster != null)
								{
									if (Slave2PollAddressList[j].FunctionCode == "1")
										UpdateCoils(Slave2PollAddressList[j].SlaveID, Slave2PollAddressList[j].PlcAddress, Convert.ToInt32(Slave2PollAddressList[j].NumberOfElements), Slave2PollAddressList[j].Index);
									else if (Slave2PollAddressList[j].FunctionCode == "2")
										UpdateInputs(Slave2PollAddressList[j].SlaveID, Slave2PollAddressList[j].PlcAddress, Convert.ToInt32(Slave2PollAddressList[j].NumberOfElements), Slave2PollAddressList[j].Index);
									else if (Slave2PollAddressList[j].FunctionCode == "3")
										UpdateHoldingRegisters("Slave2", Slave2PollAddressList[j].SlaveID, Slave2PollAddressList[j].PlcAddress, Convert.ToInt32(Slave2PollAddressList[j].NumberOfElements), Slave2PollAddressList[j].Index, j, false);
									else
										UpdateInputRegisters("Slave2", Slave2PollAddressList[j].SlaveID, Slave2PollAddressList[j].PlcAddress, Convert.ToInt32(Slave2PollAddressList[j].NumberOfElements), Slave2PollAddressList[j].Index, j);
								}
								else
								{
									AddressList[Slave2PollAddressList[j].Index].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[Slave2PollAddressList[j].Index].ValuesToWrite.Text = "Not connected"; });
									lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "Not connected"; });
									Update2HoldRelease.WaitOne(500);
									break;
								}
							}
							catch (Exception ex)
							{
								ExceptionsAndActionsAutoRead(ex);
								break;
							}

							if (holdTime2 > 0)
								Update2HoldRelease.WaitOne(holdTime2);
						}
						else
							Update2HoldRelease.WaitOne(500);

						j += 1;
					}
				}
				catch (Exception)
				{
					//Do nothing
				}
			}

			if (Slave2PollAddressList.Count > 0)
			{
				foreach (PollInfo item in Slave2PollAddressList)
				{
					AddressList[item.Index].RadioBtn.Invoke((MethodInvoker)delegate { AddressList[item.Index].RadioBtn.Enabled = true; });
					AddressList[item.Index].PlcAddress.Invoke((MethodInvoker)delegate { AddressList[item.Index].PlcAddress.Enabled = true; });
					AddressList[item.Index].CheckBoxRead.Invoke((MethodInvoker)delegate { AddressList[item.Index].CheckBoxRead.Enabled = true; });
					if (AddressList[item.Index].PlcAddress.Text.StartsWith("0") || AddressList[item.Index].PlcAddress.Text.StartsWith("4"))
						AddressList[item.Index].CheckBoxWrite.Invoke((MethodInvoker)delegate { AddressList[item.Index].CheckBoxWrite.Enabled = true; });
					AddressList[item.Index].NumberOfElements.Invoke((MethodInvoker)delegate { AddressList[item.Index].NumberOfElements.Enabled = true; });
					if (AddressList[item.Index].PlcAddress.Text.IndexOfAny(new char[] { '.' }) != -1)
						AddressList[item.Index].NumberOfElements.Invoke((MethodInvoker)delegate { AddressList[item.Index].NumberOfElements.BackColor = Color.Yellow; });
					else
						AddressList[item.Index].NumberOfElements.Invoke((MethodInvoker)delegate { AddressList[item.Index].NumberOfElements.BackColor = Color.White; });
					AddressList[item.Index].ButtonSend.Invoke((MethodInvoker)delegate { AddressList[item.Index].ButtonSend.Enabled = true; });
					AddressList[item.Index].ButtonSend.Invoke((MethodInvoker)delegate { AddressList[item.Index].ButtonSend.BackColor = Color.LightSteelBlue; });
				}

				Slave2PollAddressList.Clear();
			}

			Slave2AutoReadBckgndThread = null;
		}

		private void UpdateCoils(byte SlaveID, string startAddress, int numOfPoints, int AddressListIndex)
		{
			var responsesBoolean = new bool[] { };

			try
			{
				if (MMaster != null)
					responsesBoolean = MMaster.ReadCoils(SlaveID, Convert.ToUInt16(Convert.ToInt32(startAddress)), Convert.ToUInt16(numOfPoints));
				else
				{
					AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "Not connected"; });
					//Set the corresponding status label back color to Red = Failed
					AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Red; });

					lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "Not connected"; });
					return;
				}
			}
			catch (Exception ex)
			{
				ExceptionsAndActionsAutoRead(ex);
				AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "No Response or Error"; });
				//Set the corresponding status label back color to Red = Failed
				AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Red; });

				if (!(m_ProtocolType == 0 || m_ProtocolType == 5)) //RTU or ASCIIoverRTU
					Connect();

				return;
			}

			if (responsesBoolean.Length > 0)
			{
				string strCoils = "";

				for (int k = 0; k < responsesBoolean.Length; k++)
				{
					strCoils += responsesBoolean[k].ToString();

					if (k != responsesBoolean.Length - 1)
						strCoils += ", ";
				}

				AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "{ " + strCoils + " }"; });
				//Set the corresponding status label back color to Green = Success
				AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Green; });

				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "Comms Okay"; });
			}
			else
			{
				AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "No Response or Error"; });
				//Set the corresponding status label back color to Red = Failed
				AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Red; });

				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "No Response or Error"; });
			}
		}

		private void UpdateInputs(byte SlaveID, string startAddress, int numOfPoints, int AddressListIndex)
		{
			var responsesBoolean = new bool[] { };

			try
			{
				if (MMaster != null)
					responsesBoolean = MMaster.ReadInputs(SlaveID, Convert.ToUInt16(Convert.ToInt32(startAddress.Substring(1))), Convert.ToUInt16(numOfPoints));
				else
				{
					AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "Not connected"; });
					//Set the corresponding status label back color to Red = Failed
					AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Red; });

					lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "Not connected"; });
					return;
				}
			}
			catch (Exception ex)
			{
				ExceptionsAndActionsAutoRead(ex);
				AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "No Response or Error"; });
				//Set the corresponding status label back color to Red = Failed
				AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Red; });

				if (!(m_ProtocolType == 0 || m_ProtocolType == 5)) //RTU or ASCIIoverRTU
					Connect();

				return;
			}

			if (responsesBoolean.Length > 0)
			{
				string strInputs = "";

				for (int k = 0; k < responsesBoolean.Length; k++)
				{
					strInputs += responsesBoolean[k].ToString();

					if (k != responsesBoolean.Length - 1)
						strInputs += ", ";
				}

				AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "{ " + strInputs + " }"; });
				//Set the corresponding status label back color to Green = Success
				AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Green; });

				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "Comms Okay"; });
			}
			else
			{
				AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "No Response or Error"; });
				//Set the corresponding status label back color to Red = Failed
				AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Red; });

				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "No Response or Error"; });
			}
		}

		private void UpdateInputRegisters(string SlaveTag, byte SlaveID, string startAddress, int numOfPoints, int AddressListIndex, int SlaveIndex)
		{
			var responsesUShort = new ushort[] { };
			var valueSingle = new float[] { };
			var valueDouble = new double[] { };

			var fullAddress = AddressList[AddressListIndex].PlcAddress.Text;
			int numRegisters = 0;
			int registerAddress = 0;

			int requestedBitNumber = 0;
			if (SlaveTag == "Slave1")
				requestedBitNumber = Slave1PollAddressList[SlaveIndex].BitNumber;
			else
				requestedBitNumber = Slave2PollAddressList[SlaveIndex].BitNumber;

			try
			{
				if (MMaster != null)
				{
					// *** Read Values ***

					if (startAddress.IndexOfAny(new char[] { 'O' }) != -1)
					{
						if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LO
						{
							registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1));

							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
							{
								if (chbBitReading.Checked)
									responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), 8);
								else
									responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(8 * numOfPoints));
							}
							else
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(8 * numOfPoints));
						}
						else //UO
						{
							registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1));

							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
							{
								if (chbBitReading.Checked)
									responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), 8);
								else
									responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(8 * numOfPoints));
							}
							else
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(8 * numOfPoints));
						}
					}
					else if (startAddress.IndexOfAny(new char[] { 'Q' }) != -1)
					{
						if (startAddress.IndexOfAny(new char[] { 'F' }) != -1) //FQ
						{
							registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("F") - 1));
							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
							{
								if (chbBitReading.Checked)
									valueDouble = MMaster.ReadFloat64InputRegisters(SlaveID, Convert.ToUInt16(registerAddress), 1, chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));
								else
									valueDouble = MMaster.ReadFloat64InputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));
							}
							else
								valueDouble = MMaster.ReadFloat64InputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));
						}
						else if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LQ
						{
							registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1));

							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
							{
								if (chbBitReading.Checked)
									responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), 4);
								else
									responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(4 * numOfPoints));
							}
							else
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(4 * numOfPoints));
						}
						else //UQ
						{
							registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1));
							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
							{
								if (chbBitReading.Checked)
									responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), 4);
								else
									responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(4 * numOfPoints));
							}
							else
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(4 * numOfPoints));
						}
					}
					else if (startAddress.IndexOfAny(new char[] { 'S' }) != -1)
					{
						numRegisters = Convert.ToInt32(startAddress.Substring(startAddress.IndexOf("S") + 1));
						registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("S") - 1));

						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numRegisters));
							else
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints * numRegisters));
						}
						else
							responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints * numRegisters));
					}
					else if (startAddress.IndexOfAny(new char[] { 'F' }) != -1)
					{
						registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("F") - 1));

						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
								valueSingle = MMaster.ReadFloat32InputRegisters(SlaveID, Convert.ToUInt16(registerAddress), 1, chbSwapBytes.Checked, chbSwapWords.Checked);
							else
								valueSingle = MMaster.ReadFloat32InputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked);
						}
						else
							valueSingle = MMaster.ReadFloat32InputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked);
					}
					else if (startAddress.IndexOfAny(new char[] { 'U' }) != -1 && startAddress.IndexOfAny(new char[] { 'L' }) != -1)
					{
						registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1));

						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), 2);
							else
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(2 * numOfPoints));
						}
						else
							responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(2 * numOfPoints));
					}
					else if (startAddress.IndexOfAny(new char[] { 'L' }) != -1)
					{
						registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1));
						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), 2);
							else
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(2 * numOfPoints));
						}
						else
							responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(2 * numOfPoints));
					}
					else if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
					{
						registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1));
						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), 1);
							else
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints));
						}
						else
							responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints));
					}
					else //startAddress starts with "3" with no modifiers
					{
						registerAddress = Convert.ToInt32(startAddress.Substring(1));
						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), 1);
							else
								responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints));
						}
						else
							responsesUShort = MMaster.ReadInputRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints));
					}
				}
				else
				{
					AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "Not connected"; });
					//Set the corresponding status label back color to Red = Failed
					AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Red; });

					lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "Not connected"; });
					return;
				}
			}
			catch (Exception ex)
			{
				ExceptionsAndActionsAutoRead(ex);
				AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "No Response or Error"; });
				//Set the corresponding status label back color to Red = Failed
				AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Red; });

				if (!(m_ProtocolType == 0 || m_ProtocolType == 5)) //RTU or ASCIIoverRTU
					Connect();

				return;
			}

			string strInputRegisters = ""; //String to be displayed

			// *** Process Read Values ***
			if (responsesUShort.Length > 0)
			{
				if (!chbSwapBytes.Checked)
					responsesUShort = SwapBytesFunction(responsesUShort);

				if (startAddress.IndexOfAny(new char[] { 'O' }) != -1)
                {
					if (chbSwapWords.Checked)
						responsesUShort = SwapWords128Function(responsesUShort);

					if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LO
					{
						byte[] valueBytes = new byte[16];

						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
							{
								for (int j = 0; j < 8; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								string binaryString = "";

								for (int k = 15; k > -1; k -= 1)
								{
									binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
								}

								for (int i = 0; i < numOfPoints; i++)
								{
									strInputRegisters += ExtractInt128Bit(binaryString, requestedBitNumber + i);

									if (i != numOfPoints - 1)
										strInputRegisters += ", ";
								}
							}
							else
							{
								for (int i = 0; i < numOfPoints; i++)
								{
									for (int j = 0; j < 8; j++)
									{
										var bytes = BitConverter.GetBytes(responsesUShort[i * 8 + j]);
										valueBytes[j * 2] = bytes[0];
										valueBytes[j * 2 + 1] = bytes[1];
									}

									string binaryString = "";

									for (int k = 15; k > -1; k -= 1)
									{
										binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
									}

									strInputRegisters += ExtractInt128Bit(binaryString, requestedBitNumber);

									if (i != numOfPoints - 1)
										strInputRegisters += ", ";
								}
							}
						}
						else
						{
							for (int i = 0; i < numOfPoints; i++)
							{
								for (int j = 0; j < 8; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[i * 8 + j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								string binaryString = "";

								for (int k = 15; k > -1; k -= 1)
								{
									binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
								}

								strInputRegisters += BitConverterInt128(binaryString).ToString();

								if (i != numOfPoints - 1)
									strInputRegisters += ", ";
							}
						}
					}
					else //UO
					{
						byte[] valueBytes = new byte[16];

						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
							{
								for (int j = 0; j < 8; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								string binaryString = "";

								for (int k = 15; k > -1; k -= 1)
								{
									binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
								}

								for (int i = 0; i < numOfPoints; i++)
								{
									strInputRegisters += ExtractInt128Bit(binaryString, requestedBitNumber + i);

									if (i != numOfPoints - 1)
										strInputRegisters += ", ";
								}
							}
							else
							{
								for (int i = 0; i < numOfPoints; i++)
								{
									for (int j = 0; j < 8; j++)
									{
										var bytes = BitConverter.GetBytes(responsesUShort[i * 8 + j]);
										valueBytes[j * 2] = bytes[0];
										valueBytes[j * 2 + 1] = bytes[1];
									}

									string binaryString = "";

									for (int k = 15; k > -1; k -= 1)
									{
										binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
									}

									strInputRegisters += ExtractInt128Bit(binaryString, requestedBitNumber);

									if (i != numOfPoints - 1)
										strInputRegisters += ", ";
								}
							}
						}
						else
						{
							for (int i = 0; i < numOfPoints; i++)
							{
								for (int j = 0; j < 8; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[i * 8 + j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								string binaryString = "";

								for (int k = 15; k > -1; k -= 1)
								{
									binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
								}

								strInputRegisters += BitConverterUInt128(binaryString).ToString();

								if (i != numOfPoints - 1)
									strInputRegisters += ", ";
							}
						}
					}
				}
				else if (startAddress.IndexOfAny(new char[] { 'Q' }) != -1)
				{
					if (chbSwapWords.Checked)
						responsesUShort = SwapWords64Function(responsesUShort);

					if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LQ
					{
						byte[] valueBytes = new byte[8];

						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
							{
								for (int j = 0; j < 4; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								for (int i = 0; i < numOfPoints; i++)
								{
									strInputRegisters += ExtractInt64Bit(BitConverter.ToInt64(valueBytes, 0), requestedBitNumber + i);

									if (i != numOfPoints - 1)
										strInputRegisters += ", ";
								}
							}
							else
							{
								for (int i = 0; i < numOfPoints; i++)
								{
									for (int j = 0; j < 4; j++)
									{
										var bytes = BitConverter.GetBytes(responsesUShort[i * 4 + j]);
										valueBytes[j * 2] = bytes[0];
										valueBytes[j * 2 + 1] = bytes[1];
									}

									strInputRegisters += ExtractInt64Bit(BitConverter.ToInt64(valueBytes, 0), requestedBitNumber);

									if (i != numOfPoints - 1)
										strInputRegisters += ", ";
								}
							}
						}
						else
						{
							for (int i = 0; i < numOfPoints; i++)
							{
								for (int j = 0; j < 4; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[i * 4 + j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								strInputRegisters += BitConverter.ToInt64(valueBytes, 0).ToString();

								if (i != numOfPoints - 1)
									strInputRegisters += ", ";
							}
						}
					}
					else //UQ
					{
						byte[] valueBytes = new byte[8];

						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
							{
								for (int j = 0; j < 4; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								for (int i = 0; i < numOfPoints; i++)
								{
									strInputRegisters += ExtractInt64Bit(Convert.ToInt64(BitConverter.ToUInt64(valueBytes, 0)), requestedBitNumber + i);

									if (i != numOfPoints - 1)
										strInputRegisters += ", ";
								}
							}
							else
							{
								for (int i = 0; i < numOfPoints; i++)
								{
									for (int j = 0; j < 4; j++)
									{
										var bytes = BitConverter.GetBytes(responsesUShort[i * 4 + j]);
										valueBytes[j * 2] = bytes[0];
										valueBytes[j * 2 + 1] = bytes[1];
									}

									strInputRegisters += ExtractInt64Bit(Convert.ToInt64(BitConverter.ToUInt64(valueBytes, 0)), requestedBitNumber);

									if (i != numOfPoints - 1)
										strInputRegisters += ", ";
								}
							}
						}
						else
						{
							for (int i = 0; i < numOfPoints; i++)
							{
								for (int j = 0; j < 4; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[i * 4 + j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								strInputRegisters += BitConverter.ToUInt64(valueBytes, 0).ToString();

								if (i != numOfPoints - 1)
									strInputRegisters += ", ";
							}
						}
					}
				}
				else if (startAddress.IndexOfAny(new char[] { 'S' }) != -1)
				{
					string[] intValues = null;
					if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
					{
						intValues = new string[1];
						for (int i = 0; i < numOfPoints; i++)
						{
							if (chbBitReading.Checked)
								intValues[0] = BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[requestedBitNumber + i]), 0).ToString();
							else
								intValues[0] = BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[requestedBitNumber + i * numRegisters]), 0).ToString();

							strInputRegisters += ConvertStringOfIntegersToString(intValues);

							if (i != numOfPoints - 1)
								strInputRegisters += ", ";
						}
					}
					else
					{
						for (int i = 0; i < numOfPoints; i++)
						{
							intValues = new string[numRegisters];

							for (int j = 0; j < numRegisters; j++)
							{
								intValues[j] = BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[j + i * numRegisters]), 0).ToString();
							}

							strInputRegisters += ConvertStringOfIntegersToString(intValues);

							if (i != numOfPoints - 1)
								strInputRegisters += ", ";
						}
					}
				}
				else if (startAddress.IndexOfAny(new char[] { 'L' }) != -1)
				{
					if (chbSwapWords.Checked)
						responsesUShort = SwapWordsFunction(responsesUShort);

					byte[] valueBytes = new byte[4];

					if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
					{
						if (chbBitReading.Checked)
						{
							for (int j = 0; j < 2; j++)
							{
								var bytes = BitConverter.GetBytes(responsesUShort[j]);
								valueBytes[j * 2] = bytes[0];
								valueBytes[j * 2 + 1] = bytes[1];
							}

							for (int i = 0; i < numOfPoints; i++)
							{
								if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
									strInputRegisters += ExtractInt32Bit(Convert.ToInt32(BitConverter.ToUInt32(valueBytes, 0)), requestedBitNumber + i);
								else
									strInputRegisters += ExtractInt32Bit(BitConverter.ToInt32(valueBytes, 0), requestedBitNumber + i);

								if (i != numOfPoints - 1)
									strInputRegisters += ", ";
							}
						}
						else
						{
							for (int i = 0; i < numOfPoints; i++)
							{
								for (int j = 0; j < 2; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[i * 2 + j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
									strInputRegisters += ExtractInt32Bit(Convert.ToInt32(BitConverter.ToUInt32(valueBytes, 0)), requestedBitNumber);
								else
									strInputRegisters += ExtractInt32Bit(BitConverter.ToInt32(valueBytes, 0), requestedBitNumber);

								if (i != numOfPoints - 1)
									strInputRegisters += ", ";
							}
						}
					}
					else
					{
						for (int i = 0; i < numOfPoints; i++)
						{
							for (int j = 0; j < 2; j++)
							{
								var bytes = BitConverter.GetBytes(responsesUShort[i * 2 + j]);
								valueBytes[j * 2] = bytes[0];
								valueBytes[j * 2 + 1] = bytes[1];
							}

							if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
								strInputRegisters += BitConverter.ToUInt32(valueBytes, 0).ToString();
							else
								strInputRegisters += BitConverter.ToInt32(valueBytes, 0).ToString();

							if (i != numOfPoints - 1)
								strInputRegisters += ", ";
						}
					}
				}
				else
				{
					for (int k = 0; k < numOfPoints; k++)
					{
						if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
						{
							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
							{
								if (chbBitReading.Checked)
									strInputRegisters += ExtractInt32Bit(responsesUShort[0], requestedBitNumber + k);
								else
									strInputRegisters += ExtractInt32Bit(responsesUShort[k], requestedBitNumber);
							}
							else
							{
								strInputRegisters += responsesUShort[k].ToString();
							}
						}
						else
						{
							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
							{
								if (chbBitReading.Checked)
									strInputRegisters += ExtractInt32Bit(BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[0]), 0), requestedBitNumber + k);
								else
									strInputRegisters += ExtractInt32Bit(BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[k]), 0), requestedBitNumber);
							}
							else
							{
								strInputRegisters += BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[k]), 0).ToString();
							}
						}

						if (k != numOfPoints - 1)
							strInputRegisters += ", ";
					}
				}
			}
			else if (valueSingle.Length > 0)
			{
				for (int k = 0; k < numOfPoints; k++)
				{
					if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
					{
						if (chbBitReading.Checked)
							strInputRegisters += ExtractInt32Bit(BitConverter.ToInt32(BitConverter.GetBytes(valueSingle[0]), 0), requestedBitNumber + k);
						else
							strInputRegisters += ExtractInt32Bit(BitConverter.ToInt32(BitConverter.GetBytes(valueSingle[k]), 0), requestedBitNumber);
					}
					else
					{
						strInputRegisters += valueSingle[k].ToString();
					}

					if (k != numOfPoints - 1)
						strInputRegisters += ", ";
				}
			}
			else if (valueDouble.Length > 0)
			{
				for (int k = 0; k < numOfPoints; k++)
				{
					if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
					{
						if (chbBitReading.Checked)
							strInputRegisters += ExtractInt64Bit(BitConverter.ToInt64(BitConverter.GetBytes(valueDouble[0]), 0), requestedBitNumber + k);
						else
							strInputRegisters += ExtractInt64Bit(BitConverter.ToInt64(BitConverter.GetBytes(valueDouble[k]), 0), requestedBitNumber);
					}
					else
					{
						strInputRegisters += valueDouble[k].ToString();
					}

					if (k != numOfPoints - 1)
						strInputRegisters += ", ";
				}
			}
			else
			{
				AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "No Response or Error"; });
				//Set the corresponding status label back color to Red = Failed
				AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Red; });

				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "No Response or Error"; });
				return;
			}

			AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "{ " + strInputRegisters + " }"; });
			//Set the corresponding status label back color to Green = Success
			AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Green; });

			lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "Comms Okay"; });
		}

		private void UpdateHoldingRegisters(string SlaveTag, byte SlaveID, string startAddress, int numOfPoints, int AddressListIndex, int SlaveIndex, bool externalRead)
		{
			var responsesUShort = new ushort[] { };
			var valueSingle = new float[] { };
			var valueDouble = new double[] { };

			var fullAddress = AddressList[AddressListIndex].PlcAddress.Text;
			int numRegisters = 0;
			int registerAddress = 0;

			int requestedBitNumber = 0;
			if (SlaveTag == "Slave1")
			{
				if (!externalRead)
					requestedBitNumber = Slave1PollAddressList[SlaveIndex].BitNumber;
			}
			else
			{
				if (!externalRead)
					requestedBitNumber = Slave2PollAddressList[SlaveIndex].BitNumber;
			}

			try
			{
				if (MMaster != null)
				{
					// *** Read Values ***

					if (startAddress.IndexOfAny(new char[] { 'O' }) != -1)
                    {
						if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LO
						{
							registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1));

							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
							{
								if (chbBitReading.Checked)
									responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), 8);
								else
									responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(8 * numOfPoints));
							}
							else
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(8 * numOfPoints));
						}
						else //UO
						{
							registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1));

							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
							{
								if (chbBitReading.Checked)
									responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), 8);
								else
									responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(8 * numOfPoints));
							}
							else
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(8 * numOfPoints));
						}
					}
					else if (startAddress.IndexOfAny(new char[] { 'Q' }) != -1)
					{
						if (startAddress.IndexOfAny(new char[] { 'F' }) != -1) //FQ
						{
							registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("F") - 1));
							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
							{
								if (chbBitReading.Checked)
									valueDouble = MMaster.ReadFloat64HoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), 1, chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));
								else
									valueDouble = MMaster.ReadFloat64HoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));
							}
							else
								valueDouble = MMaster.ReadFloat64HoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked, Convert.ToUInt16(m_cbSwapWords64bitSelectedIndex));
						}
						else if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LQ
						{
							registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1));
							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
							{
								if (chbBitReading.Checked)
									responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), 4);
								else
									responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(4 * numOfPoints));
							}
							else
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(4 * numOfPoints));
						}
						else //UQ
						{
							registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1));
							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
							{
								if (chbBitReading.Checked)
									responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), 4);
								else
									responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(4 * numOfPoints));
							}
							else
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(4 * numOfPoints));
						}
					}
					else if (startAddress.IndexOfAny(new char[] { 'S' }) != -1)
					{
						numRegisters = Convert.ToInt32(startAddress.Substring(startAddress.IndexOf("S") + 1));
						registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("S") - 1));
						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numRegisters));
							else
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints * numRegisters));
						}
						else
							responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints * numRegisters));
					}
					else if (startAddress.IndexOfAny(new char[] { 'F' }) != -1)
					{
						registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("F") - 1));
						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
								valueSingle = MMaster.ReadFloat32HoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), 1, chbSwapBytes.Checked, chbSwapWords.Checked);
							else
								valueSingle = MMaster.ReadFloat32HoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked);
						}
						else
							valueSingle = MMaster.ReadFloat32HoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints), chbSwapBytes.Checked, chbSwapWords.Checked);
					}
					else if (startAddress.IndexOfAny(new char[] { 'U' }) != -1 && startAddress.IndexOfAny(new char[] { 'L' }) != -1)
					{
						registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1));
						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), 2);
							else
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(2 * numOfPoints));
						}
						else
							responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(2 * numOfPoints));
					}
					else if (startAddress.IndexOfAny(new char[] { 'L' }) != -1)
					{
						registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("L") - 1));
						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), 2);
							else
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(2 * numOfPoints));
						}
						else
							responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(2 * numOfPoints));
					}
					else if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
					{
						registerAddress = Convert.ToInt32(startAddress.Substring(1, startAddress.IndexOf("U") - 1));
						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), 1);
							else
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints));
						}
						else
							responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints));
					}
					else //startAddress starts with "4" with no modifiers
					{
						registerAddress = Convert.ToInt32(startAddress.Substring(1));
						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1)
						{
							if (chbBitReading.Checked)
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), 1);
							else
								responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints));
						}
						else
							responsesUShort = MMaster.ReadHoldingRegisters(SlaveID, Convert.ToUInt16(registerAddress), Convert.ToUInt16(numOfPoints));
					}
				}
				else
				{
					AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "Not connected"; });
					//Set the corresponding status label back color to Red = Failed
					AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Red; });

					lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "Not connected"; });
					return;
				}
			}
			catch (Exception ex)
			{
				ExceptionsAndActionsAutoRead(ex);
				AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "No Response or Error"; });
				//Set the corresponding status label back color to Red = Failed
				AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Red; });

				if (!(m_ProtocolType == 0 || m_ProtocolType == 5)) //RTU or ASCIIoverRTU
					Connect();

				return;
			}

			string strHoldingRegisters = "";

			// *** Process Read Values ***
			if (responsesUShort.Length > 0)
			{
				if (!chbSwapBytes.Checked)
					responsesUShort = SwapBytesFunction(responsesUShort);

				if (startAddress.IndexOfAny(new char[] { 'O' }) != -1)
				{
					if (chbSwapWords.Checked)
						responsesUShort = SwapWords128Function(responsesUShort);

					if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LO
					{
						byte[] valueBytes = new byte[16];

						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1 && !externalRead)
						{
							if (chbBitReading.Checked)
							{
								for (int j = 0; j < 8; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								string binaryString = "";

								for (int k = 15; k > -1; k -= 1)
								{
									binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
								}

								for (int i = 0; i < numOfPoints; i++)
								{
									strHoldingRegisters += ExtractInt128Bit(binaryString, requestedBitNumber + i);

									if (i != numOfPoints - 1)
										strHoldingRegisters += ", ";
								}
							}
							else
							{
								for (int i = 0; i < numOfPoints; i++)
								{
									for (int j = 0; j < 8; j++)
									{
										var bytes = BitConverter.GetBytes(responsesUShort[i * 8 + j]);
										valueBytes[j * 2] = bytes[0];
										valueBytes[j * 2 + 1] = bytes[1];
									}

									string binaryString = "";

									for (int k = 15; k > -1; k -= 1)
									{
										binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
									}

									strHoldingRegisters += ExtractInt128Bit(binaryString, requestedBitNumber);

									if (i != numOfPoints - 1)
										strHoldingRegisters += ", ";
								}
							}
						}
						else
						{
							for (int i = 0; i < numOfPoints; i++)
							{
								for (int j = 0; j < 8; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[i * 8 + j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								string binaryString = "";

								for (int k = 15; k > -1; k -= 1)
								{
									binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
								}

								if (externalRead)
									strHoldingRegisters += BitConverterUInt128(binaryString).ToString();
								else
									strHoldingRegisters += BitConverterInt128(binaryString).ToString();

								if (i != numOfPoints - 1)
									strHoldingRegisters += ", ";
							}
						}
					}
					else //UO
					{
						byte[] valueBytes = new byte[16];

						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1 && !externalRead)
						{
							if (chbBitReading.Checked)
							{
								for (int j = 0; j < 8; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								string binaryString = "";

								for (int k = 15; k > -1; k -= 1)
								{
									binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
								}

								for (int i = 0; i < numOfPoints; i++)
								{
									strHoldingRegisters += ExtractInt128Bit(binaryString, requestedBitNumber + i);

									if (i != numOfPoints - 1)
										strHoldingRegisters += ", ";
								}
							}
							else
							{
								for (int i = 0; i < numOfPoints; i++)
								{
									for (int j = 0; j < 8; j++)
									{
										var bytes = BitConverter.GetBytes(responsesUShort[i * 8 + j]);
										valueBytes[j * 2] = bytes[0];
										valueBytes[j * 2 + 1] = bytes[1];
									}

									string binaryString = "";

									for (int k = 15; k > -1; k -= 1)
									{
										binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
									}

									strHoldingRegisters += ExtractInt128Bit(binaryString, requestedBitNumber);

									if (i != numOfPoints - 1)
										strHoldingRegisters += ", ";
								}
							}
						}
						else
						{
							for (int i = 0; i < numOfPoints; i++)
							{
								for (int j = 0; j < 8; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[i * 8 + j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								string binaryString = "";

								for (int k = 15; k > -1; k -= 1)
								{
									binaryString += Convert.ToString(valueBytes[k], 2).PadLeft(8, '0');
								}

								strHoldingRegisters += BitConverterUInt128(binaryString).ToString();

								if (i != numOfPoints - 1)
									strHoldingRegisters += ", ";
							}
						}
					}
				}
				else if (startAddress.IndexOfAny(new char[] { 'Q' }) != -1)
				{
					if (chbSwapWords.Checked)
						responsesUShort = SwapWords64Function(responsesUShort);

					if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LQ
					{
						byte[] valueBytes = new byte[8];

						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1 & !externalRead)
						{
							if (chbBitReading.Checked)
							{
								for (int j = 0; j < 4; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								for (int i = 0; i < numOfPoints; i++)
								{
									strHoldingRegisters += ExtractInt64Bit(BitConverter.ToInt64(valueBytes, 0), requestedBitNumber + i);

									if (i != numOfPoints - 1)
										strHoldingRegisters += ", ";
								}
							}
							else
							{
								for (int i = 0; i < numOfPoints; i++)
								{
									for (int j = 0; j < 4; j++)
									{
										var bytes = BitConverter.GetBytes(responsesUShort[i * 4 + j]);
										valueBytes[j * 2] = bytes[0];
										valueBytes[j * 2 + 1] = bytes[1];
									}

									strHoldingRegisters += ExtractInt64Bit(BitConverter.ToInt64(valueBytes, 0), requestedBitNumber);

									if (i != numOfPoints - 1)
										strHoldingRegisters += ", ";
								}
							}
						}
						else
						{
							for (int i = 0; i < numOfPoints; i++)
							{
								for (int j = 0; j < 4; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[i * 4 + j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								strHoldingRegisters += BitConverter.ToInt64(valueBytes, 0).ToString();

								if (i != numOfPoints - 1)
									strHoldingRegisters += ", ";
							}
						}
					}
					else //UQ
					{
						byte[] valueBytes = new byte[8];

						if (fullAddress.IndexOfAny(new char[] { '.' }) != -1 & !externalRead)
						{
							if (chbBitReading.Checked)
							{
								for (int j = 0; j < 4; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								for (int i = 0; i < numOfPoints; i++)
								{
									strHoldingRegisters += ExtractInt64Bit(Convert.ToInt64(BitConverter.ToUInt64(valueBytes, 0)), requestedBitNumber + i);

									if (i != numOfPoints - 1)
										strHoldingRegisters += ", ";
								}
							}
							else
							{
								for (int i = 0; i < numOfPoints; i++)
								{
									for (int j = 0; j < 4; j++)
									{
										var bytes = BitConverter.GetBytes(responsesUShort[i * 4 + j]);
										valueBytes[j * 2] = bytes[0];
										valueBytes[j * 2 + 1] = bytes[1];
									}

									strHoldingRegisters += ExtractInt64Bit(Convert.ToInt64(BitConverter.ToUInt64(valueBytes, 0)), requestedBitNumber);

									if (i != numOfPoints - 1)
										strHoldingRegisters += ", ";
								}
							}
						}
						else
						{
							for (int i = 0; i < numOfPoints; i++)
							{
								for (int j = 0; j < 4; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[i * 4 + j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								strHoldingRegisters += BitConverter.ToUInt64(valueBytes, 0).ToString();

								if (i != numOfPoints - 1)
									strHoldingRegisters += ", ";
							}
						}
					}
				}
				else if (startAddress.IndexOfAny(new char[] { 'S' }) != -1)
				{
					string[] intValues = null;
					if (fullAddress.IndexOfAny(new char[] { '.' }) != -1 & !externalRead)
					{
						intValues = new string[1];

						for (int i = 0; i < numOfPoints; i++)
						{
							if (chbBitReading.Checked)
								intValues[0] = BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[requestedBitNumber + i]), 0).ToString();
							else
								intValues[0] = BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[requestedBitNumber + i * numRegisters]), 0).ToString();

							strHoldingRegisters += ConvertStringOfIntegersToString(intValues);

							if (i != numOfPoints - 1)
								strHoldingRegisters += ", ";
						}
					}
					else
					{
						for (int i = 0; i < numOfPoints; i++)
						{
							intValues = new string[numRegisters];

							for (int j = 0; j < numRegisters; j++)
							{
								intValues[j] = BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[j + i * numRegisters]), 0).ToString();
							}

							strHoldingRegisters += ConvertStringOfIntegersToString(intValues);

							if (i != numOfPoints - 1)
								strHoldingRegisters += ", ";
						}
					}
				}
				else if (startAddress.IndexOfAny(new char[] { 'L' }) != -1)
				{
					if (chbSwapWords.Checked)
						responsesUShort = SwapWordsFunction(responsesUShort);

					byte[] valueBytes = new byte[4];

					if (fullAddress.IndexOfAny(new char[] { '.' }) != -1 & !externalRead)
					{
						if (chbBitReading.Checked)
						{
							for (int j = 0; j < 2; j++)
							{
								var bytes = BitConverter.GetBytes(responsesUShort[j]);
								valueBytes[j * 2] = bytes[0];
								valueBytes[j * 2 + 1] = bytes[1];
							}

							for (int i = 0; i < numOfPoints; i++)
							{
								if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
									strHoldingRegisters += ExtractInt32Bit(Convert.ToInt32(BitConverter.ToUInt32(valueBytes, 0)), requestedBitNumber + i);
								else
									strHoldingRegisters += ExtractInt32Bit(BitConverter.ToInt32(valueBytes, 0), requestedBitNumber + i);

								if (i != numOfPoints - 1)
									strHoldingRegisters += ", ";
							}
						}
						else
						{
							for (int i = 0; i < numOfPoints; i++)
							{
								for (int j = 0; j < 2; j++)
								{
									var bytes = BitConverter.GetBytes(responsesUShort[i * 2 + j]);
									valueBytes[j * 2] = bytes[0];
									valueBytes[j * 2 + 1] = bytes[1];
								}

								if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
									strHoldingRegisters += ExtractInt32Bit(Convert.ToInt32(BitConverter.ToUInt32(valueBytes, 0)), requestedBitNumber);
								else
									strHoldingRegisters += ExtractInt32Bit(BitConverter.ToInt32(valueBytes, 0), requestedBitNumber);

								if (i != numOfPoints - 1)
									strHoldingRegisters += ", ";
							}
						}
					}
					else
					{
						for (int i = 0; i < numOfPoints; i++)
						{
							for (int j = 0; j < 2; j++)
							{
								var bytes = BitConverter.GetBytes(responsesUShort[i * 2 + j]);
								valueBytes[j * 2] = bytes[0];
								valueBytes[j * 2 + 1] = bytes[1];
							}

							if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
								strHoldingRegisters += BitConverter.ToUInt32(valueBytes, 0).ToString();
							else
								strHoldingRegisters += BitConverter.ToInt32(valueBytes, 0).ToString();

							if (i != numOfPoints - 1)
								strHoldingRegisters += ", ";
						}
					}
				}
				else
				{
					for (int k = 0; k < numOfPoints; k++)
					{
						if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
						{
							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1 & !externalRead)
							{
								if (chbBitReading.Checked)
									strHoldingRegisters += ExtractInt32Bit(responsesUShort[0], requestedBitNumber + k);
								else
									strHoldingRegisters += ExtractInt32Bit(responsesUShort[k], requestedBitNumber);
							}
							else
							{
								strHoldingRegisters += responsesUShort[k].ToString();
							}
						}
						else
						{
							if (fullAddress.IndexOfAny(new char[] { '.' }) != -1 & !externalRead)
							{
								if (chbBitReading.Checked)
									strHoldingRegisters += ExtractInt32Bit(BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[0]), 0), requestedBitNumber + k);
								else
									strHoldingRegisters += ExtractInt32Bit(BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[k]), 0), requestedBitNumber);
							}
							else
							{
								strHoldingRegisters += BitConverter.ToInt16(BitConverter.GetBytes(responsesUShort[k]), 0).ToString();
							}
						}

						if (k != numOfPoints - 1)
							strHoldingRegisters += ", ";
					}
				}
			}
			else if (valueSingle.Length > 0)
			{
				for (int k = 0; k < numOfPoints; k++)
				{
					if (fullAddress.IndexOfAny(new char[] { '.' }) != -1 & !externalRead)
					{
						if (chbBitReading.Checked)
							strHoldingRegisters += ExtractInt32Bit(BitConverter.ToInt32(BitConverter.GetBytes(valueSingle[0]), 0), requestedBitNumber + k);
						else
							strHoldingRegisters += ExtractInt32Bit(BitConverter.ToInt32(BitConverter.GetBytes(valueSingle[k]), 0), requestedBitNumber);
					}
					else
					{
						strHoldingRegisters += valueSingle[k].ToString();
					}

					if (k != numOfPoints - 1)
						strHoldingRegisters += ", ";
				}
			}
			else if (valueDouble.Length > 0)
			{
				for (int k = 0; k < numOfPoints; k++)
				{
					if (fullAddress.IndexOfAny(new char[] { '.' }) != -1 & !externalRead)
					{
						if (chbBitReading.Checked)
							strHoldingRegisters += ExtractInt64Bit(BitConverter.ToInt64(BitConverter.GetBytes(valueDouble[0]), 0), requestedBitNumber + k);
						else
							strHoldingRegisters += ExtractInt64Bit(BitConverter.ToInt64(BitConverter.GetBytes(valueDouble[k]), 0), requestedBitNumber);
					}
					else
					{
						strHoldingRegisters += valueDouble[k].ToString();
					}

					if (k != numOfPoints - 1)
						strHoldingRegisters += ", ";
				}
			}
			else
			{
				AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "No Response or Error"; });
				//Set the corresponding status label back color to Red = Failed
				AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Red; });

				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "No Response or Error"; });
				return;
			}

			if (externalRead)
				ReadValue = strHoldingRegisters.Trim();
			else
			{
				AddressList[AddressListIndex].ValuesToWrite.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].ValuesToWrite.Text = "{ " + strHoldingRegisters + " }"; });
				//Set the corresponding status label back color to Green = Success
				AddressList[AddressListIndex].LabelStatus.Invoke((MethodInvoker)delegate { AddressList[AddressListIndex].LabelStatus.BackColor = Color.Green; });

				lblMessage.Invoke((MethodInvoker)delegate { lblMessage.Text = "Comms Okay"; });
			}
		}

		#endregion

		#region "Functions"

		private int RegisterCheck(int startRegister, int valueSize)
		{
			if (chbSwapWords.Checked)
			{
                if (valueSize == 128)
				{
                    switch (m_cbSwapWords128bitSelectedIndex)
                    {
						case 0: //128-bit word order 8 7 6 5 4 3 2 1 => realRegister (7 6 5 4 3 2 1 0)
							switch (startRegister)
                            {
                                case 0:
									return 7;
								case 1:
									return 6;
								case 2:
									return 5;
								case 3:
									return 4;
								case 4:
									return 3;
								case 5:
									return 2;
								case 6:
									return 1;
								default:
									return 0;
							}
						case 1:  //128-bit word order 7 8 5 6 3 4 1 2 = realRegister (6 7 4 5 2 3 0 1)
							switch (startRegister)
							{
								case 0:
									return 6;
								case 1:
									return 7;
								case 2:
									return 4;
								case 3:
									return 5;
								case 4:
									return 2;
								case 5:
									return 3;
								case 6:
									return 0;
								default:
									return 1;
							}
						case 2:  //128-bit word order 6 5 8 7 2 1 4 3 = realRegister (5 4 7 6 1 0 3 2)
							switch (startRegister)
							{
								case 0:
									return 5;
								case 1:
									return 4;
								case 2:
									return 7;
								case 3:
									return 6;
								case 4:
									return 1;
								case 5:
									return 0;
								case 6:
									return 3;
								default:
									return 2;
							}
						case 3:  //128-bit word order 5 6 7 8 1 2 3 4 = realRegister (4 5 6 7 0 1 2 3)
							switch (startRegister)
							{
								case 0:
									return 4;
								case 1:
									return 5;
								case 2:
									return 6;
								case 3:
									return 7;
								case 4:
									return 0;
								case 5:
									return 1;
								case 6:
									return 2;
								default:
									return 3;
							}
						case 4:  //128-bit word order 4 3 2 1 8 7 6 5 = realRegister (3 2 1 0 7 6 5 4)
							switch (startRegister)
							{
								case 0:
									return 3;
								case 1:
									return 2;
								case 2:
									return 1;
								case 3:
									return 0;
								case 4:
									return 7;
								case 5:
									return 6;
								case 6:
									return 5;
								default:
									return 4;
							}
						case 5:  //128-bit word order 3 4 1 2 7 8 5 6 = realRegister (2 3 0 1 6 7 4 5)
							switch (startRegister)
							{
								case 0:
									return 2;
								case 1:
									return 3;
								case 2:
									return 0;
								case 3:
									return 1;
								case 4:
									return 6;
								case 5:
									return 7;
								case 6:
									return 4;
								default:
									return 5;
							}
						default: //128-bit word order 2 1 4 3 6 5 8 7 = realRegister (1 0 3 2 5 4 7 6)
							switch (startRegister)
							{
								case 0:
									return 1;
								case 1:
									return 0;
								case 2:
									return 3;
								case 3:
									return 2;
								case 4:
									return 5;
								case 5:
									return 4;
								case 6:
									return 7;
								default:
									return 6;
							}
					}
				}
				else if (valueSize == 64)
				{
					switch (m_cbSwapWords64bitSelectedIndex)
					{
						case 0: //64-bit word order 4 3 2 1 = realRegister (3 2 1 0)
							switch (startRegister)
							{
								case 0:
									return 3;
								case 1:
									return 2;
								case 2:
									return 1;
								default:
									return 0;
							}
						case 1:  //64-bit word order 3 4 1 2 = realRegister (2 3 0 1)
							switch (startRegister)
							{
								case 0:
									return 2;
								case 1:
									return 3;
								case 2:
									return 0;
								default:
									return 1;
							}
						default: //64-bit word order 2 1 4 3 = realRegister (1 0 3 2)
							switch (startRegister)
							{
								case 0:
									return 1;
								case 1:
									return 0;
								case 2:
									return 3;
								default:
									return 2;
							}
					}
				}
				else if (valueSize == 32) //32-bit word order 2 1 = realRegister (1 0)
				{
					if (startRegister == 0)
						return 1;
					else
						return 0;
				}
				else //16-bit word order, no word swap is possible: realRegister = startRegister
					return startRegister;
			}
			else //SwapWords is not checked: realRegister = startRegister
				return startRegister;
		}

		private bool ValuesToWriteCheckPassed(int Index, string[] values) // Only addresses starting with "0" or "4" can be written to
		{
			string startAddress = AddressList[Index].PlcAddress.Text;

			if (values.Length > 1 && values.Length != Convert.ToInt32(AddressList[Index].NumberOfElements.Text))
			{
				MessageBox.Show("The number of provided values to write does not match the number of points!" + Environment.NewLine + "For multiple targets, either enter a single value or the exact number of comma separated values.");
				return false;
			}
			else
			{
				if (startAddress.IndexOfAny(new char[] { 'S' }) == -1) //Not checking strings
				{
					if (startAddress.StartsWith("0") || startAddress.IndexOfAny(new char[] { '.' }) != -1) //startAddress starts with "0" or includes "." denoting bit writing
					{
						for (int i = 0; i < values.Length; i++)
						{
							if (!(values[i].Trim() == "0" || values[i].Trim() == "1" || values[i].Trim() == "True" || values[i].Trim() == "False"))
							{
								MessageBox.Show("Provided values to write are not of Boolean type!");
								return false;
							}
						}
					}
					else // startAddress starts with "4"
					{
						if (startAddress.IndexOfAny(new char[] { 'O' }) != -1)
                        {
							BigInteger upperLimit = BigInteger.Parse("340282366920938463463374607431768211455");
							BigInteger dummy;

							if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LO
							{
								for (int i = 0; i < values.Length; i++)
								{
									try
									{
										//If negative value then  use its absolute value
										if (Convert.ToInt32(values[i][0]) == 8722 || Convert.ToInt32(values[i][0]) == 45)
										{
											if (BigInteger.TryParse(values[i].Substring(1), out dummy))
											{
												if (dummy > BigInteger.Parse("170141183460469231731687303715884105728"))
												{
													MessageBox.Show("Provided values to write are not of Int128 type!");
													return false;
												}
											}
										}
										else
										{
											if (BigInteger.TryParse(values[i], out dummy))
											{
												if (dummy > BigInteger.Parse("170141183460469231731687303715884105727"))
												{
													MessageBox.Show("Provided values to write are not of Int128 type!");
													return false;
												}
											}
										}
									}
									catch (Exception ex)
									{
										MessageBox.Show(ex.Message);
										return false;
									}
								}
							}
							else //UO
							{
								for (int i = 0; i < values.Length; i++)
								{
									try
									{
										if (Convert.ToInt32(values[i][0]) == 8722 || Convert.ToInt32(values[i][0]) == 45)
										{
											MessageBox.Show("Provided values to write are not of UInt128 type!");
											return false;
										}
										else
										{
											if (BigInteger.TryParse(values[i], out dummy))
											{
												if (dummy < 0 || dummy > upperLimit)
												{
													MessageBox.Show("Provided values to write are not of UInt128 type!");
													return false;
												}
											}
										}
									}
									catch (Exception ex)
									{
										MessageBox.Show(ex.Message);
										return false;
									}
								}
							}
						}
						else if (startAddress.IndexOfAny(new char[] { 'Q' }) != -1)
						{
							if (startAddress.IndexOfAny(new char[] { 'F' }) != -1) //FQ
							{
								for (int i = 0; i < values.Length; i++)
								{
									if (!double.TryParse(values[i], out _))
									{
										MessageBox.Show("Provided values to write are not of Double type!");
										return false;
									}
								}
							}
							else if (startAddress.IndexOfAny(new char[] { 'L' }) != -1) //LQ
							{
								for (int i = 0; i < values.Length; i++)
								{
									if (!long.TryParse(values[i], out _))
									{
										MessageBox.Show("Provided values to write are not of Long type!");
										return false;
									}
								}
							}
							else //UQ
							{
								for (int i = 0; i < values.Length; i++)
								{
									if (!ulong.TryParse(values[i], out _))
									{
										MessageBox.Show("Provided values to write are not of Ulong type!");
										return false;
									}
								}
							}
						}
						else if (startAddress.IndexOfAny(new char[] { 'F' }) != -1)
						{
							for (int i = 0; i < values.Length; i++)
							{
								if (!float.TryParse(values[i], out _))
								{
									MessageBox.Show("Provided values to write are not of Single type!");
									return false;
								}
							}
						}
						else if (startAddress.Contains("UL"))
						{
							for (int i = 0; i < values.Length; i++)
							{
								if (!uint.TryParse(values[i], out _))
								{
									MessageBox.Show("Provided values to write are not of UInteger type!");
									return false;
								}
							}
						}
						else if (startAddress.IndexOfAny(new char[] { 'L' }) != -1)
						{
							for (int i = 0; i < values.Length; i++)
							{
								if (!int.TryParse(values[i], out _))
								{
									MessageBox.Show("Provided values to write are not of Integer type!");
									return false;
								}
							}
						}
						else if (startAddress.IndexOfAny(new char[] { 'U' }) != -1)
						{
							for (int i = 0; i < values.Length; i++)
							{
								if (!ushort.TryParse(values[i], out _))
								{
									MessageBox.Show("Provided values to write are not of UShort type!");
									return false;
								}
							}
						}
						else
						{
							for (int i = 0; i < values.Length; i++)
							{
								if (!short.TryParse(values[i], out _))
								{
									MessageBox.Show("Provided values to write are not of Short type!");
									return false;
								}
							}
						}
					}
				}
			}

			return true;
		}

		private bool AddressCheckPassed(string tempAddress, int BitNumber, int numOfPoints, int indx)
		{
			var registerAddress = Convert.ToInt32(tempAddress.Substring(1, 5));

			if (tempAddress.StartsWith("0") || tempAddress.StartsWith("1"))
			{
				if ((numOfPoints > 2000) || (registerAddress + numOfPoints > 65535))
				{
					AddressList[indx].ValuesToWrite.Text = "Limits exceeded";
					//Set the corresponding status label back color to Red = Failed
					AddressList[indx].LabelStatus.BackColor = Color.Red;
					return false;
				}
			}
			else if (tempAddress.IndexOfAny(new char[] { 'O' }) != -1)
            {
				if ((BitNumber > -1 && chbBitReading.Checked && BitNumber + numOfPoints > 128) || (BitNumber > -1 && !chbBitReading.Checked && (8 * numOfPoints > 125 || registerAddress + 8 * numOfPoints > 65535)) || (BitNumber == -1 && (8 * numOfPoints > 125 || registerAddress + 8 * numOfPoints > 65535)))
				{
					AddressList[indx].ValuesToWrite.Text = "Limits exceeded";
					//Set the corresponding status label back color to Red = Failed
					AddressList[indx].LabelStatus.BackColor = Color.Red;
					return false;
				}
			}
			else if (tempAddress.IndexOfAny(new char[] { 'Q' }) != -1)
			{
				if ((BitNumber > -1 && chbBitReading.Checked && BitNumber + numOfPoints > 64) || (BitNumber > -1 && !chbBitReading.Checked && (4 * numOfPoints > 125 || registerAddress + 4 * numOfPoints > 65535)) || (BitNumber == -1 && (4 * numOfPoints > 125 || registerAddress + 4 * numOfPoints > 65535)))
				{
					AddressList[indx].ValuesToWrite.Text = "Limits exceeded";
					//Set the corresponding status label back color to Red = Failed
					AddressList[indx].LabelStatus.BackColor = Color.Red;
					return false;
				}
			}
			else if (tempAddress.IndexOfAny(new char[] { 'F' }) != -1 || tempAddress.IndexOfAny(new char[] { 'L' }) != -1)
			{
				if ((BitNumber > -1 && chbBitReading.Checked && BitNumber + numOfPoints > 32) || (BitNumber > -1 && !chbBitReading.Checked && (2 * numOfPoints > 125 || registerAddress + 2 * numOfPoints > 65535)) || (BitNumber == -1 && (2 * numOfPoints > 125 || registerAddress + 2 * numOfPoints > 65535)))
				{
					AddressList[indx].ValuesToWrite.Text = "Limits exceeded";
					//Set the corresponding status label back color to Red = Failed
					AddressList[indx].LabelStatus.BackColor = Color.Red;
					return false;
				}
			}
			else if (tempAddress.IndexOfAny(new char[] { 'S' }) != -1)
			{
				int numRegisters;

				if (tempAddress.IndexOfAny(new char[] { '.' }) != -1)
					numRegisters = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf("S") + 1, tempAddress.IndexOf(".") - tempAddress.IndexOf("S") - 1));
				else
					numRegisters = Convert.ToInt32(tempAddress.Substring(tempAddress.IndexOf("S") + 1));

				if ((BitNumber > -1 && chbBitReading.Checked && BitNumber + numOfPoints > numRegisters) || (BitNumber > -1 && !chbBitReading.Checked && (numOfPoints * numRegisters > 125 || registerAddress + numOfPoints * numRegisters > 65535)) || (BitNumber == -1 && (numOfPoints * numRegisters > 125 || registerAddress + numOfPoints * numRegisters > 65535)))
				{
					AddressList[indx].ValuesToWrite.Text = "Limits exceeded";
					//Set the corresponding status label back color to Red = Failed
					AddressList[indx].LabelStatus.BackColor = Color.Red;
					return false;
				}
			}
			else
			{
				if ((BitNumber > -1 && chbBitReading.Checked && BitNumber + numOfPoints > 16) || (BitNumber > -1 && !chbBitReading.Checked && (numOfPoints > 125 || registerAddress + numOfPoints > 65535)) || (BitNumber == -1 && (numOfPoints > 125 || registerAddress + numOfPoints > 65535)))
				{
					AddressList[indx].ValuesToWrite.Text = "Limits exceeded";
					//Set the corresponding status label back color to Red = Failed
					AddressList[indx].LabelStatus.BackColor = Color.Red;
					return false;
				}
			}

			return true;
		}

		private ushort[] SwapBytesFunction(ushort[] responsesUShort)
		{
			lock (m_Lock)
			{
				for (int i = 0; i < responsesUShort.Length; i++)
				{
					var bytes = BitConverter.GetBytes(responsesUShort[i]);
					var tempByte = bytes[0];
					bytes[0] = bytes[1];
					bytes[1] = tempByte;
					responsesUShort[i] = BitConverter.ToUInt16(bytes, 0);
				}

				return responsesUShort;
			}
		}

		private ushort[] SwapWordsFunction(ushort[] responsesUShort)
		{
			lock (m_Lock)
			{
				for (int i = 0; i < responsesUShort.Length; i += 2)
				{
					var tempUShort = responsesUShort[i];
					responsesUShort[i] = responsesUShort[i + 1];
					responsesUShort[i + 1] = tempUShort;
				}

				return responsesUShort;
			}
		}

		private ushort[] SwapWords64Function(ushort[] responsesUShort)
		{
			lock (m_Lock)
			{
				for (int i = 0; i < responsesUShort.Length; i += 4)
				{
					if (m_cbSwapWords64bitSelectedIndex == 0) // 64-bit word order = 4 3 2 1
					{
						var tempUShort0 = responsesUShort[i];
						var tempUShort1 = responsesUShort[i + 1];
						responsesUShort[i] = responsesUShort[i + 3];
						responsesUShort[i + 3] = tempUShort0;
						responsesUShort[i + 1] = responsesUShort[i + 2];
						responsesUShort[i + 2] = tempUShort1;
					}
					else if (m_cbSwapWords64bitSelectedIndex == 1) // 64-bit word order = 2 1 4 3
					{
						var tempUShort0 = responsesUShort[i];
						var tempUShort1 = responsesUShort[i + 2];
						responsesUShort[i] = responsesUShort[i + 1];
						responsesUShort[i + 1] = tempUShort0;
						responsesUShort[i + 2] = responsesUShort[i + 3];
						responsesUShort[i + 3] = tempUShort1;
					}
					else // 64-bit word order = 3 4 1 2
					{
						var tempUShort0 = responsesUShort[i];
						var tempUShort1 = responsesUShort[i + 1];
						responsesUShort[i] = responsesUShort[i + 2];
						responsesUShort[i + 2] = tempUShort0;
						responsesUShort[i + 1] = responsesUShort[i + 3];
						responsesUShort[i + 3] = tempUShort1;
					}
				}

				return responsesUShort;
			}
		}

		private ushort[] SwapWords128Function(ushort[] responsesUShort)
		{
			lock (m_Lock)
			{
				for (int i = 0; i < responsesUShort.Length; i += 8)
				{
					if (m_cbSwapWords128bitSelectedIndex == 0) // 8 7 6 5 4 3 2 1
					{
						var tempUShort0 = responsesUShort[i];
						var tempUShort1 = responsesUShort[i + 1];
						var tempUShort2 = responsesUShort[i + 2];
						var tempUShort3 = responsesUShort[i + 3];
						responsesUShort[i] = responsesUShort[i + 7];
						responsesUShort[i + 7] = tempUShort0;
						responsesUShort[i + 1] = responsesUShort[i + 6];
						responsesUShort[i + 6] = tempUShort1;
						responsesUShort[i + 2] = responsesUShort[i + 5];
						responsesUShort[i + 5] = tempUShort2;
						responsesUShort[i + 3] = responsesUShort[i + 4];
						responsesUShort[i + 4] = tempUShort3;
					}
					else if (m_cbSwapWords128bitSelectedIndex == 1) // 7 8 5 6 3 4 1 2
					{
						var tempUShort0 = responsesUShort[i];
						var tempUShort1 = responsesUShort[i + 1];
						var tempUShort2 = responsesUShort[i + 2];
						var tempUShort3 = responsesUShort[i + 3];
						responsesUShort[i] = responsesUShort[i + 6];
						responsesUShort[i + 6] = tempUShort0;
						responsesUShort[i + 1] = responsesUShort[i + 7];
						responsesUShort[i + 7] = tempUShort1;
						responsesUShort[i + 2] = responsesUShort[i + 4];
						responsesUShort[i + 4] = tempUShort2;
						responsesUShort[i + 3] = responsesUShort[i + 5];
						responsesUShort[i + 5] = tempUShort3;
					}
					else if (m_cbSwapWords128bitSelectedIndex == 2) // 6 5 8 7 2 1 4 3
					{
						var tempUShort0 = responsesUShort[i];
						var tempUShort1 = responsesUShort[i + 1];
						var tempUShort2 = responsesUShort[i + 2];
						var tempUShort3 = responsesUShort[i + 3];
						responsesUShort[i] = responsesUShort[i + 5];
						responsesUShort[i + 5] = tempUShort0;
						responsesUShort[i + 1] = responsesUShort[i + 4];
						responsesUShort[i + 4] = tempUShort1;
						responsesUShort[i + 2] = responsesUShort[i + 7];
						responsesUShort[i + 7] = tempUShort2;
						responsesUShort[i + 3] = responsesUShort[i + 6];
						responsesUShort[i + 6] = tempUShort3;
					}
					else if (m_cbSwapWords128bitSelectedIndex == 3) // 5 6 7 8 1 2 3 4
					{
						var tempUShort0 = responsesUShort[i];
						var tempUShort1 = responsesUShort[i + 1];
						var tempUShort2 = responsesUShort[i + 2];
						var tempUShort3 = responsesUShort[i + 3];
						responsesUShort[i] = responsesUShort[i + 4];
						responsesUShort[i + 4] = tempUShort0;
						responsesUShort[i + 1] = responsesUShort[i + 5];
						responsesUShort[i + 5] = tempUShort1;
						responsesUShort[i + 2] = responsesUShort[i + 6];
						responsesUShort[i + 6] = tempUShort2;
						responsesUShort[i + 3] = responsesUShort[i + 7];
						responsesUShort[i + 7] = tempUShort3;
					}
					else if (m_cbSwapWords128bitSelectedIndex == 4) // 4 3 2 1 8 7 6 5
					{
						var tempUShort0 = responsesUShort[i];
						var tempUShort1 = responsesUShort[i + 1];
						var tempUShort2 = responsesUShort[i + 4];
						var tempUShort3 = responsesUShort[i + 5];
						responsesUShort[i] = responsesUShort[i + 3];
						responsesUShort[i + 3] = tempUShort0;
						responsesUShort[i + 1] = responsesUShort[i + 2];
						responsesUShort[i + 2] = tempUShort1;
						responsesUShort[i + 4] = responsesUShort[i + 7];
						responsesUShort[i + 7] = tempUShort2;
						responsesUShort[i + 5] = responsesUShort[i + 6];
						responsesUShort[i + 6] = tempUShort3;
					}
					else if (m_cbSwapWords128bitSelectedIndex == 5) // 3 4 1 2 7 8 5 6
					{
						var tempUShort0 = responsesUShort[i];
						var tempUShort1 = responsesUShort[i + 1];
						var tempUShort2 = responsesUShort[i + 4];
						var tempUShort3 = responsesUShort[i + 5];
						responsesUShort[i] = responsesUShort[i + 2];
						responsesUShort[i + 2] = tempUShort0;
						responsesUShort[i + 1] = responsesUShort[i + 3];
						responsesUShort[i + 3] = tempUShort1;
						responsesUShort[i + 4] = responsesUShort[i + 6];
						responsesUShort[i + 6] = tempUShort2;
						responsesUShort[i + 5] = responsesUShort[i + 7];
						responsesUShort[i + 7] = tempUShort3;
					}
					else // 2 1 4 3 6 5 8 7
					{
						var tempUShort0 = responsesUShort[i];
						var tempUShort1 = responsesUShort[i + 2];
						var tempUShort2 = responsesUShort[i + 4];
						var tempUShort3 = responsesUShort[i + 6];
						responsesUShort[i] = responsesUShort[i + 1];
						responsesUShort[i + 1] = tempUShort0;
						responsesUShort[i + 2] = responsesUShort[i + 3];
						responsesUShort[i + 3] = tempUShort1;
						responsesUShort[i + 4] = responsesUShort[i + 5];
						responsesUShort[i + 5] = tempUShort2;
						responsesUShort[i + 6] = responsesUShort[i + 7];
						responsesUShort[i + 7] = tempUShort3;
					}
				}

				return responsesUShort;
			}
		}

		private string ExtractInt128Bit(string ReadValue, int BitToReturn)
		{
			lock (m_Lock)
			{
				if (ReadValue[127 - BitToReturn] == '0')
					return "False";
				else
					return "True";
			}
		}

		private string ExtractInt64Bit(long ReadValue, int BitToReturn)
		{
			lock (m_Lock)
			{
				var bitString = Convert.ToString(ReadValue, 2).PadLeft(64, '0');
				if (bitString[63 - BitToReturn] == '0')
					return "False";
				else
					return "True";
			}
		}

		private string ExtractInt32Bit(int ReadValue, int BitToReturn)
		{
			lock (m_Lock)
			{
				var bitString = Convert.ToString(ReadValue, 2).PadLeft(32, '0');
				if (bitString[31 - BitToReturn] == '0')
					return "False";
				else
					return "True";
			}
		}

		//private string ExtractInt16Bit(short ReadValue, int BitToReturn)
		//{
		//	lock (m_Lock)
		//	{
		//		var bitString = Convert.ToString(ReadValue, 2).PadLeft(16, '0');
		//		if (bitString[15 - BitToReturn] == '0')
		//			return "False";
		//		else
		//			return "True";
		//	}
		//}

		private BigInteger ChangeUInt128Bit(BigInteger ReadValue, int BitToModify, string BitValueToWrite)
		{
			lock (m_Lock)
			{
				if (BitValueToWrite == "0" || BitValueToWrite == "False" || BitValueToWrite == "false")
					BitValueToWrite = "0";
				else
					BitValueToWrite = "1";

				byte[] bytes = new byte[17];
				Array.Copy(ReadValue.ToByteArray(), bytes, ReadValue.ToByteArray().Length);

				string bits = "";

				for (int i = bytes.Length - 2; i > -1; i -= 1)
				{
					bits += Convert.ToString(bytes[i], 2).PadLeft(8, '0');
				}

				var retValueBinary = bits.Substring(0, 127 - BitToModify) + BitValueToWrite + bits.Substring(127 - BitToModify + 1);

				return BitConverterUInt128(retValueBinary);
			}
		}

		private long ChangeInt64Bit(long ReadValue, int BitToModify, string BitValueToWrite)
		{
			lock (m_Lock)
			{
				var bits = Convert.ToString(ReadValue, 2).PadLeft(64, '0');

				if (BitValueToWrite == "0" || BitValueToWrite == "False")
					BitValueToWrite = "0";
				else
					BitValueToWrite = "1";

				var retValueBinary = bits.Substring(0, 63 - BitToModify) + BitValueToWrite + bits.Substring(63 - BitToModify + 1);

				return Convert.ToInt64(retValueBinary, 2);
			}
		}

		private int ChangeInt32Bit(int ReadValue, int BitToModify, string BitValueToWrite)
		{
			lock (m_Lock)
			{
				var bits = Convert.ToString(ReadValue, 2).PadLeft(32, '0');

				if (BitValueToWrite == "0" || BitValueToWrite == "False")
					BitValueToWrite = "0";
				else
					BitValueToWrite = "1";

				var retValueBinary = bits.Substring(0, 31 - BitToModify) + BitValueToWrite + bits.Substring(31 - BitToModify + 1);

				return Convert.ToInt32(retValueBinary, 2);
			}
		}

		private short ChangeInt16Bit(short ReadValue, int BitToModify, string BitValueToWrite)
		{
			lock (m_Lock)
			{
				var bits = Convert.ToString(ReadValue, 2).PadLeft(16, '0');

				if (BitValueToWrite == "0" || BitValueToWrite == "False")
					BitValueToWrite = "0";
				else
					BitValueToWrite = "1";

				var retValueBinary = bits.Substring(0, 15 - BitToModify) + BitValueToWrite + bits.Substring(15 - BitToModify + 1);

				return Convert.ToInt16(retValueBinary, 2);
			}
		}

		private BigInteger BitConverterInt128(string binaryString)
		{
			lock (m_Lock)
			{
				BigInteger Int128 = 0;

				for (int i = 0; i < binaryString.Length - 1; i++)
				{
					if (binaryString[127 - i] == '1')
						Int128 += (BigInteger) Math.Pow(2, i);
				}

				if (binaryString[0] == '0')
					return Int128;
				else
					return BigInteger.Parse("-170141183460469231731687303715884105728") + Int128;
			}
		}

		private BigInteger BitConverterUInt128(string binaryString)
		{
			lock (m_Lock)
			{
				BigInteger UInt128 = 0;
				for (int i = 0; i < binaryString.Length; i++)
				{
					if (binaryString[127 - i] == '1')
						UInt128 += (BigInteger) Math.Pow(2, i);
				}
				return UInt128;
			}
		}

		private ushort[] ConvertStringToStringOfUShorts(string str)
		{
			//* Convert string to an array of bytes
			byte[] ByteArray = System.Text.Encoding.Default.GetBytes(str);

			//* Convert each byte to ushort
			ushort[] ushorts = new ushort[ByteArray.Length];

			for (int i = 0; i < ByteArray.Length; i++)
			{
				ushorts[i] = Convert.ToUInt16(ByteArray[i]);
			}

			//* Return the ushort array
			return ushorts;
		}

		private string ConvertStringOfIntegersToString(string[] ints)
		{
			//* Convert integer values to strings and then to an array of bytes
			byte[] ByteArray = new byte[ints.Length];

			for (int i = 0; i < ints.Length; i++)
			{
				if (ints[i] == "0")
					ByteArray[i] = Convert.ToByte((int)(' '));
				else
					ByteArray[i] = Convert.ToByte(ints[i]);
			}

			//* Convert the array of bytes to a string
			string result = System.Text.Encoding.UTF8.GetString(ByteArray);

			//Return the string
			return result.Trim();
		}

		#endregion

		#region "ToolTips"

		private void ButtonCloseRTU_MouseHover(object sender, EventArgs e)
		{
			if (btnCloseRTU.Enabled)
				AllToolTip.SetToolTip(btnCloseRTU, "Close serial port.");
		}

		private void ButtonOpenRTU_MouseHover(object sender, EventArgs e)
		{
			if (btnOpenRTUASCII.Enabled)
				AllToolTip.SetToolTip(btnOpenRTUASCII, "Open serial port with currently selected parameters.");
		}

		private void ButtonRefresh_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(btnRefresh, "Refresh serial ports list.");
		}

		private void LabelManual_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(lblManual, "Set COM port manually.");
		}

		private void CheckBoxBitReading_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(chbBitReading, "Applicable to bit/character reading/writing of Input/Holding Registers." + Environment.NewLine + "Checked = Read/Write consecutive bits/characters of a single element." + Environment.NewLine + "Unchecked = Read/Write the exact bit/character of each of multiple elements.");
		}

		private void CheckBoxFC22_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(chbFC22, "Applicable to bit writing of Holding Register." + Environment.NewLine + "Checked = Function Code 22 (16H) will be used." + Environment.NewLine + "Unchecked = The app built-in code will be used (read register - change bit - write register).");
		}

		private void CheckBox_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip((CheckBox)sender, "Show/Hide messages window.");
		}

		private void LabelSwapWords64bit_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(lblSwapWords64bit, "Select the order of words for 64-bit values.");
		}

		private void LabelSwapWords128bit_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(lblSwapWords128bit, "Select the order of words for 128-bit values.");
		}

		private void ListBox_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip((ListBox)sender, "Double-click to save messages to a log file." + Environment.NewLine + "Right-click to clear the messages window.");
		}

		private void LabelSlaveStatus_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip((Label)sender, "WHITE = Inactive" + Environment.NewLine + "YELLOW = Processing" + Environment.NewLine + "GREEN = Success" + Environment.NewLine + "RED = Failed");
		}

		private void LabelSlaveVTW_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip((Label)sender, "Write - Either a single value or the exact number of comma separated values required for writing if Points number > 1." + Environment.NewLine + "Read - Received values will be displayed as Read-Only.");
		}

		private void LabelSlaveReset_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip((Label)sender, "Click a radio button to clear the address and reset corresponding controls.");
		}

		private void LabelSlavePoints_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip((Label)sender, "Number of elements to Read/Write." + Environment.NewLine + "Yellow back color indicates Bit/Character mode.");
		}

		private void LabelComm_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(lblComm, "Right-click for Hints.");
		}

		private void LabelAutoRead_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip((Label)sender, "Perform automatic reads.");
		}

		private void LabelPollInterval_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip((Label)sender, "Poll Interval in milliseconds.");
		}

		private void PictureBox1_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(PictureBox1, "https://code.google.com/p/nmodbus/");
		}

		#endregion
	}
}