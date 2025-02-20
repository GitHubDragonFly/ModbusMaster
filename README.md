# ModbusMaster
Standalone Windows app supporting Modbus `RTU` , `TCP` , `UDP` , `RTUoverTCP` , `RTUoverUDP` , `ASCIIoverRTU` , `ASCIIoverTCP` and `ASCIIoverUDP` protocols.

Also included are its Mono versions for Linux and Mac OS X, these are VB Net versions so:
- For Linux you will have to install `mono-complete` and `mono-vbnc` packages
- Mac might be different depending on the OS X version, maybe install `mono` and `mono-basic` packages

If a firewall is enabled then it might prompt you to allow this app to communicate on the network:
- Normally it should be allowed to communicate on the private network otherwise it might not work properly
  - Do not allow public access unless you know what you are doing
- Once the testing is done then remember to remove this app from the firewall's list of allowed apps

The app is designed to allow running multiple instances of the app at the same time, for example:
- Use the same protocol for each instance but with different port numbers, similar to:
  - IP 127.0.0.1 TCP Port 501 and IP 127.0.0.1 TCP Port 502
- Use a mix of different protocols with help of other tools (like [com0com](https://pete.akeo.ie/search/label/com0com) for RTU protocol on Windows)

This is all based on modified [nModbus](https://code.google.com/p/nmodbus/) .NET 3.5 libraries:
- MIT Licensed Copyright (c) 2006 Scott Alexander:
- These are included as a resource for Windows version but are separate for Mono version

Intended to be used as a quick testing tool:
- Can be tested with its counterpart [ModbusSlaveSimulation](https://github.com/GitHubDragonFly/ModbusSlaveSimulation) (check the video further below)

An easy alternative to use instead would be the [AdvancedHMI](https://www.advancedhmi.com/) software since it is highly functional and free.

# Screenshot

![Start Page](screenshots/Modbus%20Master.png?raw=true)

# Functionality
- Read the comments inside the form and also hover the mouse over the labels for hints.
- No Offset Addressing (where xxxxx goes from 00000 up to 65534):
  - Coils = 0xxxxx
  - Discrete Inputs = 1xxxxx
  - Input Registers = 3xxxxx
  - Holding Registers = 4xxxxx
- Apart from `Int16`, which is register adress only without modifier, this app also supports:
  - `U`, `F`, `L`, `UL` and `S` modifiers ( which are used for `UInt16`, `Float32`, `Int32`, `UInt32`, `String` )
- A support for 64-bit values was added - Float64, signed and unsigned Integer64:
  - Use `FQ`, `LQ` and `UQ` modifiers ( where `Q` stands for Quad Word )
- An experimental support for 128-bit values was added - signed and unsigned Integer128:
  - Use `LO` and `UO` modifiers ( where `O` stands for Octa Word )
- It also supports bit/character Reading/Writing:
  - select either consecutive bits/characters within a single element or the exact individual bit/character from each of the multiple elements
  - either a single value or the exact number of comma separated values will be required for writing if `Points` number > 1
- For RTU based protocols, on a single PC, this app can use the help of:
  - The [com0com](https://pete.akeo.ie/search/label/com0com) Windows program to provide virtual serial port pairs
  - Additional TextBox allows manual input of the serial port, intended for Linux so [tty0tty](https://github.com/freemed/tty0tty) virtual port pairs, like `/dev/tnt0` <=> `/dev/tnt1`, could be accessed
    - This box was removed in the Mac Mono version
- The library supports `Masked Bit Write`, function code 22 (0x16H or FC22)
- The app also includes the built-in code for slave devices that don't support `FC22`:
  - This entails `read-modify-write` process which can take a little time and could overwrite values that changed during its running

IMPORTANT: Exercise caution when attempting to write any value to the PLC.

# Usage

## -> For Windows
- Either use the Windows executable files from the `exe` folder or follow the instructions below to build it yourself:
  - Download and install Visual Studio community edition (ideally 2019)
  - Download and extract the zip file of this project
  - Open this as an existing project in Visual Studio and, on the menu, do:
    - Build/Build Solution (or press Ctrl-Shift-B)
    - Debug/Start Debugging (or press F5) to run the app
  - Locate created EXE file in the `/bin/Debug` folder and copy it over to your preferred folder or the Desktop

## -> For Mono
- Make sure that Mono is installed on your computer:
  - Both `mono-complete` and `mono-vbnc` packages for Linux
  - For Mac you might need to experiment, maybe install `mono` and `mono-basic` packages
- Download and extract the zip file of this project and locate the Mono zip archive in the `Mono` folder
- Extract 4 files and potentially rename the newly created folder and/or exe file to something shorter if you wish, just to make the terminal navigation quicker
- Open the terminal, navigate to the folder and type: `sudo mono ModbusMaster.exe`:
  - On Mac you might need to switch to the superuser `su` account
- For testing RTU protocols, on Linux you can possibly install and use [tty0tty](https://github.com/freemed/tty0tty) virtual port pairs while on Mac the later OS X versions seem to have pseudo terminals - pairs of devices such as `/dev/ptyp3` <=> `/dev/ttyp3`

Note for Mac users: this was tested on an old iMac G5 PowerPC computer with Mono v2.10.2. Some odd behaviour was present in a sense that the app was loosing focus thus disrupting TCP communication in Auto Read mode. There is a text box with red X that you can click to try to maintain focus (if you do something else afterwards then click it again). Since I cannot test it in any other way then it is left for you to experiment.

# Video

https://github.com/user-attachments/assets/dff08e3f-8fd6-417b-b4b9-fcf1b72759ad

# License
Licensed under MIT license - see the NModbus MIT License within the README.txt file inside the Resources folder.

# Trademarks
Any and all trademarks, either directly or indirectly mentioned in this project, belong to their respective owners.

# Useful Resources
The AdvancedHMI website [forum](https://www.advancedhmi.com/forum/), which is another open source project.
