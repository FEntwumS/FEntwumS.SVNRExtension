using System.Text;
using FEntwumS.SVNRExtension.Rsp.Constants;
using FEntwumS.SVNRExtension.Sbdp;
using FEntwumS.SVNRExtension.Sbdp.Constants;
using FEntwumS.SVNRExtension.Sbdp.Exceptions;

namespace FEntwumS.SVNRExtension.Rsp;

public sealed class RspCommandProcessor(
    SvnrBootloaderClient client,
    string targetDescription,
    Func<bool> interruptRequested)
{
    private const string SupportedFeatures =
        "PacketSize=1000;qXfer:features:read+;hwbreak+;swbreak-;multiprocess-";

    private const string TargetDescriptionPrefix = "qXfer:features:read:";
    private const int BytesPerWord = 2;

    private static readonly TimeSpan RunPollInterval = TimeSpan.FromMilliseconds(300);

    private bool _attachContinuePending = true;

    public string Process(string command)
    {
        try
        {
            return Dispatch(command);
        }
        catch (Exception exception) when (exception is SbdpException or FormatException
                                              or ArgumentException or IndexOutOfRangeException)
        {
            return RspResponse.GenericError;
        }
    }

    private string Dispatch(string command)
    {
        return command switch
        {
            "!" => RspResponse.Ok,
            "?" => RspResponse.TrapSignal,
            "c" => ContinueExecution(),
            "s" => SingleStep(),
            "g" => GdbRegisterFile.ReadAll(client),
            "D" or "k" => RspResponse.Ok,
            RspStream.InterruptCommand => HaltTarget(),
            _ when command.StartsWith("qSupported", StringComparison.Ordinal) => SupportedFeatures,
            _ when command.StartsWith(TargetDescriptionPrefix, StringComparison.Ordinal) =>
                DescribeTarget(command),
            _ when command.StartsWith("Z0,", StringComparison.Ordinal) => SetBreakpoint(command),
            _ when command.StartsWith("z0,", StringComparison.Ordinal) => ClearBreakpoint(command),
            _ when command.StartsWith('p') => ReadRegister(command),
            _ when command.StartsWith('P') => WriteRegister(command),
            _ when command.StartsWith('m') => ReadMemory(command),
            _ when command.StartsWith('M') => WriteMemory(command),
            _ => RspResponse.Unsupported
        };
    }

    private string ContinueExecution()
    {
        if (_attachContinuePending)
        {
            _attachContinuePending = false;
            return RspResponse.TrapSignal;
        }

        client.DebugRun();

        while (true)
        {
            if (interruptRequested()) return HaltTarget();

            Thread.Sleep(RunPollInterval);

            if (client.RequestState() is { Type: SbdpType.Status, State: SvnrState.Halted })
                return RspResponse.TrapSignal;
        }
    }

    private string SingleStep()
    {
        client.Step();
        return RspResponse.TrapSignal;
    }

    private string HaltTarget()
    {
        client.Halt();
        return RspResponse.TrapSignal;
    }

    private string SetBreakpoint(string command)
    {
        client.AddBreakpoint(ParseWordAddress(command));
        return RspResponse.Ok;
    }

    private string ClearBreakpoint(string command)
    {
        client.RemoveBreakpoint(ParseWordAddress(command));
        return RspResponse.Ok;
    }

    private string ReadRegister(string command)
    {
        return GdbRegisterFile.Read(client, Convert.ToInt32(command[1..], 16));
    }

    private string WriteRegister(string command)
    {
        var assignment = command[1..].Split('=');
        var registerNumber = Convert.ToInt32(assignment[0], 16);
        var value = RspHex.ParseLittleEndianWord(assignment[1]);

        return GdbRegisterFile.TryWrite(client, registerNumber, value)
            ? RspResponse.Ok
            : RspResponse.GenericError;
    }

    private string ReadMemory(string command)
    {
        var (byteAddress, byteCount) = ParseAddressAndLength(command[1..]);

        if (ExceedsRam(byteAddress, byteCount)) return RspResponse.GenericError;
        if (!IsWordAligned(byteAddress, byteCount)) return ZeroBytes(byteCount);

        var wordAddress = ToWordAddress(byteAddress);
        var response = new StringBuilder();

        for (var word = 0; word < byteCount / BytesPerWord; word++)
            response.Append(RspHex.LittleEndian16(client.ReadRam((ushort)(wordAddress + word))));

        return response.ToString();
    }

    private string WriteMemory(string command)
    {
        var separator = command.IndexOf(':');
        var (byteAddress, byteCount) = ParseAddressAndLength(command[1..separator]);

        if (ExceedsRam(byteAddress, byteCount)) return RspResponse.GenericError;
        if (!IsWordAligned(byteAddress, byteCount)) return ZeroBytes(byteCount);

        var wordAddress = ToWordAddress(byteAddress);
        var values = RspHex.ParseLittleEndianWords(command[(separator + 1)..]);

        for (var word = 0; word < values.Count; word++)
            client.WriteRam((ushort)(wordAddress + word), values[word]);

        return RspResponse.Ok;
    }

    private string DescribeTarget(string command)
    {
        var annexAndRange = command[TargetDescriptionPrefix.Length..];
        var range = annexAndRange[(annexAndRange.IndexOf(':') + 1)..].Split(',');

        var offset = Convert.ToInt32(range[0], 16);
        if (offset >= targetDescription.Length) return "l";

        var length = Math.Min(Convert.ToInt32(range[1], 16), targetDescription.Length - offset);
        var marker = offset + length < targetDescription.Length ? "m" : "l";

        return marker + targetDescription.Substring(offset, length);
    }

    private static ushort ParseWordAddress(string breakpointCommand)
    {
        return Convert.ToUInt16(breakpointCommand[3..].Split(',')[0], 16);
    }

    private static (int ByteAddress, int ByteCount) ParseAddressAndLength(string arguments)
    {
        var parts = arguments.Split(',');
        return (Convert.ToInt32(parts[0], 16), Convert.ToInt32(parts[1], 16));
    }

    private static ushort ToWordAddress(int byteAddress)
    {
        return (ushort)(byteAddress / BytesPerWord);
    }

    private static bool IsWordAligned(int byteAddress, int byteCount)
    {
        return byteAddress % BytesPerWord == 0 && byteCount % BytesPerWord == 0;
    }

    private static bool ExceedsRam(int byteAddress, int byteCount)
    {
        return byteAddress + byteCount > SbdpConstants.ImageSize;
    }

    private static string ZeroBytes(int byteCount)
    {
        return new string('0', byteCount * 2);
    }
}
