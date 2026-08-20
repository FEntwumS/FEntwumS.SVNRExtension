using System.Net.Sockets;
using Avalonia.Media;
using FEntwumS.SVNRExtension.Asm.Entities;
using FEntwumS.SVNRExtension.Sbdp;
using FEntwumS.SVNRExtension.Tools;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace FEntwumS.SVNRExtension.Services;

/// <summary>
/// Bringt den SVNR in einen Zustand, in dem GDB andocken kann: bauen, laden, Stub starten.
/// </summary>
/// <remarks>
/// Die Reihenfolge ist nicht frei waehlbar. Der Stub muss lauschen und die Hardware im
/// Debug-Modus stehen, bevor GDB sein <c>-target-select extended-remote</c> absetzt; deshalb
/// laeuft alles hier und nicht verteilt auf mehrere Menuepunkte.
/// <para>
/// Gestartet wird nicht hier. Der Kern fragt ueber <see cref="IDebugLaunchProvider"/>, laesst
/// vorbereiten und startet mit der Anforderung, die dabei herauskommt - so bleibt der
/// Debug-Einstieg in der allgemeinen Oberflaeche und diese Erweiterung ohne eigene Knoepfe.
/// </para>
/// </remarks>
public sealed class SvnrDebugLaunchProvider : IDebugLaunchProvider
{
    public const string GdbAdapterId = "gdb";

    private readonly SvnrDebugBuildService _buildService;
    private readonly RemoteStubService _stubService;
    private readonly ISettingsService _settingsService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IOutputService _outputService;
    private readonly ILogger _logger;

    public SvnrDebugLaunchProvider(
        SvnrDebugBuildService buildService,
        RemoteStubService stubService,
        ISettingsService settingsService,
        IProjectExplorerService projectExplorerService,
        IOutputService outputService,
        ILogger logger)
    {
        _buildService = buildService;
        _stubService = stubService;
        _settingsService = settingsService;
        _projectExplorerService = projectExplorerService;
        _outputService = outputService;
        _logger = logger;
    }

    public string DisplayName => "SVNR (on-chip)";

    private UniversalFpgaProjectRoot? ActiveSvnrProject =>
        _projectExplorerService.ActiveProject as UniversalFpgaProjectRoot;

    /// <summary>
    /// Bewusst nicht an der aktiven Toolchain festgemacht: Debuggen soll auch dann gehen, wenn
    /// synthetisiert gerade niemand. Die registrierte <c>.asm</c> ist die einzige Bedingung, und
    /// sie zu lesen kostet nichts - der Kern ruft das beim Fuellen der Startauswahl.
    /// </summary>
    public bool CanPrepare()
    {
        return ActiveSvnrProject is { } project && SvnrSettingsHelper.GetAsmFile(project) != "none";
    }

