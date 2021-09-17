using System;
using AuthProviderComponent;
using CommunityToolkit.Authentication;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;

namespace WebViewNativeAuthUwp
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();

            // Set the global provider
            var scopes = new string[] { "User.Read" };
            ProviderManager.Instance.GlobalProvider = new WindowsProvider(scopes);

            MyWebView.WebResourceRequested += OnWebViewWebResourceRequested;
            MyWebView.NavigationStarting += OnWebViewNavigationStarting;
        }

        private void OnWebViewNavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            // Inject the auth provider to enable direct manipulation.
            sender.AddWebAllowedObject("authProvider", new AuthProviderShim());
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Navigate to the index page.
            MyWebView.Navigate(new Uri("ms-appx-web:///web/index.html"));
        }

        private void OnWebViewWebResourceRequested(WebView sender, WebViewWebResourceRequestedEventArgs args)
        {
            // Detect requests to Microsoft Graph
            if (args.Request.RequestUri.Host == "graph.microsoft.com")
            {
                // Get the token and append it to the request.
                var token = ProviderManager.Instance.GlobalProvider.GetTokenAsync().Result;
                args.Request.Headers.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("Bearer", token);

                // Send the request and set the response.
                var client = new HttpClient();
                args.Response = client.SendRequestAsync(args.Request).AsTask().Result;
            }
        }
    }
}
