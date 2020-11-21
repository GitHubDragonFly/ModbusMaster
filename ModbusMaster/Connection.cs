using System;
using System.Net.Sockets;
using System.Net;

namespace ModbusMaster
{
	public class Connection : System.ComponentModel.Component, IDisposable
	{
		/// <summary>
		/// Event to raise upon failing to connect.
		/// </summary>
		/// <remarks></remarks>
		public event EventHandler<CommMessageEventArgs> ErrorMessage;

		private TcpClient ClientTCP;
		private UdpClient ClientUDP;
		private int TcpPort;
		private IPAddress IpAddr;

		private IPHostEntry IPHE;

		#region "Constructor/Destructors"

		public Connection(System.ComponentModel.IContainer container) : this()
		{
			//Required for Windows.Forms Class Composition Designer support
			container.Add(this);
		}

		public Connection()
		{
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing) {
				if (this.ClientTCP != null) {
					this.ClientTCP.Close();
				}

				if (this.ClientUDP != null) {
					this.ClientUDP.Close();
				}
			}
		}

		#endregion

		#region "Events"

		protected virtual void OnErrorMessage(CommMessageEventArgs e)
		{
			ErrorMessage?.Invoke(this, e);
		}

		#endregion

		#region "Public Methods"

		/// <summary>
		/// Connect to remote host
		/// </summary>
		/// <param name="IP">IP address to connect to via TCP or UDP.</param>
		/// <param name="port">TCP/UDP port to use to connect.</param>
		/// <param name="ProtocolType">Modbus protocol type.</param>
		/// <returns>An object is returned as either of TcpClient or UdpClient. Nothing is returned if failed to connect.</returns>
		/// <remarks></remarks>
		public object Connect(string IP, string port, int ProtocolType)
		{
			try {
				TcpPort = int.Parse(port);
				IpAddr = new System.Net.IPAddress(0);

				if (System.Net.IPAddress.TryParse(IP, out IpAddr)) {
					if (ProtocolType == 1 || ProtocolType == 3 || ProtocolType == 6) //TCP or RTUoverTCP or ASCIIoverTCP
					{
						this.ClientTCP = new TcpClient();
						this.ClientTCP.Connect(IpAddr, TcpPort);
						return this.ClientTCP;
					}
					else //UDP or RTUoverUDP or ASCIIoverUDP
					{
						this.ClientUDP = new UdpClient();
						this.ClientUDP.Connect(IpAddr, TcpPort);
						return this.ClientUDP;
					}
				} else {
					IPHE = Dns.GetHostEntry(IP);
					if (ProtocolType == 1 || ProtocolType == 3 || ProtocolType == 6) //TCP or RTUoverTCP or ASCIIoverTCP
					{
						this.ClientTCP = new TcpClient();
						this.ClientTCP.Connect(IPHE.AddressList[1], TcpPort);
						return this.ClientTCP;
					}
					else //RTUoverUDP or ASCIIoverUDP
					{
						this.ClientUDP = new UdpClient();
						this.ClientUDP.Connect(IPHE.AddressList[1], TcpPort);
						return this.ClientUDP;
					}
				}
			} catch (Exception) {
				OnErrorMessage(new CommMessageEventArgs("Could not connect to remote host"));
				return null;
			}
		}

		#endregion
	}
}
