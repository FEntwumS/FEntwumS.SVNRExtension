using FEntwumS.SVNRExtension.Elf.Dwarf.Constants;

namespace FEntwumS.SVNRExtension.Elf.Dwarf;

/// <summary>
/// Ein (Attribut, Form)-Paar, wie es in <c>.debug_abbrev</c> steht - ohne Wert.
/// Bestandteil der <see cref="AbbrevSignature"/>.
/// </summary>
internal readonly record struct AbbrevAttribute(DwAt At, DwForm Form)
{
    public override string ToString() => $"{At}:{Form}";
}
