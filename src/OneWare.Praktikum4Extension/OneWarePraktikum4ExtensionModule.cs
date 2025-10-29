using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.OssCadSuiteIntegration.ViewModels;
using OneWare.OssCadSuiteIntegration.Views;
using OneWare.Praktikum4Extension.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Praktikum4Extension;

public class OneWarePraktikum4ExtensionModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<AsmConverterService>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var asmConverterService = containerProvider.Resolve<AsmConverterService>();
        var projectExplorerService = containerProvider.Resolve<IProjectExplorerService>();
        var fpgaService = containerProvider.Resolve<FpgaService>();

        containerProvider.Resolve<IProjectExplorerService>().RegisterConstructContextMenu((x,l) =>
        {
            if (x is [IProjectFile { Extension: ".asm"} file])
            {
                l.Add(new MenuItemViewModel("GHDL")
                {
                    Header = "Convert .asm",
                    Command = new AsyncRelayCommand(()=>asmConverterService.ConvertAsync(file)),
                });
            }
        });
        
        containerProvider.Resolve<FpgaService>().RegisterPreCompileStep<AsmToVhdlPreCompileStep>();
        containerProvider.Resolve<FpgaService>().RegisterToolchain<Praktikum4Toolchain>();
        
        /*
        var ghdlPreCompiler = containerProvider.Resolve<AsmToVhdlPreCompileStep>();
        var asmPreCompiler = containerProvider.Resolve<AsmToVhdlPreCompileStep>();
        containerProvider.Resolve<IWindowService>().RegisterUiExtension("UniversalFpgaToolBar_CompileMenuExtension",
            new UiExtension(
                x =>
                {
                    if (x is not UniversalFpgaProjectRoot { Toolchain: Praktikum4Toolchain } root) return null;

                    var name = root.Properties["Fpga"]?.ToString();
                    var fpgaPackage = fpgaService.FpgaPackages.FirstOrDefault(obj => obj.Name == name);
                    var fpga = fpgaPackage?.LoadFpga();
                    
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
                                    await projectExplorerService.SaveOpenFilesForProjectAsync(root);
                                    var fpgaModel = new FpgaModel(fpga!); 
                                    await asmPreCompiler.PerformPreCompileStepAsync(root, fpgaModel);
                                    await ghdlPreCompiler.PerformPreCompileStepAsync(root, fpgaModel);
                                    
                                    try{
                                        var verilogFileName = ghdlPreCompiler.VerilogFileName ?? throw new Exception("Invalid verilog file name!");
                                        var ghdlOutputPath = Path.Combine(root.FullPath, ghdlPreCompiler.BuildDir,
                                            ghdlPreCompiler.GhdlOutputDir, verilogFileName);
                                        var mandatoryFileList = new List<string>(1) {ghdlOutputPath};
                                        await yosysService.SynthAsync(root, new FpgaModel(fpga!), mandatoryFileList);
                                    }
                                    catch (Exception e)
                                    { 
                                        ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
                                    }
                                    
                                }, () => fpga != null)
                            },
                            new MenuItem()
                            {
                                Header = "Run Fit",
                                Command = new AsyncRelayCommand(async () =>
                                {
                                    await projectExplorerService.SaveOpenFilesForProjectAsync(root);
                                    await yosysService.FitAsync(root, new FpgaModel(fpga!));
                                }, () => fpga != null)
                            },
                            new MenuItem()
                            {
                                Header = "Run Assemble",
                                Command = new AsyncRelayCommand(async () =>
                                {
                                    await projectExplorerService.SaveOpenFilesForProjectAsync(root);
                                    await yosysService.AssembleAsync(root, new FpgaModel(fpga!));
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
*/
    }
}