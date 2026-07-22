using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
    public const string GdbPathSetting = "GdbPath";
    
    public static readonly Package GdbPackage = new()
    {
        Category = "Binaries",
        Id = "gdb",
        Type = "NativeTool",
        Name = "GDB Multiarch",
        Description = "GNU Debugger with multi-architecture support for remote debugging via gdbserver.",
        License = "GPL 3.0",
        Links =
        [
            new PackageLink
            {
                Name = "GDB",
                Url = "https://www.sourceware.org/gdb/"
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
                                RelativePath = "bin/gdb",
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

    public static readonly string[] SupportedExtensions = [".asm"];

    public static string SuggestGdbPath(IPaths paths)
    {
        var target = GdbPackage.Versions?
            .SelectMany(v => v.Targets ?? [])
            .FirstOrDefault(t => t.Target == PlatformHelper.PlatformIdentifier);

        var relativePath = target?.AutoSetting?.FirstOrDefault()?.RelativePath;
        if (relativePath is null) return string.Empty;

        return Path.Combine(paths.NativeToolsDirectory, GdbPackage.Id!, relativePath);
    }

    public override IReadOnlyCollection<string> Dependencies
        => ["OssCadSuiteIntegrationModule"];

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<AsmConverterService>();
        services.AddSingleton<SvnrToolchainService>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var asmConverterService = serviceProvider.Resolve<AsmConverterService>();
        var projectExplorerService = serviceProvider.Resolve<IProjectExplorerService>();
        var windowService = serviceProvider.Resolve<IWindowService>();
        var fpgaService = serviceProvider.Resolve<FpgaService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var paths = serviceProvider.Resolve<IPaths>();

        settingsService.RegisterSetting("Tools", "Debugger", GdbPathSetting,
            new FilePathSetting(
                "GDB Binary Path",
                SuggestGdbPath(paths),
                "Auto-detected",
                paths.NativeToolsDirectory,
                PlatformHelper.ExistsOnPath,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Pfad zur GDB-Executable für Remote-Debugging über gdbserver."
            });

        serviceProvider.Resolve<FpgaService>().RegisterToolchain<SvnrToolchain>();

        fpgaService.RegisterLanguage("ASM", SupportedExtensions);
        var languageManager = serviceProvider.Resolve<ILanguageManager>();

        languageManager.RegisterTextMateLanguage("asm", "avares:// FEntwumS.SVNRExtension/Assets/asm.tmLanguage.json",
            SupportedExtensions);

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
}