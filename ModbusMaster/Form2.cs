using System;
using System.Drawing;
using System.Windows.Forms;

namespace ModbusMaster
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

		private readonly ToolTip AllToolTip = new ToolTip();
		private bool NoText;
		public string Txt, ResultingText;

		#region "Private Methods"

		private void Form2_Load(object sender, EventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(Txt))
			{
				InitializeAll();

				for (int i = 0; i < cbIO.Items.Count; i++)
				{
					if (cbIO.Items[i].ToString() == Txt.Substring(0, 1))
					{
						cbIO.SelectedIndex = cbIO.Items.IndexOf(cbIO.Items[i]);
						break;
					}
				}

				tbUserAddress.Text = Txt.Substring(1, 5);

				if (Txt.Length > 6)
				{
					var extension = Txt.Substring(6);
					if (extension.IndexOfAny(new char[] { '.' }) != -1)
					{
						if (extension.IndexOfAny(new char[] { 'F' }) != -1 && extension.IndexOfAny(new char[] { 'Q' }) != -1)
						{
							SetBitIndex(extension, "FQ");
							cbModifier.SelectedIndex = 6;
						}
						else if (extension.IndexOfAny(new char[] { 'F' }) != -1)
						{
							SetBitIndex(extension, "F");
							cbModifier.SelectedIndex = 3;
						}
						else if (extension.IndexOfAny(new char[] { 'S' }) != -1)
						{
							SetBitIndex(extension, "S");
							cbModifier.SelectedIndex = 2;
						}
						else if (extension.IndexOfAny(new char[] { 'U' }) != -1 && extension.IndexOfAny(new char[] { 'L' }) != -1)
						{
							SetBitIndex(extension, "UL");
							cbModifier.SelectedIndex = 5;
						}
						else if (extension.IndexOfAny(new char[] { 'U' }) != -1 && extension.IndexOfAny(new char[] { 'Q' }) != -1)
						{
							SetBitIndex(extension, "UQ");
							cbModifier.SelectedIndex = 8;
						}
						else if (extension.IndexOfAny(new char[] { 'U' }) != -1 && extension.IndexOfAny(new char[] { 'O' }) != -1)
						{
							SetBitIndex(extension, "UO");
							cbModifier.SelectedIndex = 10;
						}
						else if (extension.IndexOfAny(new char[] { 'L' }) != -1 && extension.IndexOfAny(new char[] { 'Q' }) != -1)
						{
							SetBitIndex(extension, "LQ");
							cbModifier.SelectedIndex = 7;
						}
						else if (extension.IndexOfAny(new char[] { 'L' }) != -1 && extension.IndexOfAny(new char[] { 'O' }) != -1)
						{
							SetBitIndex(extension, "LO");
							cbModifier.SelectedIndex = 9;
						}
						else if (extension.IndexOfAny(new char[] { 'L' }) != -1)
						{
							SetBitIndex(extension, "L");
							cbModifier.SelectedIndex = 4;
						}
						else if (extension.IndexOfAny(new char[] { 'U' }) != -1)
						{
							SetBitIndex(extension, "U");
							cbModifier.SelectedIndex = 1;
						}
						else
						{
							SetBitIndex(extension, "");

							for (int i = 0; i < cbModifier.Items.Count; i++)
							{
								if (cbModifier.Items[i].ToString() == Txt.Substring(6))
								{
									cbModifier.SelectedIndex = cbModifier.Items.IndexOf(cbModifier.Items[i]);
									break;
								}
							}
						}
					}
					else
					{
						if (extension.IndexOfAny(new char[] { 'S' }) != -1)
						{
							cbModifier.SelectedIndex = 2;

							for (int i = 0; i < cbStringLength.Items.Count; i++)
							{
								if (cbStringLength.Items[i].ToString() == extension.Substring(1))
								{
									cbStringLength.SelectedIndex = cbStringLength.Items.IndexOf(cbStringLength.Items[i]);
									break;
								}
							}
						}
						else
						{
							for (int i = 0; i < cbModifier.Items.Count; i++)
							{
								if (cbModifier.Items[i].ToString() == Txt.Substring(6))
								{
									cbModifier.SelectedIndex = cbModifier.Items.IndexOf(cbModifier.Items[i]);
									break;
								}
							}
						}
					}
				}
			}
			else
				InitializeAll();

			if (cbStringLength.Enabled)
				tbResultingAddress.Text = cbIO.SelectedItem + tbUserAddress.Text + cbModifier.SelectedItem.ToString().Trim() + cbStringLength.SelectedItem + cbBit.SelectedItem.ToString().Trim() + cbCharacter.SelectedItem.ToString().Trim();
			else
				tbResultingAddress.Text = cbIO.SelectedItem + tbUserAddress.Text + cbBit.SelectedItem.ToString().Trim() + cbModifier.SelectedItem.ToString().Trim();

			CheckValue();
		}

		private void InitializeAll()
		{
			NoText = true;
			cbIO.SelectedIndex = 0;
			tbUserAddress.Text = "00000";
			cbBit.SelectedIndex = 0;
			cbBit.Enabled = false;
			cbModifier.SelectedIndex = 0;
			cbModifier.Enabled = false;
			cbStringLength.SelectedIndex = 0;
			cbStringLength.Enabled = false;
			cbCharacter.SelectedIndex = 0;
			cbCharacter.Enabled = false;
			NoText = false;
		}

		private void SetBitIndex(string txt, string modifier)
		{
			if (!string.IsNullOrWhiteSpace(modifier))
			{
				if (modifier == "S")
				{
					cbBit.SelectedIndex = 0;
					for (int i = 0; i < cbStringLength.Items.Count; i++)
					{
						if (cbStringLength.Items[i].ToString() == txt.Substring(1, txt.IndexOf(".") - 1))
						{
							cbStringLength.SelectedIndex = cbStringLength.Items.IndexOf(cbStringLength.Items[i]);
							break;
						}
					}
					for (int i = 0; i < cbCharacter.Items.Count; i++)
					{
						if (cbCharacter.Items[i].ToString() == txt.Substring(txt.IndexOf(".")))
						{
							cbCharacter.SelectedIndex = cbCharacter.Items.IndexOf(cbCharacter.Items[i]);
							break;
						}
					}
				}
				else
				{
					for (int i = 0; i < cbBit.Items.Count; i++)
					{
						if (cbBit.Items[i].ToString() == txt.Substring(0, txt.IndexOf(modifier)))
						{
							cbBit.SelectedIndex = cbBit.Items.IndexOf(cbBit.Items[i]);
							break;
						}
					}
				}
			}
			else
			{
				for (int i = 0; i < cbBit.Items.Count; i++)
				{
					if (cbBit.Items[i].ToString() == txt)
					{
						cbBit.SelectedIndex = cbBit.Items.IndexOf(cbBit.Items[i]);
						break;
					}
				}
			}
		}

		private void ComboBoxIO_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (!NoText)
			{
				if (cbIO.SelectedIndex == 0 || cbIO.SelectedIndex == 1)
				{
					cbModifier.SelectedIndex = 0;
					cbModifier.Enabled = false;
					cbStringLength.SelectedIndex = 0;
					cbStringLength.Enabled = false;
					cbBit.SelectedIndex = 0;
					cbBit.Enabled = false;
				}
				else
				{
					cbModifier.SelectedIndex = 0;
					cbModifier.Enabled = true;
					cbBit.SelectedIndex = 0;
					cbBit.Enabled = true;
				}

				tbResultingAddress.Text = cbIO.Text + tbUserAddress.Text.PadLeft(5, '0');

				CheckValue();
			}
		}

		private void ComboBoxBit_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (!NoText)
			{
				if (cbModifier.SelectedIndex == 2)
					tbResultingAddress.Text = cbIO.SelectedItem + tbUserAddress.Text + cbModifier.SelectedItem.ToString().Trim() + cbStringLength.SelectedItem + cbCharacter.SelectedItem.ToString().Trim();
				else
				{
					tbResultingAddress.Text = cbIO.SelectedItem + tbUserAddress.Text + cbBit.SelectedItem.ToString().Trim() + cbModifier.SelectedItem.ToString().Trim();

					if (cbBit.SelectedIndex != 0)
					{
						var bitsNumber = Convert.ToInt32(cbBit.SelectedItem.ToString().Substring(1));
						if (((bitsNumber < 16) && (cbModifier.SelectedIndex != 2)) || ((bitsNumber > 15) && (bitsNumber < 32) && cbModifier.SelectedIndex > 2) || ((bitsNumber > 31) && cbModifier.SelectedIndex > 5) || ((bitsNumber > 63) && (cbModifier.SelectedIndex == 9 || cbModifier.SelectedIndex == 10)))
							tbResultingAddress.BackColor = Color.LimeGreen;
						else
							tbResultingAddress.BackColor = Color.Red;
					}
				}
			}
		}

		private void ComboBoxModifier_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (!NoText)
			{
				if (cbModifier.SelectedIndex == 2)
				{
					cbBit.Enabled = false;
					cbStringLength.Enabled = true;
					cbCharacter.Enabled = true;
					tbResultingAddress.Text = cbIO.SelectedItem + tbUserAddress.Text.PadLeft(5, '0') + cbModifier.SelectedItem.ToString() + cbStringLength.SelectedItem + cbCharacter.SelectedItem.ToString().Trim();

					if (!string.IsNullOrWhiteSpace(cbCharacter.SelectedItem.ToString()))
					{
						if (Convert.ToInt32(cbCharacter.SelectedItem.ToString().Substring(1)) <= Convert.ToInt32(cbStringLength.SelectedItem))
							tbResultingAddress.BackColor = Color.LimeGreen;
						else
							tbResultingAddress.BackColor = Color.Red;
					}
					else
						tbResultingAddress.BackColor = Color.LimeGreen;
				}
				else
				{
					cbBit.Enabled = true;
					cbStringLength.Enabled = false;
					cbCharacter.Enabled = false;
					tbResultingAddress.Text = cbIO.SelectedItem + tbUserAddress.Text.PadLeft(5, '0') + cbBit.SelectedItem.ToString().Trim() + cbModifier.SelectedItem.ToString().Trim();

					if (cbBit.SelectedIndex != 0)
					{
						var bitsNumber = Convert.ToInt32(cbBit.SelectedItem.ToString().Substring(1));

						if ((bitsNumber > 15 && bitsNumber < 32 && cbModifier.SelectedIndex > 2) || (bitsNumber > 31 && bitsNumber < 64 && cbModifier.SelectedIndex > 5) || (bitsNumber > 63 && cbModifier.SelectedIndex > 8) || (bitsNumber < 16 && (cbModifier.SelectedIndex != 2)))
							tbResultingAddress.BackColor = Color.LimeGreen;
						else
							tbResultingAddress.BackColor = Color.Red;
					}
					else
						tbResultingAddress.BackColor = Color.LimeGreen;
				}
			}
		}

		private void ComboBoxString_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (!NoText)
			{
				tbResultingAddress.Text = cbIO.SelectedItem + tbUserAddress.Text.PadLeft(5, '0') + cbModifier.SelectedItem.ToString().Trim() + cbStringLength.SelectedItem + cbCharacter.SelectedItem.ToString().Trim();
				if (cbCharacter.SelectedIndex > 0)
				{
					if (((Convert.ToInt32(tbUserAddress.Text) + cbStringLength.SelectedIndex + 1) < 65536) && (cbCharacter.SelectedIndex < cbStringLength.SelectedIndex + 2))
						tbResultingAddress.BackColor = Color.LimeGreen;
					else
						tbResultingAddress.BackColor = Color.Red;
				}
				else
				{
					if ((Convert.ToInt32(tbUserAddress.Text) + cbStringLength.SelectedIndex + 1) < 65536)
						tbResultingAddress.BackColor = Color.LimeGreen;
					else
						tbResultingAddress.BackColor = Color.Red;
				}
			}
		}

		private void TextBoxUserAddress_TextChanged(object sender, EventArgs e)
		{
			if (!NoText)
			{
				if (cbStringLength.Enabled)
					tbResultingAddress.Text = cbIO.SelectedItem + tbUserAddress.Text.PadLeft(5, '0') + cbModifier.SelectedItem.ToString() + cbStringLength.SelectedItem + cbCharacter.SelectedItem.ToString().Trim();
				else
					tbResultingAddress.Text = cbIO.SelectedItem + tbUserAddress.Text.PadLeft(5, '0') + cbBit.SelectedItem.ToString().Trim() + cbModifier.SelectedItem.ToString().Trim();

				CheckValue();

				if (cbBit.SelectedIndex != 0)
				{
					var bitsNumber = Convert.ToInt32(cbBit.SelectedItem.ToString().Substring(1));
					if ((bitsNumber > 15 && bitsNumber < 32 && cbModifier.SelectedIndex > 2) || (bitsNumber > 31 && bitsNumber < 64 && cbModifier.SelectedIndex > 5) || (bitsNumber > 63 && cbModifier.SelectedIndex > 8) || (bitsNumber < 16 && (cbModifier.SelectedIndex != 2)))
						tbResultingAddress.BackColor = Color.LimeGreen;
					else
						tbResultingAddress.BackColor = Color.Red;
				}
				else
					tbResultingAddress.BackColor = Color.LimeGreen;
			}
		}

		private void TextBoxResultingAddress_TextChanged(object sender, EventArgs e)
		{
			ResultingText = tbResultingAddress.Text;
		}

		private void TextBoxResultingAddress_BackColorChanged(object sender, EventArgs e)
		{
			if (tbResultingAddress.BackColor == Color.Red)
				Button1.Enabled = false;
			else
				Button1.Enabled = true;
		}

		private void CheckValue()
		{
			if (int.TryParse(tbUserAddress.Text, out int address))
			{
				if (address < 0 || (cbModifier.SelectedIndex < 2 && address > 65535) || (cbModifier.SelectedIndex > 2 && cbModifier.SelectedIndex < 6 && address > 65534) || ((cbModifier.SelectedIndex == 6 || cbModifier.SelectedIndex == 7 || cbModifier.SelectedIndex == 8) && address > 65532) || ((cbModifier.SelectedIndex == 9 || cbModifier.SelectedIndex == 10) && address > 65528) || (cbStringLength.Enabled && (address + cbStringLength.SelectedIndex + 1) > 65535))
					tbResultingAddress.BackColor = Color.Red;
				else
					tbResultingAddress.BackColor = Color.LimeGreen;
			}
			else
				tbResultingAddress.BackColor = Color.Red;
		}

		#endregion

		#region "ToolTips"

		private void LabelModifier_MouseHover(object sender, System.EventArgs e)
		{
			AllToolTip.SetToolTip(lblModifier, "F = Float32" + Environment.NewLine + "L = Int32" + Environment.NewLine + "S = String" + Environment.NewLine + "U = UInt16" + Environment.NewLine + "UL = UInt32" + Environment.NewLine + "FQ = Float64" + Environment.NewLine + "LQ = Int64" + Environment.NewLine + "UQ = UInt64" + Environment.NewLine + "LO = Int128" + Environment.NewLine + "UO = UInt128");
		}

		private void LabelCharacter_MouseHover(object sender, System.EventArgs e)
		{
			AllToolTip.SetToolTip(lblCharacter, "A character from the string");
		}

        private void LabelBit_MouseHover(object sender, System.EventArgs e)
		{
			AllToolTip.SetToolTip(lblBit, "A bit from the 16 / 32 / 64 / 128 bit number");
		}

		#endregion
	}
}
