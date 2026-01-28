using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using FEntwumS.SVNRExtension.Services;
using FEntwumS.SVNRExtension.Tools;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.OssCadSuiteIntegration.ViewModels;
using OneWare.OssCadSuiteIntegration.Views;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace FEntwumS.SVNRExtension;

/*TODO:
 * Idee zu Rechtsklickmenu:
 * Projekt-optionsfenster mit auto/manuell, manuell eingestellte Datei kann über Rechtsklickmenu geändert werden
 */

public class FEntwumsSvnrExtensionModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<AsmConverterService>();
        containerRegistry.RegisterSingleton<SvnrToolchainService>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var asmConverterService = containerProvider.Resolve<AsmConverterService>();
        var projectExplorerService = containerProvider.Resolve<IProjectExplorerService>();
        var windowService = containerProvider.Resolve<IWindowService>();
        var fpgaService = containerProvider.Resolve<FpgaService>();

        containerProvider.Resolve<FpgaService>().RegisterPreCompileStep<AsmToVhdlPreCompileStep>();
        containerProvider.Resolve<FpgaService>().RegisterToolchain<SvnrToolchain>();

        var resourceInclude = new ResourceInclude(new Uri("avares://FEntwumS.SVNRExtension/Styles/Icons.axaml")) 
            {Source = new Uri("avares://FEntwumS.SVNRExtension/Styles/Icons.axaml")};
        Application.Current?.Resources.MergedDictionaries.Add(resourceInclude);
        
        
        projectExplorerService.Projects.CollectionChanged += (sender, e) =>
        {
            if (sender is ObservableCollection<IProjectRoot> collection)
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (var project in collection)
                    {
                        SvnrSettingsHelper.SetAsmOverlay(project);
                    }
                }
            }
        };
        
        
        containerProvider.Resolve<IProjectExplorerService>().RegisterConstructContextMenu((x,l) =>
        {
            if (x is [IProjectFile { Extension: ".asm" } file])
            {

                if (file.Root is not UniversalFpgaProjectRoot { Toolchain: SvnrToolchain } universalFpgaProjectRoot)
                {
                    l.Add(new MenuItemViewModel("AsmConversion")
                    {
                        Header = "Convert .asm",
                        Command = new AsyncRelayCommand(() => asmConverterService.ConvertAsync(file)),
                    });
                }
                else
                {
                    if (SvnrSettingsHelper.GetAsmFile(universalFpgaProjectRoot) != file.RelativePath)
                    {
                        l.Add(new MenuItemViewModel("RegisterAsm")
                        {
                            Header = "Use this file to Compile",
                            Command = new AsyncRelayCommand(() => SvnrSettingsHelper.UpdateProjectAsmFile(file)),
                        });
                    }
                }
            }
        });
        

        
        containerProvider.Resolve<IWindowService>().RegisterUiExtension("UniversalFpgaToolBar_CompileMenuExtension",
            new UiExtension(
                x =>
                {
                    if (x is not UniversalFpgaProjectRoot { Toolchain: SvnrToolchain } root) return null;

                    var name = root.Properties["Fpga"]?.ToString();
                    var fpgaPackage = fpgaService.FpgaPackages.FirstOrDefault(obj => obj.Name == name);
                    var fpga = fpgaPackage?.LoadFpga();
                    var svnrToolchainService = containerProvider.Resolve<SvnrToolchainService>();
                    
                    return new StackPanel()
                    {
                        Orientation = Orientation.Vertical,
                        Children =
                        {
                            new MenuItem()
                            {
                                Header = "Run Synthesis",
                                Command = new AsyncRelayCommand(async () =>
                                {
                                    await svnrToolchainService.SynthAsync(root, new FpgaModel(fpga!));
                                }, () => fpga != null)
                            },
                            new MenuItem()
                            {
                                Header = "Run Fit",
                                Command = new AsyncRelayCommand(async () =>
                                {
                                    await svnrToolchainService.FitAsync(root, new FpgaModel(fpga!));
                                }, () => fpga != null)
                            },
                            new MenuItem()
                            {
                                Header = "Run Assemble",
                                Command = new AsyncRelayCommand(async () =>
                                {
                                    await svnrToolchainService.AssembleAsync(root, new FpgaModel(fpga!));
                                }, () => fpga != null)
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
                                            containerProvider.Resolve<ILogger>()
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

    }
}