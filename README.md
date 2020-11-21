# ModbusMaster
Standalone Windows app supporting Modbus RTU, TCP, UDP, RTUoverTCP, RTUoverUDP, ASCIIoverRTU, ASCIIoverTCP and ASCIIoverUDP protocols.

Based on modified nModbus .NET 3.5 libraries, Copyright (c) 2006 Scott Alexander ( https://code.google.com/p/nmodbus/ ).

# Functionality
- Read the comments inside the form and also hover the mouse over the labels for hints.
- Apart from Int16, no modifier, it also supports U, F, L, UL and S modifiers (UInt16, Float32, Int32, UInt32, String)
- A support for 64-bit values was added - Float64, signed and unsigned Integer64. Use FQ, LQ and UQ modifiers (where "Q" stands for Quad Word).
- An experimental support for 128-bit values was added - signed and unsigned Integer128. Use LO and UO modifiers (where "O" stands for Octa Word).
- It also supports bit/character Reading/Writing:
  - select either consecutive bits/characters within a single element or the exact individual bit/character from each of the multiple elements.
  - either a single value or the exact number of comma separated values will be required for writing if Points number > 1.
- For RTU based protocols, on a single PC, this app can use the help of the com0com Windows program to provide virtual serial port pairs.
- Additional TextBox allows manual input of the serial port (intended for Linux so tty0tty virtual ports could be accessed).
- The library supports Masked Bit Write, function code 22 (0x16H), but the app also includes the built-in code for slave devices that don't support FC22.
- Addresses do NOT have offset of +1.

# Build
All it takes is to:

- Download and install Visual Studio community edition (ideally 2019).
- Download and extract the zip file of this project.
- Open this as an existing project in Visual Studio and, on the menu, do:
  - Build/Build Solution (or press Ctrl-Shift-B).
  - Debug/Start Debugging (or press F5) to run the app.
- Locate created EXE file in the /bin/Debug folder and copy it over to your preferred folder or Desktop.

# License
Licensed under MIT license - see the README.txt file inside the Resources folder.

# Trademarks
Any and all trademarks, either directly or indirectly mentioned in this project, belong to their respective owners.

# Useful Resources
The forum of AdvancedHMI website, which is another open source project that has this app, its Mono version as well as its VB version:

https://www.advancedhmi.com/forum/
