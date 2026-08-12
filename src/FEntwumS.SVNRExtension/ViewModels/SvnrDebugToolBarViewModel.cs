using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FEntwumS.SVNRExtension.Services;
using FEntwumS.SVNRExtension.Tools;
using OneWare.Essentials.Debugger;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace FEntwumS.SVNRExtension.ViewModels;

/// <summary>
/// Zustand des Debug-Knopfes in der Werkzeugleiste.
/// </summary>
/// <remarks>
/// Sichtbar ist der Knopf, sobald das Projekt eine <c>.asm</c> fuehrt - bewusst nicht an der
/// aktiven Toolchain festgemacht: Debuggen soll auch dann gehen, wenn synthetisiert gerade
/// niemand.
/// </remarks>
public sealed partial class SvnrDebugToolBarViewModel : ObservableObject
{
    private readonly SvnrDebugSessionService _sessionService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IDebuggerService _debuggerService;

    [ObservableProperty] private bool _isAvailable;

    public SvnrDebugToolBarViewModel(
        SvnrDebugSessionService sessionService,
        IProjectExplorerService projectExplorerService,
        IDebuggerService debuggerService)
    {
        _sessionService = sessionService;
        _projectExplorerService = projectExplorerService;
        _debuggerService = debuggerService;

        _projectExplorerService.PropertyChanged += OnProjectExplorerChanged;
        _debuggerService.StateChanged += (_, _) => Refresh();

        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanStartDebug))]
    private Task StartDebugAsync()
    {
        return _sessionService.StartAsync();
    }

    private bool CanStartDebug()
    {
        return IsAvailable && !_debuggerService.IsActive;
    }

    private void OnProjectExplorerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IProjectExplorerService.ActiveProject)) Refresh();
    }

    private void Refresh()
    {
        IsAvailable = _projectExplorerService.ActiveProject is UniversalFpgaProjectRoot project
                      && SvnrSettingsHelper.GetAsmFile(project) != "none";

        StartDebugCommand.NotifyCanExecuteChanged();
    }
}
