using FEntwumS.SVNRExtension.Sbdp.Constants;
using FEntwumS.SVNRExtension.Sbdp.Entities;
using FEntwumS.SVNRExtension.Sbdp.Exceptions;

namespace FEntwumS.SVNRExtension.Sbdp;

// SBDP-2-Operationen: Programm laden, laufen lassen, debuggen
// Portierung von bootloader_protocol.py - Wartezeiten und Reihenfolge sind hardware-erzwungen
public sealed class SvnrBootloaderClient(ISbdpTransport transport)
{
    // FSM braucht etwas Zeit nach dem letzten Wort, bis b_full steht (sonst RamNotFull)
    private static readonly TimeSpan UploadSettleTime = TimeSpan.FromMilliseconds(500);

    // Wartezeit nach einem Einzelschritt vor Statusabfrage
    private static readonly TimeSpan StepSettleTime = TimeSpan.FromMilliseconds(1);

    // Wartezeit nach Breakpoint-Änderung - sonst kommt NoSpace trotz erfolgreichem Eintrag
    private static readonly TimeSpan BreakpointSettleTime = TimeSpan.FromMilliseconds(1);

    // Spiegel der Breakpointtabelle im SVNR-Bootloader
    private readonly HashSet<ushort> _hardwareBreakpoints = [];

    public Action<bool, SbdpPacket>? Trace { get; set; }

    // ---- Grundlagen -------------------------------------------------------

    // Nicht nur Abfrage: z_DEBUG_HALT verlässt die FSM nur über RequestState
    // Ohne diesen Aufruf nimmt der Decoder keine Register/RAM-Zugriffe an
    public SbdpPacket? RequestState()
    {
        Send(SbdpPacket.Command(SvnrCommand.RequestState));
        return Receive();
    }

    public SvnrState RequireState(string operation, params SvnrState[] allowed)
    {
        // 1. Aktuelle Hardware-State abfragen
        var received = RequestState();

        // 2. Prüfen: Ist der State einer der erlaubten?
        if (received is { Type: SbdpType.Status } packet && allowed.Contains(packet.State))
            return packet.State;

        // 3. Wenn nein → Exception werfen
        throw SbdpException.Unexpected(operation, received, string.Join(" or ", allowed));
    }

    // Prüft SVNR-Präsenz. Reset bei Running/Debug-Zuständen, sonst keine saubere Wiederaufnahme
    public bool TestCommunication()
    {
        var received = RequestState();
        if (received is not { Type: SbdpType.Status } packet)
            return false;

        return HandleStateForCommunication(packet.State);
    }

    // Zustandsbehandlung für TestCommunication - jeder Zustand braucht eigene Reaktion
    private bool HandleStateForCommunication(SvnrState state)
    {
        if (state is SvnrState.PowerOn or SvnrState.DebugInit)
            return true;

        if (state is SvnrState.Running)
        {
            ResetNormal();
            return true;
        }

        if (state is SvnrState.DebugRunning or SvnrState.Halted)
        {
            // Zurücksetzen statt verwerfen - Board steht danach wie frisch eingesteckt
            DebugReset();
            SwitchToPowerOn();
            return true;
        }

        return false;
    }

    // ---- Programm laden ---------------------------------------------------

    // Byte-Reihenfolge: Highbyte zuerst (bootloader_protocol.py:128-130)
    public void Bootload(ReadOnlySpan<byte> image)
    {
        ValidateImageSize(image);
        PrepareAndUpload(image);
        WaitForUploadCompletion();
    }

    // Bildgröße muss exakt passen - Hardware antwortet mit RamNotFull/RamOverflow bei Abweichung
    private void ValidateImageSize(ReadOnlySpan<byte> image)
    {
        if (image.Length != SbdpConstants.ImageSize)
        {
            throw new ArgumentException(
                $"The image must be exactly {SbdpConstants.ImageSize} bytes ({SbdpConstants.RamSize} words), " +
                $"but is {image.Length} bytes. The hardware answers RamNotFull to a short image " +
                "and RamOverflow to a long one.", nameof(image));
        }
    }

