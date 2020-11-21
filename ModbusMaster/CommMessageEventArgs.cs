namespace ModbusMaster
{
	public class CommMessageEventArgs : System.EventArgs
	{
		#region "Constructor/Destructors"

		/// <summary>
		/// Creates a new instance of the CommMessageEventArgs
		/// </summary>
		/// <remarks></remarks>
		public CommMessageEventArgs() : base()
		{
		}

		/// <summary>
		/// Creates a new instance of the CommMessageEventArgs
		/// </summary>
		/// <param name="message">Error message</param>
		/// <remarks></remarks>
		public CommMessageEventArgs(string message) : this()
		{
			this.m_Message = message;
		}

		#endregion

		#region "Properties"

		private string m_Message = "";
		public string Message {
			get { return this.m_Message; }
			set {
				if (this.m_Message != value) {
					this.m_Message = value;
				}
			}
		}

		#endregion
	}
}
