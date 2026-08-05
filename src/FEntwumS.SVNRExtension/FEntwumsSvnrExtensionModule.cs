using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FEntwumS.SVNRExtension.Services;
using FEntwumS.SVNRExtension.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration.ViewModels;
using OneWare.OssCadSuiteIntegration.Views;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;


namespace FEntwumS.SVNRExtension;

public class FEntwumsSvnrExtensionModule : OneWareModuleBase
{
    public static readonly string[] SupportedExtensions = [".asm"];

    public override IReadOnlyCollection<string> Dependencies
        => ["OssCadSuiteIntegrationModule"];

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<AsmConverterService>();
        services.AddSingleton<SvnrToolchainService>();
        //services.AddSingleton<DebuggingStub>();
    }

    /// <summary>
    ///     Schluessel der Einstellung, in die der Endpunkt des Stubs geschrieben wird.
    /// </summary>
    /// <remarks>
    ///     Wortgleich mit <c>OneWare.Debugger.DebuggerModule.RemoteEndpointSetting</c>. Der Kern
    ///     veroeffentlicht <c>OneWare.Debugger</c> nicht als NuGet-Paket, deshalb laesst sich die
    ///     Konstante von hier aus nicht referenzieren - dieselbe Kopplung ueber einen blossen String
    ///     wie beim GDB-Pfad. Aendert der Kern den Schluessel, faellt das erst zur Laufzeit auf.
    /// </remarks>
    private const string RemoteEndpointSetting = "FEntwumS_Debugger_RemoteEndpoint";

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var asmConverterService = serviceProvider.Resolve<AsmConverterService>();
        var projectExplorerService = serviceProvider.Resolve<IProjectExplorerService>();
        var windowService = serviceProvider.Resolve<IWindowService>();
        var fpgaService = serviceProvider.Resolve<FpgaService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var paths = serviceProvider.Resolve<IPaths>();
        serviceProvider.Resolve<IPackageService>().RegisterPackage(GdbPackage);

        // Erst nach dem Hochfahren prüfen: vorher existiert das Hauptfenster und damit der
        // NotificationManager noch nicht.
        serviceProvider.Resolve<IApplicationStateService>()
            .RegisterAutoLaunchAction(_ => NotifyIfGdbMissing(serviceProvider));

        serviceProvider.Resolve<FpgaService>().RegisterToolchain<SvnrToolchain>();

        fpgaService.RegisterLanguage("ASM", SupportedExtensions);
        var languageManager = serviceProvider.Resolve<ILanguageManager>();

        languageManager.RegisterTextMateLanguage("asm", "avares:// FEntwumS.SVNRExtension/Assets/asm.tmLanguage.json",
            SupportedExtensions);

        // Die Grammatik faerbt nur ein. Erst die TypeAssistance macht .asm zu einer Sprache, die
        // der Editor kennt - und damit zu einer, in deren Randspalte sich Breakpoints setzen
        // lassen. Siehe AsmTypeAssistance.CanAddBreakPoints.
        languageManager.RegisterStandaloneTypeAssistance(typeof(AsmTypeAssistance), SupportedExtensions);

        RegisterDebugStubMenuItem(serviceProvider, windowService, settingsService);

        projectExplorerService.RegisterConstructContextMenu((x, l) =>
        {
            if (x is [IProjectFile { Extension: ".asm" } file])
            {
                if (file.Root is not UniversalFpgaProjectRoot { Toolchain: "svnr" } universalFpgaProjectRoot)
                {
                    l.Add(new MenuItemModel("AsmConversion")
                    {
                        Header = "Convert .asm",
                        Command = new AsyncRelayCommand(() => asmConverterService.ConvertAsync(file)),
                    });
                }
                else
                {
                    if (SvnrSettingsHelper.GetAsmFile(universalFpgaProjectRoot) != file.RelativePath)
                    {
                        l.Add(new MenuItemModel("RegisterAsm")
                        {
                            Header = "Use this file to Compile",
                            Command = new AsyncRelayCommand(() => SvnrSettingsHelper.UpdateProjectAsmFile(file)),
                        });
                    }
                }
            }
        });


        windowService.RegisterUiExtension("UniversalFpgaToolBar_CompileMenuExtension",
            new OneWareUiExtension(x =>
            {
                if (x is not UniversalFpgaProjectRoot { Toolchain: "svnr" } root) return null;

                var name = root.Properties["Fpga"]?.ToString();
                var fpgaPackage = fpgaService.FpgaPackages.FirstOrDefault(obj => obj.Name == name);
                var fpga = fpgaPackage?.LoadFpga();
                var svnrToolchainService = serviceProvider.Resolve<SvnrToolchainService>();

                return new StackPanel()
                {
                    Orientation = Orientation.Vertical,
                    Children =
                    {
                        new MenuItem()
                        {
                            Header = "Run Synthesis",
                            Command = new AsyncRelayCommand(
                                async () => { await svnrToolchainService.SynthAsync(root, new FpgaModel(fpga!)); },
                                () => fpga != null)
                        },
                        new MenuItem()
                        {
                            Header = "Run Fit",
                            Command = new AsyncRelayCommand(
                                async () => { await svnrToolchainService.FitAsync(root, new FpgaModel(fpga!)); },
                                () => fpga != null)
                        },
                        new MenuItem()
                        {
                            Header = "Run Assemble",
                            Command = new AsyncRelayCommand(
                                async () => { await svnrToolchainService.AssembleAsync(root, new FpgaModel(fpga!)); },
                                () => fpga != null)
                        },
                        new Separator(),
                        new MenuItem()
                        {
                            Header = "Yosys Settings",
                            Icon = new Image()
                            {
                                Source = Application.Current!.FindResource(
                                    Application.Current!.RequestedThemeVariant,
                                    "Material.SettingsOutline") as IImage
                            },
                            Command = new AsyncRelayCommand(async () =>
                            {
                                if (projectExplorerService
                                        .ActiveProject is UniversalFpgaProjectRoot fpgaProjectRoot)
                                {
                                    var selectedFpga = root.Properties["Fpga"]?.ToString();
                                    var selectedFpgaPackage =
                                        fpgaService.FpgaPackages.FirstOrDefault(obj => obj.Name == selectedFpga);

                                    if (selectedFpgaPackage == null)
                                    {
                                        serviceProvider.Resolve<ILogger>()
                                            .Warning("No FPGA Selected. Open Pin Planner first!");
                                        return;
                                    }

                                    await windowService.ShowDialogAsync(
                                        new YosysCompileSettingsView
                                        {
                                            DataContext = new YosysCompileSettingsViewModel(fpgaProjectRoot,
                                                selectedFpgaPackage.LoadFpga())
                                        });
                                }
                            })
                        }
                    }
                };
            }));
        fpgaService.RegisterProjectEntryModification(x =>
        {
            if (x.Root is not UniversalFpgaProjectRoot universalFpgaProjectRoot) return;
            if (!(x is IProjectFile file && file.Extension == ".asm")) return;
            if (SvnrSettingsHelper.GetAsmFile(universalFpgaProjectRoot) == file.RelativePath)
            {
                x.Icon?.AddOverlay("ConstraintFile", "ForkAwesome.Check");
            }
            else
            {
                x.Icon?.RemoveOverlay("ConstraintFile");
            }
        });
    }
    
    public const string GdbPathSetting = "FEntwumS_Debugger_GdbPath";

    /// <summary>
    ///     Haengt den Umschalter fuer den <see cref="DebuggingStub" /> ins Menue.
    /// </summary>
    /// <remarks>
    ///     Bewusst ein Menuepunkt und kein automatischer Start: ein Stub, der immer lauscht, belegt
    ///     einen Port, ohne dass jemand debuggen will. Beim Start wird der vergebene Port gleich in
    ///     die Einstellung geschrieben - sonst muesste der Nutzer ihn aus dem Log abschreiben, und
    ///     er ist bei jedem Start ein anderer.
    /// </remarks>
    private static void RegisterDebugStubMenuItem(IServiceProvider serviceProvider, IWindowService windowService,
        ISettingsService settingsService)
    {
        var stub = serviceProvider.Resolve<DebuggingStub>();

        var item = new MenuItemModel("SvnrDebugStub")
        {
            Header = StartStubHeader,
            Icon = new IconModel("Material.BugReport")
        };

        item.Command = new AsyncRelayCommand(async () =>
        {
            if (stub.Endpoint != null)
            {
                await stub.StopAsync();
                settingsService.SetSettingValue(RemoteEndpointSetting, string.Empty);
                item.Header = StartStubHeader;
                return;
            }

            var endpoint = stub.Start();
            settingsService.SetSettingValue(RemoteEndpointSetting, endpoint);
            item.Header = $"Stop SVNR Debug Stub ({endpoint})";
        });

        windowService.RegisterMenuItem("MainWindow_MainMenu/Extras", item);
    }

    private const string StartStubHeader = "Start SVNR Debug Stub";

    private static void NotifyIfGdbMissing(IServiceProvider serviceProvider)
    {
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var logger = serviceProvider.Resolve<ILogger>();

        if (!settingsService.HasSetting(GdbPathSetting)) return;

        var configuredPath = settingsService.GetSettingValue<string>(GdbPathSetting);
        if (!string.IsNullOrWhiteSpace(configuredPath) && PlatformHelper.Exists(configuredPath)) return;

        logger.Log("[SVNR] Keine GDB-Binary eingerichtet, zeige Installationshinweis.");

        var packageWindowService = serviceProvider.Resolve<IPackageWindowService>();
        var windowService = serviceProvider.Resolve<IWindowService>();

        void ShowHint()
        {
            windowService.ShowNotificationWithButton(
                "GDB wird zum Debuggen benötigt",
                "Es ist keine GDB-Binary eingerichtet. Du kannst sie mit einem Klick im Extension Manager installieren.",
                "GDB installieren",
                () => _ = packageWindowService.ShowExtensionManagerAndTryInstallAsync(GdbPackage.Id!),
                type: NotificationType.Warning,
                expiration: TimeSpan.FromSeconds(20));
        }

        // Auto-Launch-Aktionen laufen noch in OnFrameworkInitializationCompleted, also bevor Avalonia
        // das Hauptfenster zeigt. Der WindowNotificationManager verwirft Notifications stillschweigend,
        // solange sein Template nicht angewandt ist - deshalb erst nach dem Oeffnen anzeigen.
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } mainWindow
            })
        {
            ShowHint();
            return;
        }

        if (mainWindow.IsLoaded)
            Dispatcher.UIThread.Post(ShowHint, DispatcherPriority.Background);
        else
            mainWindow.Opened += (_, _) => Dispatcher.UIThread.Post(ShowHint, DispatcherPriority.Background);
    }
    
    public static readonly Package GdbPackage = new()
    {
        Category = "Binaries",
        Id = "gdb",
        Type = "NativeTool",
        Name = "GNU Debugger",
        Description = "GNU Debugger for remote debugging via gdbserver.",
        License = "GPL 3.0",
        // Archer Fish, das GDB-Maskottchen. Liegt als Kopie im eigenen Repo, damit das Icon nicht
        // verschwindet, wenn sourceware.org die Datei verschiebt.
        // Steht unter CC BY-SA 3.0 US und damit unter einer anderen Lizenz als GDB selbst -
        // Namensnennung siehe Mascot-Link unten und Assets/archer.svg.license.txt.
        IconUrl =
            "https://raw.githubusercontent.com/FEntwumS/FEntwumS.SVNRExtension/main/src/FEntwumS.SVNRExtension/Assets/archer.svg",
        Links =
        [
            new PackageLink
            {
                Name = "GDB",
                Url = "https://www.sourceware.org/gdb/"
            },
            new PackageLink
            {
                Name = "Mascot (c) Jamie Guinan, Andreas Arnez - CC BY-SA 3.0 US",
                Url = "https://creativecommons.org/licenses/by-sa/3.0/us/"
            }
        ],
        Versions =
        [
            new PackageVersion
            {
                Version = "1.0.0",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        // GDB 11.2, statisch, --enable-targets=all, exe im Zip-Root (verifiziert)
                        Url =
                            "https://github.com/adamrehn/gdb-multiarch-windows/releases/download/gdb-11.2/gdb-11.2.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "gdb-multiarch.exe",
                                SettingKey = GdbPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        // GDB 17.1, musl-statisch, "full" = Python + Cross-Arch, flaches Archiv (verifiziert)
                        Url =
                            "https://github.com/guyush1/gdb-static/releases/download/v17.1-static/gdb-static-full-x86_64.tar.gz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "gdb",
                                SettingKey = GdbPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        // GDB 17.2, Homebrew-Ursprung, mit install_name_tool relokiert und
                        // ad-hoc signiert; --enable-targets=all, Python 3.14 im Bundle.
                        Url =
                            "https://github.com/FEntwumS/GDB/releases/download/v0.1.0/gdb-macos-arm64.tar.gz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                // Anders als die Archive fuer Windows und Linux entpackt dieses Tarball
                                // in einen eigenen Ordner, der im Pfad enthalten sein muss.
                                RelativePath = "gdb-macos-arm64/bin/gdb",
                                SettingKey = GdbPathSetting
                            }
                        ]
                    }
                    // Historie: Ursprünglich war für osx-arm64 kein standalone Prebuilt bekannt;
                    // Homebrew-gdb war der Vorschlag, benötigt aber lokale Homebrew-Installation.
                    // Ersetzt durch die FEntwumS-eigene Relozierung (build-gdb.sh im GDB-Repo).
                ]
            }
        ]
    };
}