0000: JA 40 ;Springe direkt auf Zeile 40
@FreeLines
0001: 1234
0030: 03ff
0031: 0001 ;Ausgabe Welche auf Speicher 6 geschrieben wird, weil dort die LED verknüft sind. 
0032: 0100 
0033: FFFF ;Konstante: Innerer Schleifen-Zähler 65535
0034: 0019 ;Konstante: Äußerer Schleifen-Zähler 25 
# 65535 mal 25 Iterationen ≈ 1,9 Mio Durchgänge bis sich die LED verändert.  
0035: 0000 
0036: 0031 # Pointer
0037: 0031 # Read-Only Pointer
@FreeLines
0040: LDM 31 
0041: STM 06
0042: LDM 34
0043: STM 35
0044: LDM 33
0045: DEC 00 
0046: JNA 45
0047: LDM 35
0048: DEC 00
0049: STM 35
004a: JNA 44
004b: LDM 31
004c: LEFT 01
004d: STM 31
004e: SUB 32
004f: JNA 40
0050: LDA 01
0051: STM 31
0054: JA 40
@FreeLines
0060: LDI 36
0061: LDI 37
0062: STM 36