namespace FEntwumS.SVNRExtension.Elf.Dwarf.Constants;

/// <summary>DWARF-Tags (DW_TAG_*) - die Art eines <see cref="Die"/>.</summary>
internal enum DwTag : ushort
{
    ArrayType = 0x01,
    LexicalBlock = 0x0b,
    Member = 0x0d,
    PointerType = 0x0f,
    CompileUnit = 0x11,
    StructureType = 0x13,
    Typedef = 0x16,
    UnionType = 0x17,
    BaseType = 0x24,
    ConstType = 0x26,
    EnumerationType = 0x04,
    Enumerator = 0x28,
    FormalParameter = 0x05,
    Subprogram = 0x2e,
    Variable = 0x34,
    VolatileType = 0x35,
    SubrangeType = 0x21,
}
