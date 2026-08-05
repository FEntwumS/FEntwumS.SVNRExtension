namespace FEntwumS.SVNRExtension.Elf.Dwarf.Constants;

/// <summary>Basistyp-Kodierungen (DW_ATE_*) fuer <see cref="DwAt.Encoding"/>.</summary>
internal enum DwAte : byte
{
    Address = 0x01,
    Boolean = 0x02,
    Float = 0x04,
    Signed = 0x05,
    SignedChar = 0x06,
    Unsigned = 0x07,
    UnsignedChar = 0x08,
}
