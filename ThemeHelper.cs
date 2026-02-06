using System.Collections.Generic;
using Windows.Foundation;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;


namespace AppGroup {

    public static class ThemeHelper {
        // 윈도우별 핸들러 추적 (중복 등록 방지 및 해제용)
        private static readonly Dictionary<FrameworkElement, TypedEventHandler<FrameworkElement, object>> _registeredHandlers = new();

        public static void UpdateTitleBarColors(Window window) {
            if (window.Content is FrameworkElement root) {
                // 이미 등록된 핸들러가 있으면 중복 등록 방지
                if (_registeredHandlers.ContainsKey(root)) return;

                TypedEventHandler<FrameworkElement, object> handler = (sender, args) => {
                    var titleBar = window.AppWindow.TitleBar;
                    var isDarkMode = (window.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;
                    titleBar.ButtonForegroundColor = isDarkMode ? Colors.White : Colors.Black;
                };

                root.ActualThemeChanged += handler;
                _registeredHandlers[root] = handler;

                // 윈도우 Closed 이벤트에서 핸들러 해제
                window.Closed += (sender, args) => {
                    if (_registeredHandlers.TryGetValue(root, out var registeredHandler)) {
                        root.ActualThemeChanged -= registeredHandler;
                        _registeredHandlers.Remove(root);
                    }
                };

                var initialIsDarkMode = (window.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;
                var initialTitleBar = window.AppWindow.TitleBar;
                initialTitleBar.ButtonForegroundColor = initialIsDarkMode ? Colors.White : Colors.Black;
            }
        }
    }
}

