using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ToolVPS.Services;
using ToolVPS.ViewModels;

namespace ToolVPS;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        services.AddSingleton<SettingsService>();
        services.AddSingleton<SshService>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<VultrService>();

        services.AddSingleton<DockerService>();
        services.AddSingleton<InstanceListViewModel>();
        services.AddSingleton<SshKeyViewModel>();
        services.AddSingleton<TerminalViewModel>();
        services.AddSingleton<DockerViewModel>();
        services.AddSingleton<MainViewModel>();

        Services = services.BuildServiceProvider();

        Services.GetRequiredService<SettingsService>().Load();

        var main = Services.GetRequiredService<MainViewModel>();

        // Wire instance connect → open terminal
        var instanceVm = Services.GetRequiredService<InstanceListViewModel>();
        var terminalVm = Services.GetRequiredService<TerminalViewModel>();
        instanceVm.ConnectRequested += inst =>
        {
            terminalVm.PreFill(inst);
            main.CurrentView = terminalVm;
        };

        instanceVm.QuickConnectRequested += (host, port, user, pass, key) =>
        {
            main.CurrentView = terminalVm;
            terminalVm.AutoConnect(host, port, user, pass, key);
        };

        var window = new MainWindow { DataContext = main };
        window.Show();
    }
}
