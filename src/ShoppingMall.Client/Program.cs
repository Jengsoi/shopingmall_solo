namespace ShoppingMall.Client;

/// <summary>클라이언트 프로그램 진입점. Avalonia 앱을 만들고 데스크톱 창 모드로 실행한다.</summary>
internal static class Program
{
    // [STAThread]: Windows 에서 창·클립보드 같은 UI 기능이 요구하는 스레드 방식
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>App 클래스로 앱을 구성한다. UsePlatformDetect() 가 Windows/Linux/macOS 에 맞는 화면 출력 방식을 고른다.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect();
}
