using FEntwumS.SVNRExtension.Sbdp.Constants;
using FEntwumS.SVNRExtension.Sbdp.Entities;
using FEntwumS.SVNRExtension.Sbdp.Exceptions;

namespace FEntwumS.SVNRExtension.Sbdp;

/// <summary>
/// Die SBDP-2-Operationen: Programm laden, laufen lassen, debuggen.
/// </summary>
/// <remarks>
/// Portierung von <c>bootloader_protocol.py</c>. Die Wartezeiten und die Reihenfolge der
/// Statusabfragen sind <em>nicht</em> Geschmackssache, sondern von der Hardware erzwungen - siehe
/// die Kommentare an den einzelnen Stellen und Abschnitt 6.2 der Design-Notizen. Insbesondere:
/// Die Hardware sendet nie unaufgefordert, und eine Statusabfrage ist an mehreren Stellen zugleich
/// die Quittung, ohne die die FSM stehen bleibt.
/// </remarks>
public sealed class SvnrBootloaderClient(ISbdpTransport transport)
{
    /// <summary>
    /// Wartezeit zwischen dem letzten Programmwort und der ersten Statusabfrage.
    /// </summary>
    /// <remarks>
    /// Uebernommen aus der Referenz (<c>time.sleep(0.5)</c>). Die FSM braucht nach dem letzten
    /// Wort einen Moment, bis <c>b_full</c> steht; fragt man zu frueh, kommt
    /// <see cref="SvnrError.RamNotFull" />.
    /// </remarks>
    private static readonly TimeSpan UploadSettleTime = TimeSpan.FromMilliseconds(500);

    /// <summary>Wartezeit nach einem Einzelschritt, bevor der Zustand abgefragt wird.</summary>
    private static readonly TimeSpan StepSettleTime = TimeSpan.FromMilliseconds(1);

    /// <summary>
    /// Wird fuer jeden gesendeten und empfangenen Rahmen gerufen - <c>true</c> heisst zum FPGA.
    /// </summary>
    public Action<bool, SbdpPacket>? Trace { get; set; }

    // ---- Grundlagen -------------------------------------------------------

    /// <summary>Fragt den Zustand ab und liefert die Antwort unbewertet.</summary>
    /// <remarks>
    /// Nicht nur eine Abfrage: <c>z_DEBUG_HALT</c> verlaesst die FSM ausschliesslich ueber ein
    /// <see cref="SvnrCommand.RequestState" /> (<c>decoder.vhd:426-432</c>). Ohne diesen Aufruf
    /// nimmt der Decoder danach keine Register- oder RAM-Zugriffe an.
    /// </remarks>
    public SbdpPacket? RequestState()
    {
        Send(SbdpPacket.Command(SvnrCommand.RequestState));
        return Receive();
    }

    /// <summary>Fragt den Zustand ab und besteht darauf, dass es einer der erlaubten ist.</summary>
    /// <exception cref="SbdpException">Etwas anderes kam an, oder nichts.</exception>
    public SvnrState RequireState(string operation, params SvnrState[] allowed)
    {
        var received = RequestState();

        if (received is { Type: SbdpType.Status } packet && allowed.Contains(packet.State))
            return packet.State;

        throw SbdpException.Unexpected(operation, received, string.Join(" oder ", allowed));
    }

    /// <summary>
    /// Prueft, ob am anderen Ende ueberhaupt ein SVNR haengt.
    /// </summary>
    /// <remarks>
    /// Laeuft das Programm gerade, wird zurueckgesetzt - sonst liesse sich eine Verbindung, die
    /// beim letzten Mal nicht sauber beendet wurde, nie wieder aufnehmen.
    /// </remarks>
    public bool TestCommunication()
    {
        var received = RequestState();
        if (received is not { Type: SbdpType.Status } packet) return false;

        switch (packet.State)
        {
            case SvnrState.PowerOn:
            case SvnrState.DebugInit:
                return true;
            case SvnrState.Running:
                ResetNormal();
                return true;
            default:
                return false;
        }
    }

