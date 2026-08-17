namespace FEntwumS.SVNRExtension.Rsp.Constants;

public static class GdbRegisterNumber
{
    public const int FirstSvnrRegister = 0x10;
    public const int LastSvnrRegister = 0x16;

    public const int Akku = 0x10;
    public const int Programmzaehler = 0x11;
    public const int Befehlsregister = 0x12;
    public const int Hilfsregister = 0x13;
    public const int AluFlagSmallerZero = 0x14;
    public const int AluFlagGreaterZero = 0x15;
    public const int AluFlagEqualZero = 0x16;
    public const int SvnrReset = 0x17;
    public const int ProgramStatus = 0x18;
    public const int InstructionPointer = 0x19;

    public const int UnusedM68kRegisterCount = 16;
}
