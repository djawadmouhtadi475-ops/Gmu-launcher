using System.Diagnostics;
using System.Windows;

namespace GmuLauncher.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    public AppState State => AppState.I;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(PlayCommand)), NotifyCanExecuteChangedFor(nameof(KillCommand))]
    private bool _isBusy;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(PlayCommand)), NotifyCanExecuteChangedFor(nameof(KillCommand))]
    private bool _isRunning;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _status = Loc.T("Home_Ready");

    public ObservableCollection<string> Logs { get; } = new();
    CancellationTokenSource? _cts;
    Process? _proc;

    public HomeViewModel()
    {
        State.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.SelectedInstance)) PlayCommand.NotifyCanExecuteChanged();
        };
    }

    bool CanPlay() => !IsBusy && !IsRunning && State.SelectedInstance != null && State.Session != null;
    bool CanKill() => IsBusy || IsRunning;

    void Log(string line) => Application.Current.Dispatcher.BeginInvoke(() =>
    {
        Logs.Add(line);
        if (Logs.Count > 3000) Logs.RemoveAt(0);
    });

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync()
    {
        var inst = State.SelectedInstance!;
        _cts = new CancellationTokenSource();
        IsBusy = true; Progress = 0; Status = Loc.T("Status_Preparing");
        try
        {
            // تجديد توكن Microsoft قبل التشغيل
            if (State.Account?.Type == "Microsoft")
            {
                try { State.Session = await State.Auth.MicrosoftSilentAsync(); } catch { }
            }

            inst.LaunchVersion = await State.Launcher.EnsureLoaderAsync(inst, _cts.Token);
            State.Db.SaveInstance(inst);

            var prog = new Progress<(string text, double pct)>(p => { Status = p.text; Progress = p.pct; });
            var proc = await State.Launcher.PrepareAsync(inst, State.Session!, prog, _cts.Token);

            proc.EnableRaisingEvents = true;
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) Log(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) Log("[ERR] " + e.Data); };
            proc.Exited += (_, _) => Application.Current.Dispatcher.BeginInvoke(() =>
            {
                Log($"--- exit code {SafeExit(proc)} ---");
                IsRunning = false; Status = Loc.T("Status_Stopped"); Progress = 0;
            });

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            _proc = proc;

            inst.LastPlayed = DateTime.Now;
            State.Db.SaveInstance(inst);
            State.Settings.LastInstanceId = inst.Id;

            IsRunning = true; Status = Loc.T("Status_Running"); Progress = 100;
        }
        catch (OperationCanceledException) { Status = Loc.T("Status_Cancelled"); Progress = 0; }
        catch (Exception ex) { Log(ex.ToString()); Status = Loc.T("Status_Error"); Progress = 0; }
        finally { IsBusy = false; }
    }

    static int SafeExit(Process p) { try { return p.ExitCode; } catch { return -1; } }

    /// <summary>إيقاف قسري: إلغاء التنزيل إن كان جارياً أو قتل عملية اللعبة.</summary>
    [RelayCommand(CanExecute = nameof(CanKill))]
    private void Kill()
    {
        _cts?.Cancel();
        try { if (_proc is { HasExited: false }) _proc.Kill(true); } catch { }
    }

    [RelayCommand] private void ClearLogs() => Logs.Clear();
}
