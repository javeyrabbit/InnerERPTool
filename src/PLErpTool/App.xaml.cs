using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using PLErpTool.Infrastructure;
using PLErpTool.ViewModels;
using PLErpTool.Application.Receivable;
using PLErpTool.Application.Account;
using PLErpTool.Services;
using Wpf.Ui.Appearance;

namespace PLErpTool;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private ServiceProvider? _serviceProvider;
    private IServiceScope? _applicationScope;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // Keep the original brand color instead of inheriting the Windows accent color.
        ApplicationAccentColorManager.Apply(Color.FromRgb(0x54, 0x48, 0xC8), ApplicationTheme.Light);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        string dbPath = Path.Combine(AppContext.BaseDirectory, "plerp.db");
        var services = new ServiceCollection();
        services.AddInfrastructure(dbPath);
        services.AddApplication();
        services.AddSingleton<SearchHistoryStore>();

        // View models and application services share one desktop-application scope.
        // This prevents scoped EF Core services from being resolved from the root provider.
        services.AddScoped<MainViewModel>();
        services.AddScoped<ReceivableViewModel>();
        services.AddScoped<AccountViewModel>();
        services.AddScoped<PersonnelViewModel>();

        _serviceProvider = services.BuildServiceProvider();
        _applicationScope = _serviceProvider.CreateScope();
        Services = _applicationScope.ServiceProvider;
        _serviceProvider.EnsureDatabaseCreated(dbPath);

        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _applicationScope?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogFatal(e.Exception);
        MessageBox.Show($"程序发生未处理的异常：\n{e.Exception.Message}\n\n详细信息已写入日志文件。",
            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogFatal(ex);
    }

    private static void LogFatal(Exception ex)
    {
        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"error_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            File.WriteAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n");
        }
        catch
        {
            // 日志写入失败时不再抛出，避免二次崩溃
        }
    }
}
