![Icon](https://raw.githubusercontent.com/FEntwumS/FEntwumS.SVNRExtension/main/Icon.png)

Assembler, loader and debugger for the SVNR (Simple Von Neumann Rechner), the 16-bit teaching computer of TH Köln.

### Features

- Syntax highlighting for SVNR assembly (`.asm`)
- Assembles into a binary and an ELF file with debug information
- Loads the program onto the board over the serial port
- Debugging on the hardware: breakpoints, single step, registers, memory and variables
- Registers, memory and variables can be changed while the program is halted

### Get Started

1. Install the **GNU Debugger** from the **Binaries** category of the package manager.
2. Create a new project from the template **Base SVNR for .asm programming**.
3. Synthesize the project and program the board once, so that the SVNR runs on the FPGA.
4. Right-click your `.asm` file and choose **Use this file to assemble**. A green check marks the active file.
5. Optional: set the port under **Settings → Tools → Debugger → SVNR Serial Port**. Leave it empty to probe all ports.
6. Start debugging from the Debug panel.
