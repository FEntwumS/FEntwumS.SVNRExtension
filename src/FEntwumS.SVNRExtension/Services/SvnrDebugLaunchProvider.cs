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

        try
        {
            // Bei jedem Start neu assemblieren: nur so koennen Zeilentabelle und Quelltext nicht
            // auseinanderlaufen, und der Debugger haelt nicht stillschweigend an der falschen Stelle.
            _outputService.WriteLine($"Assembliere {assemblerFile}...");
            var artifacts = _buildService.Build(assemblerPath, project.FullPath);
            ReportDiagnostics(artifacts.Diagnostics);

            ct.ThrowIfCancellationRequested();

            _outputService.WriteLine("Suche den SVNR...");
            var transport = SvnrPortLocator.Open(
                _settingsService.GetSettingValue<string>(FEntwumsSvnrExtensionModule.SerialPortSetting));

            var port = _stubService.Start(transport);
            _outputService.WriteLine($"Stub laeuft auf localhost:{port}.");

            ct.ThrowIfCancellationRequested();

            _outputService.WriteLine("Lade das Programm auf den SVNR...");
            _stubService.LoadProgram(await File.ReadAllBytesAsync(artifacts.BinaryPath, ct));

            var endpoint = $"localhost:{port}";
            _settingsService.SetSettingValue(FEntwumsSvnrExtensionModule.RemoteEndpointSetting, endpoint);

            return new DebugLaunchRequest(GdbAdapterId, artifacts.ElfPath, endpoint, project.FullPath);
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