    // Bootloader läuft nicht im Debug-Zweig - erst nach PowerOn wechseln
    private void PrepareAndUpload(ReadOnlySpan<byte> image)
    {
        var state = RequireState("Before the upload", SvnrState.PowerOn, SvnrState.DebugInit);
        if (state == SvnrState.DebugInit)
            SwitchToPowerOn();

        Send(SbdpPacket.Command(SvnrCommand.SendProgram));

        // 1024 Rahmen am Stück - Einzelversand kostet nur Zeit (UART limitiert)
        var framed = new byte[SbdpConstants.RamSize * SbdpConstants.PacketSize];
        for (var word = 0; word < SbdpConstants.RamSize; word++)
        {
            var target = framed.AsSpan(word * SbdpConstants.PacketSize);
            target[0] = (byte)SbdpType.ProgramData;
            target[1] = image[word * 2];       // Highbyte zuerst
            target[2] = image[word * 2 + 1];
        }

        transport.SendRaw(framed);
        Thread.Sleep(UploadSettleTime);
    }

    // Handshake braucht zwei Abfragen (H-5): erst Puffer voll, dann PowerOn nach Kopieren
    // Zweite Abfrage nicht weglassen - sonst bleibt FSM im Kopiervorgang stehen
    private void WaitForUploadCompletion()
    {
        RequireState("After the upload", SvnrState.RamFullOk);
        RequireState("After copying into RAM", SvnrState.PowerOn);
    }

    // ---- Normalbetrieb ----------------------------------------------------

    public void RunNormal()
    {
        var state = RequireState("Before the run", SvnrState.PowerOn, SvnrState.DebugInit);
        if (state == SvnrState.DebugInit)
            SwitchToPowerOn();

        Send(SbdpPacket.Command(SvnrCommand.Execute));
        RequireState("After the run started", SvnrState.Running);
    }

    public void ResetNormal()
    {
        Send(SbdpPacket.Command(SvnrCommand.Reset));
        RequireState("After the reset", SvnrState.PowerOn);
    }

    // ---- Betriebsart ------------------------------------------------------

    public void SwitchToDebug()
    {
        Send(SbdpPacket.Command(SvnrCommand.SwitchDebug));
        RequireState("Switching to debug mode", SvnrState.DebugInit);
    }

    // Ohne Zustandsprüfung - aus welchem Debug-Zustand zurückgesetzt wird, entscheidet FSM-Position
    // Statusabfrage bleibt nötig - sie ist die Quittung
    public void DebugReset()
    {
        Send(SbdpPacket.Command(SvnrCommand.Reset));

        if (RequestState() is null)
            throw new SbdpException("Reset in debug mode: no answer.");

        ClearBootloaderBreakpointRegister();
    }

    // Löscht jeden Breakpoint, den dieser Client gesetzt hat
    private void ClearBootloaderBreakpointRegister()
    {
        foreach (var address in _hardwareBreakpoints.ToArray())
        {
            try
            {
                RemoveBreakpoint(address);
            }
            catch (SbdpException exception) when (exception.Received is not null)
            {
                _hardwareBreakpoints.Remove(address);
                return;
            }
        }
    }

    public void SwitchToPowerOn()
    {
        Send(SbdpPacket.Command(SvnrCommand.SwitchPowerOn));
        RequireState("Switching to normal mode", SvnrState.PowerOn);
    }

    // ---- Ablaufsteuerung im Debug-Modus -----------------------------------

    // Kein RequireState danach - Aufrufer entscheidet ob gepollt oder angehalten wird
    public void DebugRun()
    {
        Send(SbdpPacket.Command(SvnrCommand.Execute));
    }

    public void Halt()
    {
        Send(SbdpPacket.Command(SvnrCommand.Halt));
        RequireState("After the halt", SvnrState.Halted);
    }

    // Hardware ungetestet (single_step.vhd:4) - erster Verdächtiger bei Bringup-Fehlern
    public void Step()
    {
        Send(SbdpPacket.Command(SvnrCommand.Step));
        Thread.Sleep(StepSettleTime);
        RequireState("After the single step", SvnrState.Halted);
    }

    // ---- Breakpoints ------------------------------------------------------

    // Wortadresse - auf dem Draht ist alles wortadressiert (breakpoint_controller.vhd:65)
    public void AddBreakpoint(ushort address)
    {
        RequireAddress(address);
        Send(new SbdpPacket(SbdpType.AddBreakpoint, address));

        // 3 Bytes Statuskommando (~260µs bei 115200) deckt Rennen mit i_bp_edit_done nicht
        // Wie beim Einzelschritt: erst warten, dann fragen
        Thread.Sleep(BreakpointSettleTime);

        _hardwareBreakpoints.Add(address);
        RequireState($"Setting breakpoint at 0x{address:x3}", SvnrState.BreakpointAdded);
    }

