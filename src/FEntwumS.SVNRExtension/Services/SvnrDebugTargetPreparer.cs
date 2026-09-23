using System.Net.Sockets;
using System.Xml;
using System.Xml.Linq;
using Avalonia.Media;
using FEntwumS.SVNRExtension.Asm.Entities; 
using FEntwumS.SVNRExtension.Sbdp;
using FEntwumS.SVNRExtension.Sbdp.Constants;
using FEntwumS.SVNRExtension.Tools; 
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace FEntwumS.SVNRExtension.Services;

public sealed class SvnrDebugTargetPreparer(
    SvnrDebugBuildService buildService,
    RemoteStubService stubService,
    ISettingsService settingsService,
    IProjectExplorerService projectExplorerService,
    IOutputService outputService,
    ILogger logger)
    : IDebugTargetPreparer
{
    private const string GdbBackendId = "gdb_server";
    
    public string DisplayName => "SVNRDebugPreparer";


    private UniversalFpgaProjectRoot? ActiveProject => projectExplorerService.ActiveProject as UniversalFpgaProjectRoot;
   
    //Wenn aktives Projekt UniveralFpga und DebugKit = SVNR in JSON
    public bool CanPrepare() { return ActiveProject is { } project && SvnrSettingsHelper.IsSvnrKit(project); }

    // Wenn man den über den Käfer den Workflow anstößt, wir erstmal die Async Methode zur Vorbereitung des Stubs angestoßen.
    public async Task<DebugLaunchRequest?> PrepareAsync()
    {
        if (ActiveProject is not { } project) // Checken ob man in einem FPGA Projekt ist. 
        {
            outputService.WriteLine("No FPGA project is active.", Brushes.Red);
            return null;
        }

        var assemblerFile = SvnrSettingsHelper.GetAsmFile(project); // Assembler Quelldatei nehmen, auf der "der Cursor" ist. 
        
        if (assemblerFile == "none") // Wenn keine Assemblerdatei 
        {
            outputService.WriteLine(
                "No *.asm-File registered to compile. Choose File in project Tree and call 'Use this file to Compile'.", Brushes.Red);
            return null;
        }

        var assemblerPath = Path.Combine(project.FullPath, assemblerFile); // Pfad zusammensetzen (Arbeitsverzeichnis)

        // Der Endpunkt aus den Einstellungen legt fest, an welchem Port der Stub lauscht. Vor
        // dem Assemblieren und vor dem Oeffnen der seriellen Schnittstelle geprueft: ein
        // Tippfehler soll nicht erst auffallen, wenn der COM-Port bereits belegt ist.
        if (!TryReadConfiguredPort(
                settingsService.GetSettingValue<string>(FEntwumsSvnrExtensionModule.RemoteEndpointSetting),
                out var configuredPort, out var rejection))
        {
            outputService.WriteLine(rejection, Brushes.Red);
            return null;
        }

        try
        {
            // Bei jedem Start neu assemblieren: nur so koennen Zeilentabelle und Quelltext nicht
            // auseinanderlaufen, und der Debugger haelt nicht stillschweigend an der falschen Stelle.
            outputService.WriteLine($"Assemble {assemblerFile}...");
            var artifacts = buildService.Build(assemblerPath, project.FullPath);
            ReportDiagnostics(artifacts.Diagnostics);


            outputService.WriteLine("Detect SVNR...");
            var transport = SvnrPortLocator.Open(
                settingsService.GetSettingValue<string>(FEntwumsSvnrExtensionModule.SerialPortSetting));

            // Was an der Hardware scheitert, sieht GDB nur als E01. Ohne diese Zeile steht in der
            // Debugger Console am Ende eine Meldung ueber Speicher, und der wahre Grund fehlt.
            stubService.Fault = message => outputService.WriteLine(message, Brushes.Yellow);

            var port = stubService.Start(transport, configuredPort);
            outputService.WriteLine($"Stub listening on localhost:{port}.");


            outputService.WriteLine("Uploading the program to the SVNR...");
            stubService.LoadProgram(await File.ReadAllBytesAsync(artifacts.BinaryPath));

            var endpoint = $"localhost:{port}";

            // Nur zurueckschreiben, wenn der Port nicht vorgegeben war. Sonst stuende in der
            // Einstellung am Ende ein Wert, den niemand eingetragen hat - und genau das soll
            // sie nicht mehr sein.
            if (configuredPort == 0)
                settingsService.SetSettingValue(FEntwumsSvnrExtensionModule.RemoteEndpointSetting, endpoint);

            return new DebugLaunchRequest(GdbBackendId, artifacts.ElfPath, endpoint, project.FullPath,
                CreateInitCommands(), CreateSvnrProfile());
        }
        catch (SocketException exception)
        {
            // Der haeufigste Fall bei fest eingetragenem Port: ihn haelt schon jemand - eine
            // vorige Sitzung, die noch nicht aufgeraeumt hat, oder ein fremdes Programm.
            var subject = configuredPort == 0 ? "No free port" : $"Port {configuredPort}";
            outputService.WriteLine($"{subject} could not be claimed: {exception.Message}", Brushes.Red);
            logger.Error(exception.Message, exception);
            return null;
        }
        catch (Exception exception)
        {
            // Aufgeraeumt wird nicht hier: Der Kern ruft CleanupAsync, sobald die Vorbereitung
            // ohne Sitzung endet. Das ist derselbe Weg wie beim regulaeren Sitzungsende.
            outputService.WriteLine($"Debug start failed: {exception.Message}", Brushes.Red);
            logger.Error(exception.Message, exception);
            return null;
        }
    }
    
    private static bool TryReadConfiguredPort(string? endpoint, out int port, out string rejection)
    {
        port = 0;
        rejection = string.Empty;

        // 1. Leere Eingabe ist valide (Default-Verhalten)
        var input = endpoint?.Trim() ?? string.Empty;
        if (input.Length == 0)
            return true;

        // 2. Host und Port trennen (Format: "host:port" oder nur "port")
        var lastColonIndex = input.LastIndexOf(':');
        var host = lastColonIndex < 0 ? string.Empty : input[..lastColonIndex].Trim();
        var portText = lastColonIndex < 0 ? input : input[(lastColonIndex + 1)..].Trim();

        // 3. Port validieren (muss Zahl im Bereich 0-65535 sein)
        if (!int.TryParse(portText, out port) || port is < 0 or > 65535)
        {
            rejection = $"Invalid port: {portText}";
            port = 0;
            return false;
        }

        // 4. Host validieren (nur Loopback erlaubt)
        if (IsLoopback(host))
            return true;

        rejection = $"Invalid host: {host}";
        port = 0;
        return false;
    }

    private static bool IsLoopback(string host)
    {
        // Leerer Host gilt als Loopback (Default-Verhalten)
        if (host.Length == 0)
            return true;

        // Explizite Loopback-Adressen
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || host is "127.0.0.1" or "::1" or "[::1]";
    }
    
    
    public Task CleanupAsync()
    {
        // Nothing to clean up if the stub was never started
        if (!stubService.IsRunning)
            return Task.CompletedTask;

        stubService.Stop(); // release serial port
        outputService.WriteLine("Stub stopped, serial connection released.");

        return Task.CompletedTask;
    }

    private void ReportDiagnostics(IReadOnlyList<AssemblyDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var colour = diagnostic.Severity == AssemblySeverity.Error ? Brushes.Red : Brushes.Yellow;
            outputService.WriteLine(diagnostic.ToString(), colour);
        }
    }
    
    private string[] CreateInitCommands()
    {
        return
        [
            "set architecture m68k", // Für Motorola
            $"set tdesc filename {NormalizePath(AccessAssetsUtil.TargetDescriptionPath())}",
            "set breakpoint always-inserted on"
        ];
    }
    
    private DebugTargetProfile CreateSvnrProfile()
    {
        return new DebugTargetProfile
        {
            AddressableUnitBytes = 2,
            Registers = SvnrRegisters(),
            MaxBreakpoints = SbdpConstants.MaxBreakpoints,
            HasCallStack = false,
            AddressWatermark = "Wortadresse im SVNR-RAM, z. B. 0x0"
        };
    }
    
    private static string NormalizePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/');
    }
    

    private IReadOnlyList<string>? SvnrRegisters()
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore
            };

            using var reader = XmlReader.Create(
                AccessAssetsUtil.TargetDescriptionPath(),
                settings);

            var document = XDocument.Load(reader);

            return document
                .Descendants("reg")
                .Where(reg => string.Equals(
                    (string?)reg.Attribute("group"),
                    "SVNR",
                    StringComparison.OrdinalIgnoreCase))
                .Select(reg => (string?)reg.Attribute("name"))
                .OfType<string>()
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
        }
        catch (Exception exception)
        {
            logger.Error(exception.Message, exception);
            return null;
        }
    }
}
