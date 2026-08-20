using System.Text;
using FEntwumS.SVNRExtension.Rsp.Constants;
using FEntwumS.SVNRExtension.Sbdp;
using FEntwumS.SVNRExtension.Sbdp.Constants;

namespace FEntwumS.SVNRExtension.Rsp;

public static class GdbRegisterFile
{
    public static string ReadAll(SvnrBootloaderClient client)
    {
        var akku = client.ReadRegister(SvnrRegister.Akku);
        var programmzaehler = client.ReadRegister(SvnrRegister.Programmzaehler);
        var befehlsregister = client.ReadRegister(SvnrRegister.Befehlsregister);
        var hilfsregister = client.ReadRegister(SvnrRegister.Hilfsregister);
        var aluFlags = client.ReadRegister(SvnrRegister.AluFlags);

        var builder = new StringBuilder();

        for (var i = 0; i < GdbRegisterNumber.UnusedM68kRegisterCount; i++)
            builder.Append(RspHex.LittleEndian32(0));

        builder.Append(RspHex.LittleEndian16(akku));
        builder.Append(RspHex.LittleEndian16(programmzaehler));
        builder.Append(RspHex.LittleEndian16(befehlsregister));
        builder.Append(RspHex.LittleEndian16(hilfsregister));
        builder.Append(RspHex.LittleEndian16(FlagValue(aluFlags, AluFlagMask.SmallerZero)));
        builder.Append(RspHex.LittleEndian16(FlagValue(aluFlags, AluFlagMask.GreaterZero)));
        builder.Append(RspHex.LittleEndian16(FlagValue(aluFlags, AluFlagMask.EqualZero)));
        builder.Append(RspHex.LittleEndian16(0));

        builder.Append(RspHex.LittleEndian32(0));
        builder.Append(RspHex.LittleEndian32(programmzaehler));

        return builder.ToString();
    }

    public static string Read(SvnrBootloaderClient client, int registerNumber)
    {
        if (registerNumber == GdbRegisterNumber.InstructionPointer)
            return RspHex.LittleEndian32(client.ReadRegister(SvnrRegister.Programmzaehler));

        if (registerNumber is < GdbRegisterNumber.FirstSvnrRegister or > GdbRegisterNumber.LastSvnrRegister)
            return RspHex.LittleEndian32(0);

        return RspHex.LittleEndian16(registerNumber switch
        {
            GdbRegisterNumber.Akku => client.ReadRegister(SvnrRegister.Akku),
            GdbRegisterNumber.Programmzaehler => client.ReadRegister(SvnrRegister.Programmzaehler),
            GdbRegisterNumber.Befehlsregister => client.ReadRegister(SvnrRegister.Befehlsregister),
            GdbRegisterNumber.Hilfsregister => client.ReadRegister(SvnrRegister.Hilfsregister),
            GdbRegisterNumber.AluFlagSmallerZero =>
                FlagValue(client.ReadRegister(SvnrRegister.AluFlags), AluFlagMask.SmallerZero),
            GdbRegisterNumber.AluFlagGreaterZero =>
                FlagValue(client.ReadRegister(SvnrRegister.AluFlags), AluFlagMask.GreaterZero),
            GdbRegisterNumber.AluFlagEqualZero =>
                FlagValue(client.ReadRegister(SvnrRegister.AluFlags), AluFlagMask.EqualZero),
            _ => (ushort)0
        });
    }

    public static bool TryWrite(SvnrBootloaderClient client, int registerNumber, ushort value)
    {
        switch (registerNumber)
        {
            case GdbRegisterNumber.Akku:
                client.WriteRegister(SvnrRegister.Akku, value);
                return true;
            case GdbRegisterNumber.Programmzaehler:
                client.WriteRegister(SvnrRegister.Programmzaehler, value);
                return true;
            case GdbRegisterNumber.Befehlsregister:
                client.WriteRegister(SvnrRegister.Befehlsregister, value);
                return true;
            case GdbRegisterNumber.Hilfsregister:
                client.WriteRegister(SvnrRegister.Hilfsregister, value);
                return true;
            case GdbRegisterNumber.SvnrReset:
                if (value > 0) client.DebugReset();
                return true;
            default:
                return false;
        }
    }

    private static ushort FlagValue(ushort aluFlags, AluFlagMask mask)
    {
        return (ushort)((aluFlags & (ushort)mask) > 0 ? 1 : 0);
    }
}
