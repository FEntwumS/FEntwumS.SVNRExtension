namespace FEntwumS.SVNRExtension.Elf.Dwarf.Constants;

/// <summary>DWARF-Attributcodes (DW_AT_*).</summary>
internal enum DwAt : ushort
{
    Sibling = 0x01,
    Location = 0x02,
    Name = 0x03,
    ByteSize = 0x0b,
    StmtList = 0x10,
    LowPc = 0x11,
    HighPc = 0x12,
    Language = 0x13,
    CompDir = 0x1b,
    ConstValue = 0x1c,
    UpperBound = 0x2f,
    Producer = 0x25,
    Prototyped = 0x27,
    Count = 0x37,
    DataMemberLocation = 0x38,
    DeclColumn = 0x39,
    DeclFile = 0x3a,
    DeclLine = 0x3b,
    Declaration = 0x3c,
    Encoding = 0x3e,
    External = 0x3f,
    FrameBase = 0x40,
    Specification = 0x47,
    Type = 0x49,
}
