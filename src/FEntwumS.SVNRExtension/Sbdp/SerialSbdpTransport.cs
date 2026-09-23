using System.IO.Ports;
using System.Runtime.InteropServices;
using FEntwumS.SVNRExtension.Sbdp.Constants;
using FEntwumS.SVNRExtension.Sbdp.Entities;

namespace FEntwumS.SVNRExtension.Sbdp;

// ISbdpTransport über serielle Schnittstelle: 115200 wie in Referenzimplementierung
public sealed class SerialSbdpTransport : ISbdpTransport
{
    private readonly SerialPort _port;
    private readonly byte[] _singleByte = new byte[1];

    // readTimeout: Referenz nutzt 100ms - trägt 3 Byte bei 115200 (~260µs) mit Reserve
    public SerialSbdpTransport(string portName, int readTimeout = 100)
    {
        _port = new SerialPort(portName, SbdpConstants.BaudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = readTimeout,
            WriteTimeout = 2000
        };

        try
        {
            _port.Open();
        }
        catch (UnauthorizedAccessException e)
        {
            // Häufigster Fehler unter Linux - Framework-Meldung nennt Ursache nicht
            // User muss in 'dialout' Gruppe sein oder anderer Prozess hält Port
            throw new UnauthorizedAccessException(
                $"No access to '{portName}'. On Linux this usually means the user is not in the " +
                "'dialout' group (sudo usermod -aG dialout $USER, then log in again). " +
                "Otherwise another process is holding the port open.", e);
        }

        // Puffer leeren - alte Daten von vorheriger Sitzung könnten sonst Störungen verursachen
        _port.DiscardInBuffer();
        _port.DiscardOutBuffer();
    }

    public void Send(SbdpPacket packet)
    {
        var buffer = packet.ToBytes();
        _port.Write(buffer, 0, buffer.Length);
    }

    public void SendRaw(ReadOnlySpan<byte> data)
    {
        _port.BaseStream.Write(data);
        _port.BaseStream.Flush();
    }

    // SerialPort.Read liefert was da ist - bei 3 Byte durchaus auch nur 1 Byte
    // Ohne Schleife entstünden aus einem Rahmen zwei halbe Pakete
    public SbdpPacket? Receive()
    {
        Span<byte> buffer = stackalloc byte[SbdpConstants.PacketSize];
        var filled = 0;

        while (filled < SbdpConstants.PacketSize)
        {
            int read;
            try
            {
                read = _port.Read(_singleByte, 0, 1);
            }
            catch (TimeoutException)
            {
                // Timeout beim Lesen - Paket unvollständig
                return null;
            }

            if (read == 0)
                return null;

            buffer[filled++] = _singleByte[0];
        }

        return SbdpPacket.FromBytes(buffer);
    }

    public void Flush()
    {
        if (!_port.IsOpen)
            return;

        _port.DiscardInBuffer();
        _port.DiscardOutBuffer();
    }

    public void Dispose()
    {
        if (_port.IsOpen)
            _port.Close();

        _port.Dispose();
    }

    // macOS: /dev/cu.* statt /dev/tty.* - tty blockiert bis DCD gesetzt wird (passiert nie bei USB-Seriell)
    // Linux: ttyUSB* (FTDI/CP210x) und ttyACM* (CDC-Geräte)
    public static IReadOnlyList<string> ListCandidatePorts()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return SafeEnumerate("/dev", "cu.*")
                .Where(x => !x.EndsWith("cu.Bluetooth-Incoming-Port", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToList();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return SafeEnumerate("/dev", "ttyUSB*")
                .Concat(SafeEnumerate("/dev", "ttyACM*"))
                .Order(StringComparer.Ordinal)
                .ToList();
        }

        return SerialPort.GetPortNames().Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Wrapper mit Exception-Handling - bei fehlendem Verzeichnis leere Liste statt Crash
    private static IEnumerable<string> SafeEnumerate(string directory, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern);
        }
        catch (Exception)
        {
            return [];
        }
    }
}