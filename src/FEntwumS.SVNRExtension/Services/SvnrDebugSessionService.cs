using Avalonia.Media;
using FEntwumS.SVNRExtension.Asm.Entities;
using FEntwumS.SVNRExtension.Sbdp;
using FEntwumS.SVNRExtension.Tools;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Debugger;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace FEntwumS.SVNRExtension.Services;

/// <summary>
/// Bringt eine Debug-Sitzung auf dem SVNR in Gang: bauen, laden, Stub starten, GDB anhaengen.
/// </summary>
/// <remarks>
/// Die Reihenfolge ist nicht frei waehlbar. Der Stub muss lauschen und die Hardware im
/// Debug-Modus stehen, bevor GDB sein <c>-target-select extended-remote</c> absetzt; deshalb
/// laeuft alles hier und nicht verteilt auf mehrere Menuepunkte.
/// </remarks>
public sealed class SvnrDebugSessionService
{
    public const string GdbAdapterId = "gdb";

    private readonly SvnrDebugBuildService _buildService;
    private readonly RemoteStubService _stubService;
    private readonly IDebuggerService _debuggerService;
    private readonly ISettingsService _settingsService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IOutputService _outputService;
    private readonly ILogger _logger;

    private bool _sessionWasActive;

    public SvnrDebugSessionService(
        SvnrDebugBuildService buildService,
        RemoteStubService stubService,
        IDebuggerService debuggerService,
        ISettingsService settingsService,
        IProjectExplorerService projectExplorerService,
        IOutputService outputService,
        ILogger logger)
    {
        _buildService = buildService;
        _stubService = stubService;
        _debuggerService = debuggerService;
        _settingsService = settingsService;
        _projectExplorerService = projectExplorerService;
        _outputService = outputService;
        _logger = logger;

        // Ohne diesen Rueckweg bliebe der COM-Port nach dem Ende der Sitzung belegt, und der
        // naechste Start scheiterte an einer Schnittstelle, die niemand mehr haelt.
        _debuggerService.StateChanged += OnDebuggerStateChanged;
    }

    public bool CanStart => ActiveSvnrProject is not null && !_debuggerService.IsActive;

    private UniversalFpgaProjectRoot? ActiveSvnrProject =>
        _projectExplorerService.ActiveProject as UniversalFpgaProjectRoot;

    public async Task StartAsync()
    {
        if (ActiveSvnrProject is not { } project)
        {
            _outputService.WriteLine("Kein FPGA-Projekt aktiv.", Brushes.Red);
            return;
        }

        var assemblerFile = SvnrSettingsHelper.GetAsmFile(project);
        if (assemblerFile == "none")
        {
            _outputService.WriteLine(
                "Keine .asm-Datei registriert. Im Explorer die Datei waehlen und " +
                "'Use this file to Compile' aufrufen.", Brushes.Red);
            return;
        }

        var assemblerPath = Path.Combine(project.FullPath, assemblerFile);

        try
        {
            _outputService.WriteLine($"Assembliere {assemblerFile}...");
            var artifacts = _buildService.Build(assemblerPath, project.FullPath);
            ReportDiagnostics(artifacts.Diagnostics);

            _outputService.WriteLine("Suche den SVNR...");
            var transport = SvnrPortLocator.Open(
                _settingsService.GetSettingValue<string>(FEntwumsSvnrExtensionModule.SerialPortSetting));

            var port = _stubService.Start(transport);
            _outputService.WriteLine($"Stub laeuft auf localhost:{port}.");

            _outputService.WriteLine("Lade das Programm auf den SVNR...");
            _stubService.LoadProgram(await File.ReadAllBytesAsync(artifacts.BinaryPath));

            var endpoint = $"localhost:{port}";
            _settingsService.SetSettingValue(FEntwumsSvnrExtensionModule.RemoteEndpointSetting, endpoint);

            var started = await _debuggerService.StartAsync(new DebugLaunchRequest(
                GdbAdapterId, artifacts.ElfPath, endpoint, project.FullPath));

            if (!started)
            {
                _outputService.WriteLine("GDB liess sich nicht starten.", Brushes.Red);
                _stubService.Stop();
                return;
            }

            _sessionWasActive = true;
            _outputService.WriteLine("Debug-Sitzung bereit.", Brushes.LightGreen);
        }
        catch (Exception exception)
        {
            _outputService.WriteLine($"Debug-Start fehlgeschlagen: {exception.Message}", Brushes.Red);
            _logger.Error(exception.Message, exception);
            _stubService.Stop();
        }
    }

    public void Stop()
    {
        _stubService.Stop();
    }

    private void ReportDiagnostics(IReadOnlyList<AssemblyDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var colour = diagnostic.Severity == AssemblySeverity.Error ? Brushes.Red : Brushes.Yellow;
            _outputService.WriteLine(diagnostic.ToString(), colour);
        }
    }

    private void OnDebuggerStateChanged(object? sender, EventArgs e)
    {
        if (_debuggerService.IsActive)
        {
            _sessionWasActive = true;
            return;
        }

        // Nur aufraeumen, wenn tatsaechlich eine Sitzung lief: Das Ereignis feuert auch beim
        // Wechsel zwischen Zustaenden ohne Sitzung.
        if (!_sessionWasActive) return;

        _sessionWasActive = false;

        if (!_stubService.IsRunning) return;

        _stubService.Stop();
        _outputService.WriteLine("Stub beendet, serielle Verbindung freigegeben.");
    }
}