    public void RemoveBreakpoint(ushort address)
    {
        RequireAddress(address);
        Send(new SbdpPacket(SbdpType.DeleteBreakpoint, address));

        // Dasselbe Rennen wie beim Setzen, nur über z_DELETING_BP (decoder.vhd:451-463)
        Thread.Sleep(BreakpointSettleTime);
        RequireState($"Removing breakpoint at 0x{address:x3}", SvnrState.BreakpointDeleted);
        _hardwareBreakpoints.Remove(address);
    }

    // ---- Speicher ---------------------------------------------------------

    // Wortadresse, nicht Byteadresse
    public ushort ReadRam(ushort address)
    {
        RequireAddress(address);
        Send(new SbdpPacket(SbdpType.ReadRamAddress, address));

        var received = Receive();
        if (received is { Type: SbdpType.RamData } packet)
            return packet.Value;

        throw SbdpException.Unexpected($"Reading RAM at 0x{address:x3}", received, "RamData");
    }

    // Hardware quittiert Schreiben nicht - nichts zu prüfen
    public void WriteRam(ushort address, ushort value)
    {
        RequireAddress(address);
        Send(new SbdpPacket(SbdpType.WriteRamAddress, address));
        Send(new SbdpPacket(SbdpType.RamData, value));
    }

    // ---- Register ---------------------------------------------------------

    public ushort ReadRegister(SvnrRegister register)
    {
        Send(SbdpPacket.Command(ReadCommandFor(register)));

        var received = Receive();
        if (received is { Type: SbdpType.RegisterData } packet)
            return packet.Value;

        throw SbdpException.Unexpected($"Reading register {register}", received, "RegisterData");
    }

    // ALU-Flags sind read-only - Hardware dekodiert kein Schreibkommando dafür
    public void WriteRegister(SvnrRegister register, ushort value)
    {
        var command = register switch
        {
            SvnrRegister.Akku => SvnrCommand.WriteAkku,
            SvnrRegister.Programmzaehler => SvnrCommand.WriteProgramCounter,
            SvnrRegister.Befehlsregister => SvnrCommand.WriteBefehlsregister,
            SvnrRegister.Hilfsregister => SvnrCommand.WriteHilfsregister,
            SvnrRegister.AluFlags => throw new ArgumentException(
                "ALU flags are read-only.", nameof(register)),
            _ => throw new ArgumentOutOfRangeException(nameof(register))
        };

        Send(SbdpPacket.Command(command));
        Send(new SbdpPacket(SbdpType.RegisterData, value));
    }

    // Zentrale Mapping-Tabelle: Register ↔ Read-Command
    private static SvnrCommand ReadCommandFor(SvnrRegister register)
    {
        return register switch
        {
            SvnrRegister.Akku => SvnrCommand.ReadAkku,
            SvnrRegister.Programmzaehler => SvnrCommand.ReadProgramCounter,
            SvnrRegister.Befehlsregister => SvnrCommand.ReadBefehlsregister,
            SvnrRegister.Hilfsregister => SvnrCommand.ReadHilfsregister,
            SvnrRegister.AluFlags => SvnrCommand.ReadAluFlags,
            _ => throw new ArgumentOutOfRangeException(nameof(register))
        };
    }

    // ---- Intern -----------------------------------------------------------

    // SVNR hat nur RamSize Wörter - Adresse muss im gültigen Bereich liegen
    private static void RequireAddress(ushort address)
    {
        if (address >= SbdpConstants.RamSize)
            throw new ArgumentOutOfRangeException(nameof(address),
                $"The SVNR has {SbdpConstants.RamSize} words, 0x{address:x4} is outside that range.");
    }

    private void Send(SbdpPacket packet)
    {
        Trace?.Invoke(true, packet);
        transport.Send(packet);
    }

    private SbdpPacket? Receive()
    {
        // 1. Paket vom Transport empfangen (z. B. UART/Serial)
        var packet = transport.Receive();
    
        // 2. Wenn Paket nicht null → Trace-Callback aufrufen (Logging)
        if (packet is { } received)
            Trace?.Invoke(false, received); // false = empfangenes Paket, gesendete Pakete nutzen true)
    
        // 3. Paket zurückgeben
        return packet;
    }
}