    // ---- Programm laden ---------------------------------------------------

    /// <summary>
    /// Laedt ein Programmabbild in den SVNR-RAM.
    /// </summary>
    /// <param name="image">
    /// Rohabbild, genau <see cref="SbdpConstants.ImageSize" /> Byte: je Wort Highbyte zuerst.
    /// Woher die Datei stammt, spielt hier keine Rolle.
    /// </param>
    /// <remarks>
    /// Die Byte-Reihenfolge ist am Referenzcode belegt, nicht an der VHDL:
    /// <c>bootloader_protocol.py:128-130</c> liest zwei Byte aus der Datei und haengt sie
    /// unveraendert hinter das Typbyte (<c>msg = b"\x02" + data</c>). Das erste Byte der Datei
    /// wird also zum Highbyte.
    /// </remarks>
    /// <exception cref="ArgumentException">Das Abbild hat die falsche Groesse.</exception>
    /// <exception cref="SbdpException">Die Hardware war im falschen Zustand oder hat abgelehnt.</exception>
    public void Bootload(ReadOnlySpan<byte> image)
    {
        if (image.Length != SbdpConstants.ImageSize)
        {
            throw new ArgumentException(
                $"Das Abbild muss genau {SbdpConstants.ImageSize} Byte gross sein ({SbdpConstants.RamSize} Worte), " +
                $"ist aber {image.Length} Byte. Ein zu kurzes Abbild quittiert die Hardware mit " +
                "RamNotFull, ein zu langes mit RamOverflow.", nameof(image));
        }

        // Aus DebugInit muss erst zurueck nach PowerOn geschaltet werden - der Bootloader laeuft
        // nicht im Debug-Zweig der FSM.
        var state = RequireState("Vor dem Laden", SvnrState.PowerOn, SvnrState.DebugInit);
        if (state == SvnrState.DebugInit) SwitchToPowerOn();

        Send(SbdpPacket.Command(SvnrCommand.SendProgram));

        // Die 1024 Rahmen am Stueck. Die Referenz schreibt jeden einzeln; das kostet nur Zeit,
        // denn die Rate begrenzt ohnehin die UART.
        var framed = new byte[SbdpConstants.RamSize * SbdpConstants.PacketSize];
        for (var word = 0; word < SbdpConstants.RamSize; word++)
        {
            var target = framed.AsSpan(word * SbdpConstants.PacketSize);
            target[0] = (byte)SbdpType.ProgramData;
            target[1] = image[word * 2];       // Highbyte zuerst - siehe O-6
            target[2] = image[word * 2 + 1];
        }

        transport.SendRaw(framed);
        Thread.Sleep(UploadSettleTime);

        // Der dokumentierte Handshake braucht zwei Abfragen (H-5): erst meldet der Decoder, dass
        // sein Puffer voll ist, und erst nach dem Umkopieren durch den Runner steht wieder
        // PowerOn. Die zweite Abfrage wegzulassen laesst die FSM im Kopiervorgang stehen.
        RequireState("Nach dem Laden", SvnrState.RamFullOk);
        RequireState("Nach dem Umkopieren", SvnrState.PowerOn);
    }

    // ---- Normalbetrieb ----------------------------------------------------

    /// <summary>Startet das geladene Programm ausserhalb des Debug-Modus.</summary>
    public void RunNormal()
    {
        var state = RequireState("Vor dem Start", SvnrState.PowerOn, SvnrState.DebugInit);
        if (state == SvnrState.DebugInit) SwitchToPowerOn();

        Send(SbdpPacket.Command(SvnrCommand.Execute));
        RequireState("Nach dem Start", SvnrState.Running);
    }

    /// <summary>Setzt einen laufenden SVNR zurueck.</summary>
    public void ResetNormal()
    {
        Send(SbdpPacket.Command(SvnrCommand.Reset));
        RequireState("Nach dem Ruecksetzen", SvnrState.PowerOn);
    }