    public async Task<DebugLaunchRequest?> PrepareAsync(CancellationToken ct = default)
    {
        if (ActiveSvnrProject is not { } project)
        {
            _outputService.WriteLine("Kein FPGA-Projekt aktiv.", Brushes.Red);
            return null;
        }

        var assemblerFile = SvnrSettingsHelper.GetAsmFile(project);
        if (assemblerFile == "none")
        {
            _outputService.WriteLine(
                "Keine .asm-Datei registriert. Im Explorer die Datei waehlen und " +
                "'Use this file to Compile' aufrufen.", Brushes.Red);
            return null;
        }

        var assemblerPath = Path.Combine(project.FullPath, assemblerFile);

        // Der Endpunkt aus den Einstellungen legt fest, an welchem Port der Stub lauscht. Vor
        // dem Assemblieren und vor dem Oeffnen der seriellen Schnittstelle geprueft: ein
        // Tippfehler soll nicht erst auffallen, wenn der COM-Port bereits belegt ist.
        if (!TryReadConfiguredPort(
                _settingsService.GetSettingValue<string>(FEntwumsSvnrExtensionModule.RemoteEndpointSetting),
                out var configuredPort, out var rejection))
        {
            _outputService.WriteLine(rejection, Brushes.Red);
            return null;
        }

        try
        {
            // Bei jedem Start neu assemblieren: nur so koennen Zeilentabelle und Quelltext nicht
            // auseinanderlaufen, und der Debugger haelt nicht stillschweigend an der falschen Stelle.
            _outputService.WriteLine($"Assembliere {assemblerFile}...");
            var artifacts = _buildService.Build(assemblerPath, project.FullPath);
            ReportDiagnostics(artifacts.Diagnostics);

            WriteGdbCommandFile(artifacts.ElfPath);

            ct.ThrowIfCancellationRequested();

            _outputService.WriteLine("Suche den SVNR...");
            var transport = SvnrPortLocator.Open(
                _settingsService.GetSettingValue<string>(FEntwumsSvnrExtensionModule.SerialPortSetting));

            var port = _stubService.Start(transport, configuredPort);
            _outputService.WriteLine($"Stub laeuft auf localhost:{port}.");

            ct.ThrowIfCancellationRequested();

            _outputService.WriteLine("Lade das Programm auf den SVNR...");
            _stubService.LoadProgram(await File.ReadAllBytesAsync(artifacts.BinaryPath, ct));

            var endpoint = $"localhost:{port}";

            // Nur zurueckschreiben, wenn der Port nicht vorgegeben war. Sonst stuende in der
            // Einstellung am Ende ein Wert, den niemand eingetragen hat - und genau das soll
            // sie nicht mehr sein.
            if (configuredPort == 0)
                _settingsService.SetSettingValue(FEntwumsSvnrExtensionModule.RemoteEndpointSetting, endpoint);

            return new DebugLaunchRequest(GdbAdapterId, artifacts.ElfPath, endpoint, project.FullPath);
        }
        catch (SocketException exception)
        {
            // Der haeufigste Fall bei fest eingetragenem Port: ihn haelt schon jemand - eine
            // vorige Sitzung, die noch nicht aufgeraeumt hat, oder ein fremdes Programm.
            var subject = configuredPort == 0 ? "Kein freier Port" : $"Port {configuredPort}";
            _outputService.WriteLine($"{subject} laesst sich nicht belegen: {exception.Message}", Brushes.Red);
            _logger.Error(exception.Message, exception);
            return null;
        }
        catch (OperationCanceledException)
        {
            _outputService.WriteLine("Vorbereitung abgebrochen.");
            return null;
        }
        catch (Exception exception)
        {
            // Aufgeraeumt wird nicht hier: Der Kern ruft CleanupAsync, sobald die Vorbereitung
            // ohne Sitzung endet. Das ist derselbe Weg wie beim regulaeren Sitzungsende.
            _outputService.WriteLine($"Debug-Start fehlgeschlagen: {exception.Message}", Brushes.Red);
            _logger.Error(exception.Message, exception);
            return null;
        }
    }

    /// <summary>
    /// Legt die GDB-Kommandodatei neben die Programmdatei.
    /// </summary>
    /// <remarks>
    /// GDB liest sie beim Start ueber <c>-x</c>, also lange bevor es sich an den Stub haengt.
    /// Genau darauf kommt es an: die Registeraufteilung muss stehen, bevor das erste
    /// <c>g</c>-Paket ausgewertet wird - dass der Stub dieselbe Beschreibung auf Anfrage
    /// ausliefert, kommt dafuer unter Umstaenden zu spaet.
    /// <para>
    /// Zwei der drei Zeilen sind streng genommen entbehrlich: die Architektur steht schon im
    /// ELF-Kopf und noch einmal in der Zielbeschreibung, und die Programmdatei uebergibt der
    /// Kern ohnehin als Argument. Sie stehen trotzdem darin, damit die Datei fuer sich allein
    /// vollstaendig ist - <c>gdb -x &lt;Programm&gt;.gdbinit</c> ohne weitere Argumente stellt
    /// denselben Zustand her, und genau das braucht man beim Suchen eines Fehlers.
    /// </para>
    /// <para>
    /// Die Reihenfolge ist nicht beliebig: erst die Architektur, dann die Registeraufteilung,
    /// dann die Symbole.
    /// </para>
    /// </remarks>
    private static void WriteGdbCommandFile(string elfPath)
    {
        string[] commands =
        [
            "set architecture m68k",
            $"set tdesc filename {ToGdbPath(RemoteStubService.TargetDescriptionPath())}",
            $"symbol-file {ToGdbPath(elfPath)}"
        ];

        File.WriteAllText(Path.ChangeExtension(elfPath, ".gdbinit"),
            string.Join(Environment.NewLine, commands) + Environment.NewLine);
    }

