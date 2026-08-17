namespace FEntwumS.SVNRExtension.Elf.Entities;

/// <summary>
/// Eine vom Assembler erkannte SVNR-Variable. Entspricht <c>Variable</c> aus
/// <c>elfgen.py</c> und wird zu einem <see cref="DwTag.Variable"/>-DIE.
/// </summary>
/// <param name="Name">Anzeigename, z. B. <c>var_at_0x6</c>.</param>
/// <param name="Address">
/// Adresse im SVNR-Speicher, wie sie in den <c>DW_OP_addr</c>-Ausdruck geschrieben wird.
/// Achtung: Der Assembler liefert hier eine Byte-Adresse (Wortadresse * 2), waehrend die
/// Zeilentabelle Wortadressen verwendet.
/// </param>
/// <param name="Line">Quellzeile der Deklaration.</param>
public readonly record struct SvnrVariable(string Name, uint Address, int Line);
