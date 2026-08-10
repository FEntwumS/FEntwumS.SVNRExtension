namespace FEntwumS.SVNRExtension.Elf.Dwarf.Constants;

/// <summary>Quellsprachen (DW_LANG_*) fuer <see cref="DwAt.Language"/>.</summary>
internal enum DwLang : ushort
{
    C89 = 0x0001,
    C = 0x0002,
    C99 = 0x000c,
    C11 = 0x001d,
}