    // ---- Betriebsart ------------------------------------------------------

    public void SwitchToDebug()
    {
        Send(SbdpPacket.Command(SvnrCommand.SwitchDebug));
        RequireState("Umschalten in den Debug-Modus", SvnrState.DebugInit);
    }

    /// <summary>Setzt den SVNR zurueck, ohne den Debug-Modus zu verlassen.</summary>
    /// <remarks>
    /// Ohne Zustandspruefung, anders als <see cref="ResetNormal" />: Aus welchem Debug-Zustand
    /// heraus zurueckgesetzt wird, entscheidet, wo die FSM danach steht. Die Statusabfrage bleibt
    /// trotzdem noetig - sie ist die Quittung, ohne die es nicht weitergeht.
    /// </remarks>
    public void DebugReset()
    {
        Send(SbdpPacket.Command(SvnrCommand.Reset));

        if (RequestState() is null)
            throw new SbdpException("Ruecksetzen im Debug-Modus: keine Antwort.");
    }

    public void SwitchToPowerOn()
    {
        Send(SbdpPacket.Command(SvnrCommand.SwitchPowerOn));
        RequireState("Umschalten in den Normalbetrieb", SvnrState.PowerOn);
    }

    // ---- Ablaufsteuerung im Debug-Modus -----------------------------------

    /// <summary>Startet den Lauf im Debug-Modus, ohne auf sein Ende zu warten.</summary>
    /// <remarks>
    /// Kein <see cref="RequireState" /> danach: Der Aufrufer entscheidet, ob er pollt
    /// (<see cref="WaitForHalt" />) oder anhaelt.
    /// </remarks>
    public void DebugRun()
    {
        Send(SbdpPacket.Command(SvnrCommand.Execute));
    }

