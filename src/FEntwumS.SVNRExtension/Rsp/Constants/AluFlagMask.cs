namespace FEntwumS.SVNRExtension.Rsp.Constants;

[Flags]
public enum AluFlagMask : ushort
{
    SmallerZero = 0x01,
    GreaterZero = 0x02,
    EqualZero = 0x04
}
