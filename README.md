# ModbusMaster
Standalone Windows app supporting Modbus RTU, TCP, UDP, RTUoverTCP, RTUoverUDP, ASCIIoverRTU, ASCIIoverTCP and ASCIIoverUDP protocols. Also included are its Mono versions for Linux and Mac OS X (these are VB Net versions so for Linux you will have to install mono-complete and mono-vbnc packages while Mac might be different depending on the OS X version).

Based on modified [nModbus](https://code.google.com/p/nmodbus/) .NET 3.5 libraries, Copyright (c) 2006 Scott Alexander.
These are included as a resource for Windows version but are separate for Mono version.

Intended to be used as a quick testing tool. Can be tested with its counterpart [ModbusSlaveSimulation](https://github.com/GitHubDragonFly/ModbusSlaveSimulation).

# Functionality
- Read the comments inside the form and also hover the mouse over the labels for hints.
- No Offset Addressing: Coils = 0xxxxx, Discrete Inputs = 1xxxxx, Input Registers = 3xxxxx, Holding Registers = 4xxxxx (where xxxxx goes from 0 up to 65534).
- Apart from Int16, register adress only (without modifier), it also supports U, F, L, UL and S modifiers (UInt16, Float32, Int32, UInt32, String)
- A support for 64-bit values was added - Float64, signed and unsigned Integer64. Use FQ, LQ and UQ modifiers (where "Q" stands for Quad Word).
- An experimental support for 128-bit values was added - signed and unsigned Integer128. Use LO and UO modifiers (where "O" stands for Octa Word).
- It also supports bit/character Reading/Writing:
  - select either consecutive bits/characters within a single element or the exact individual bit/character from each of the multiple elements.
  - either a single value or the exact number of comma separated values will be required for writing if Points number > 1.
- For RTU based protocols, on a single PC, this app can use the help of the com0com Windows program to provide virtual serial port pairs.
- Additional TextBox allows manual input of the serial port (intended for Linux so tty0tty virtual ports could be accessed). This box was removed in Mac Mono version.
- The library supports Masked Bit Write, function code 22 (0x16H), but the app also includes the built-in code for slave devices that don't support FC22 (this entails read-modify-write which could overwrite values that changed during this process).
- Exercise caution when attempting to write any value to the PLC.

# Build
All it takes is to:
## -> For Windows
- Download and install Visual Studio community edition (ideally 2019).
- Download and extract the zip file of this project.
- Open this as an existing project in Visual Studio and, on the menu, do:
  - Build/Build Solution (or press Ctrl-Shift-B).
  - Debug/Start Debugging (or press F5) to run the app.
- Locate created EXE file in the /bin/Debug folder and copy it over to your preferred folder or Desktop.
## -> For Mono
- Make sure that Mono is installed on your computer, both mono-complete and mono-vbnc for Linux while for Mac you might need to experiment (maybe mono and mono-basic).
- Download and extract the zip file of this project and locate Mono archive in the "Mono" folder.
- Extract 4 files and potentially rename the newly created folder and/or exe file to something shorter if you wish (just to make terminal navigation quicker).
- Open the terminal, navigate to the folder and type: sudo mono ModbusMaster.exe (on Mac you might need to switch to superuser "su" account)
- For testing RTU protocols, on Linux you can possibly install and use [tty0tty](https://github.com/freemed/tty0tty) virtual ports while on Mac the later OS X versions seem to have pseudo terminals - pairs of devices such as /dev/ptyp3 and /dev/ttyp3.

# License
Licensed under MIT license - see the README.txt file inside the Resources folder.

# Trademarks
Any and all trademarks, either directly or indirectly mentioned in this project, belong to their respective owners.

# Useful Resources
The AdvancedHMI website [forum](https://www.advancedhmi.com/forum/), which is another open source project that has this app, its Mono version as well as its VB version.