    /// <summary>
    /// Pollt, bis der SVNR haelt.
    /// </summary>
    /// <remarks>
    /// Pollen ist Pflicht, kein Notbehelf: <c>decoder.vhd</c> setzt <c>o_tx_trig</c> nur in
    /// Zweigen, die durch ein empfangenes Paket bewacht sind - ein Breakpoint-Treffer meldet sich
    /// also nie von selbst. Waehrend <c>z_DEBUG_RUNNING</c> nimmt die Hardware ausserdem nur
    /// <see cref="SvnrCommand.RequestState" /> und <see cref="SvnrCommand.Halt" /> an.
    /// <para>
    /// Ohne gesetzten Breakpoint kehrt der Aufruf nie zurueck - dann bleibt nur
    /// <see cref="Halt" /> ueber <paramref name="cancellationToken" />.
    /// </para>
    /// </remarks>
    public SvnrState WaitForHalt(TimeSpan pollInterval, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(pollInterval);

            var received = RequestState();
            if (received is not { Type: SbdpType.Status } packet) continue;

            // Halted deckt beides ab: Breakpoint-Treffer und Programmende. Die Hardware kann das
            // nicht unterscheiden (H-3), beides fuehrt ueber i_svnr_running = '0' hierher.
            if (packet.State == SvnrState.Halted) return packet.State;
        }
    }

    public void Halt()
    {
        Send(SbdpPacket.Command(SvnrCommand.Halt));
        RequireState("Nach dem Anhalten", SvnrState.Halted);
    }

    /// <summary>Fuehrt einen Einzelschritt aus.</summary>
    /// <remarks>
    /// Die Hardware dahinter ist ungetestet - <c>single_step.vhd:4</c> sagt es selbst. Schlaegt
    /// etwas beim Bringup fehl, ist das der erste Verdaechtige.
    /// </remarks>
    public void Step()
    {
        Send(SbdpPacket.Command(SvnrCommand.Step));
        Thread.Sleep(StepSettleTime);
        RequireState("Nach dem Einzelschritt", SvnrState.Halted);
    }

    // ---- Breakpoints ------------------------------------------------------

    /// <param name="address">
    /// Wortadresse. Auf dem Draht ist alles wortadressiert - der Wert geht ungewandelt gegen den
    /// Programmzaehler (<c>breakpoint_controller.vhd:65</c>).
    /// </param>
    /// <exception cref="SbdpException">
    /// Unter anderem bei <see cref="SvnrError.NoSpace" />: Die Hardware haelt nur
    /// <see cref="SbdpConstants.MaxBreakpoints" /> Stueck.
    /// </exception>
    public void AddBreakpoint(ushort address)
    {
        RequireAddress(address);
        Send(new SbdpPacket(SbdpType.AddBreakpoint, address));

        // Kein Sleep davor: Die Laufzeit der drei Bytes des Statuskommandos (rund 260 us bei
        // 115200) deckt das Rennen mit i_bp_edit_done zu (H-4). Wer hier "optimiert" und schneller
        // fragt, bekommt den Fehlercode aus z_ADDING_BP statt BreakpointAdded.
        RequireState($"Breakpoint bei 0x{address:x3} setzen", SvnrState.BreakpointAdded);
    }

    public void RemoveBreakpoint(ushort address)
    {
        RequireAddress(address);
        Send(new SbdpPacket(SbdpType.DeleteBreakpoint, address));
        RequireState($"Breakpoint bei 0x{address:x3} loeschen", SvnrState.BreakpointDeleted);
    }

    // ---- Speicher ---------------------------------------------------------

    /// <param name="address">Wortadresse, nicht Byteadresse.</param>
    public ushort ReadRam(ushort address)
    {
        RequireAddress(address);
        Send(new SbdpPacket(SbdpType.ReadRamAddress, address));

        var received = Receive();
        if (received is { Type: SbdpType.RamData } packet) return packet.Value;

        throw SbdpException.Unexpected($"RAM bei 0x{address:x3} lesen", received, "RamData");
    }

    /// <remarks>Die Hardware quittiert das Schreiben nicht - es gibt nichts zu pruefen.</remarks>
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
        if (received is { Type: SbdpType.RegisterData } packet) return packet.Value;

        throw SbdpException.Unexpected($"Register {register} lesen", received, "RegisterData");
    }

    /// <exception cref="ArgumentException">
    /// Bei <see cref="SvnrRegister.AluFlags" /> - die Flags sind read-only, die Hardware
    /// dekodiert kein Schreibkommando dafuer.
    /// </exception>
    public void WriteRegister(SvnrRegister register, ushort value)
    {
        var command = register switch
        {
            SvnrRegister.Akku => SvnrCommand.WriteAkku,
            SvnrRegister.Programmzaehler => SvnrCommand.WriteProgramCounter,
            SvnrRegister.Befehlsregister => SvnrCommand.WriteBefehlsregister,
            SvnrRegister.Hilfsregister => SvnrCommand.WriteHilfsregister,
            SvnrRegister.AluFlags => throw new ArgumentException(
                "Die ALU-Flags sind read-only.", nameof(register)),
            _ => throw new ArgumentOutOfRangeException(nameof(register))
        };

        Send(SbdpPacket.Command(command));
        Send(new SbdpPacket(SbdpType.RegisterData, value));
    }

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

    private static void RequireAddress(ushort address)
    {
        if (address >= SbdpConstants.RamSize)
            throw new ArgumentOutOfRangeException(nameof(address),
                $"Der SVNR hat {SbdpConstants.RamSize} Worte, 0x{address:x4} liegt ausserhalb.");
    }

    private void Send(SbdpPacket packet)
    {
        Trace?.Invoke(true, packet);
        transport.Send(packet);
    }

    private SbdpPacket? Receive()
    {
        var packet = transport.Receive();
        if (packet is { } received) Trace?.Invoke(false, received);
        return packet;
    }
}
