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

   
    public bool CanPrepare() // Bewusst nicht an der aktiven Toolchain festgemacht 
    {
        return ActiveSvnrProject is { } project && SvnrSettingsHelper.GetAsmFile(project) != "none";
    }

    // Wenn man den über den Käfer den Workflow anstößt, wir erstmal die Async Methode zur Vorbereitung des Stubs angestoßen.
    public async Task<DebugLaunchRequest?> PrepareAsync(CancellationToken ct = default)
    {
        if (ActiveSvnrProject is not { } project) // Checken ob man in einem FPGA Projekt ist. 
        {
            _outputService.WriteLine("Kein FPGA-Projekt aktiv.", Brushes.Red);
            return null;
        }

        var assemblerFile = SvnrSettingsHelper.GetAsmFile(project); // Assembler Quelldatei nehmen, auf der "der Cursor" ist. 
        if (assemblerFile == "none") // Wenn keine Assemblerdatei 
        {
            _outputService.WriteLine(
                "Keine .asm-Datei registriert. Im Explorer die Datei waehlen und " +
                "'Use this file to Compile' aufrufen.", Brushes.Red);
            return null;
        }

        var assemblerPath = Path.Combine(project.FullPath, assemblerFile); // Pfad zusammensetzen (Arbeitsverzeichnis)

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

            // Was an der Hardware scheitert, sieht GDB nur als E01. Ohne diese Zeile steht in der
            // Debugger Console am Ende eine Meldung ueber Speicher, und der wahre Grund fehlt.
            _stubService.Fault = message => _outputService.WriteLine(message, Brushes.Yellow);

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

   // Darf hier nicht liegen, weil es SVNR spezifisch ist. 
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
    
    
    private static string ToGdbPath(string path) // Vorwaertsschraegstriche, weil GDB Backslashes in Pfaden als Escape
    {
        return path.Replace(Path.DirectorySeparatorChar, '/');
    }

    
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
