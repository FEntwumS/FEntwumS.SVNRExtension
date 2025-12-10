using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SVNRExtension.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.SVNRExtension;

public class OneWareSVNRExtensionModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<AsmConverterService>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var asmConverterService = containerProvider.Resolve<AsmConverterService>();

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
        
    }
}