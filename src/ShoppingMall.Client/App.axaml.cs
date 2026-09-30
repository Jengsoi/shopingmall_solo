using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ShoppingMall.Client;

/// <summary>앱 객체. App.axaml 의 테마를 불러오고, 시작할 때 MainWindow 를 띄운다.</summary>
public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // 데스크톱 앱으로 실행된 경우에만 창을 띄운다. (MainWindow 가 닫히면 앱도 종료)
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