    // Vorwaertsschraegstriche, weil GDB Backslashes in Pfaden als Escape liest - dieselbe
    // Umschrift wie bei den Breakpoints. Anfuehrungszeichen braucht es nicht: beide Kommandos
    // nehmen den Rest der Zeile als Dateinamen, Leerzeichen darin stoeren also nicht.
    private static string ToGdbPath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Liest den Port aus der Einstellung <c>Tools -&gt; Debugger -&gt; Remote Endpoint</c>.
    /// </summary>
    /// <remarks>
    /// Leer heisst 0: dann sucht das Betriebssystem einen freien Port, und der gefundene wird
    /// nach dem Start zurueckgeschrieben. Ein eingetragener Port gilt unveraendert, auch wenn er
    /// belegt ist - der Start scheitert dann mit Ansage, statt still auf einen anderen
    /// auszuweichen und die Einstellung zur Luege zu machen.
    /// <para>
    /// Der Host muss auf diesen Rechner zeigen: der Stub lauscht auf <c>IPAddress.Loopback</c>,
    /// jede andere Adresse waere eine, unter der GDB ihn nie erreicht.
    /// </para>
    /// </remarks>
    private static bool TryReadConfiguredPort(string? endpoint, out int port, out string rejection)
    {
        port = 0;
        rejection = string.Empty;

        var value = endpoint?.Trim() ?? string.Empty;
        if (value.Length == 0) return true;

        // Von hinten getrennt, damit auch "[::1]:3333" richtig zerfaellt. Ohne Doppelpunkt gilt
        // die Eingabe als blosse Portnummer - so tippt es, wer nur den Port festlegen will.
        var separator = value.LastIndexOf(':');
        var host = separator < 0 ? string.Empty : value[..separator].Trim();
        var portText = separator < 0 ? value : value[(separator + 1)..].Trim();

        if (!int.TryParse(portText, out port) || port is < 0 or > 65535)
        {
            port = 0;
            rejection = $"Remote Endpoint '{value}': '{portText}' ist keine Portnummer. " +
                        "Erwartet wird host:port, etwa localhost:3333.";
            return false;
        }

        if (IsLoopback(host)) return true;

        port = 0;
        rejection = $"Remote Endpoint '{value}': Der Stub lauscht nur auf diesem Rechner. " +
                    "Als Host sind localhost oder 127.0.0.1 moeglich, sonst nichts.";
        return false;
    }

    private static bool IsLoopback(string host)
    {
        return host.Length == 0
               || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || host is "127.0.0.1" or "::1" or "[::1]";
    }

    /// <summary>
    /// Ohne diesen Rueckweg bliebe der COM-Port nach dem Ende der Sitzung belegt, und der
    /// naechste Start scheiterte an einer Schnittstelle, die niemand mehr haelt.
    /// </summary>
    public Task CleanupAsync()
    {
        // Laeuft auch dann, wenn gar nichts hochgefahren wurde - etwa weil schon der Assembler
        // aufgab. Dann ist hier nichts zu tun.
        if (!_stubService.IsRunning) return Task.CompletedTask;

        _stubService.Stop();
        _outputService.WriteLine("Stub beendet, serielle Verbindung freigegeben.");

        return Task.CompletedTask;
    }

    private void ReportDiagnostics(IReadOnlyList<AssemblyDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var colour = diagnostic.Severity == AssemblySeverity.Error ? Brushes.Red : Brushes.Yellow;
            _outputService.WriteLine(diagnostic.ToString(), colour);
        }
    }
}
