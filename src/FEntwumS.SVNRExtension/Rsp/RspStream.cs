using System.Net.Sockets;
using System.Text;

namespace FEntwumS.SVNRExtension.Rsp;

public sealed class RspStream(NetworkStream stream)
{
    public const string InterruptCommand = "\u0003";

    private const byte InterruptByte = 0x03;
    private const byte PacketStart = (byte)'$';
    private const byte PacketEnd = (byte)'#';
    private const byte Acknowledge = (byte)'+';
    private const byte Retransmit = (byte)'-';
    private const int ChecksumLength = 2;
    private const int MaxSendAttempts = 3;

    private readonly StringBuilder _payload = new();

    public Action<bool, string>? Trace { get; set; }

    public string? ReadCommand()
    {
        while (true)
        {
            var next = stream.ReadByte();
            if (next < 0) return null;

            if (next == InterruptByte)
            {
                Trace?.Invoke(false, InterruptCommand);
                return InterruptCommand;
            }

            if (next != PacketStart) continue;

            var command = ReadPayloadAndChecksum();
            if (command is null) return null;

            Trace?.Invoke(false, command);
            return command;
        }
    }

    public bool TryConsumeInterrupt()
    {
        while (stream.DataAvailable)
        {
            var next = stream.ReadByte();
            if (next < 0) return false;
            if (next == InterruptByte)
            {
                Trace?.Invoke(false, InterruptCommand);
                return true;
            }
        }

        return false;
    }

    public void Send(string payload)
    {
        Trace?.Invoke(true, payload);

        var frame = Encoding.ASCII.GetBytes($"${payload}#{Checksum(payload)}");

        for (var attempt = 0; attempt < MaxSendAttempts; attempt++)
        {
            stream.Write(frame, 0, frame.Length);
            stream.Flush();

            if (WaitForAcknowledge()) return;
        }
    }

    private string? ReadPayloadAndChecksum()
    {
        _payload.Clear();

        while (true)
        {
            var next = stream.ReadByte();
            if (next < 0) return null;
            if (next == PacketEnd) break;

            _payload.Append((char)next);
        }

        var received = new char[ChecksumLength];
        for (var i = 0; i < ChecksumLength; i++)
        {
            var next = stream.ReadByte();
            if (next < 0) return null;
            received[i] = (char)next;
        }

        var command = _payload.ToString();

        if (!new string(received).Equals(Checksum(command), StringComparison.OrdinalIgnoreCase))
        {
            stream.WriteByte(Retransmit);
            stream.Flush();
            return ReadCommandAfterFailedChecksum();
        }

        stream.WriteByte(Acknowledge);
        stream.Flush();
        return command;
    }

    private string? ReadCommandAfterFailedChecksum()
    {
        while (true)
        {
            var next = stream.ReadByte();
            if (next < 0) return null;
            if (next == PacketStart) return ReadPayloadAndChecksum();
        }
    }

    private bool WaitForAcknowledge()
    {
        while (true)
        {
            var next = stream.ReadByte();
            if (next < 0) return true;
            if (next == Acknowledge) return true;
            if (next == Retransmit) return false;
        }
    }

    private static string Checksum(string payload)
    {
        var sum = 0;
        foreach (var character in payload) sum += (byte)character;
        return (sum % 256).ToString("x2");
    }
}
