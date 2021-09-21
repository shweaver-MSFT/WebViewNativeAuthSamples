using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using CommunityToolkit.Authentication;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

#pragma warning disable CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.

namespace WebView2NativeAuthWinUI2
{
    public sealed partial class MainPage : Page
    {
        // The Microsoft Graph service.
        const string GRAPH_ENDPOINT = "https://graph.microsoft.com";

        public MainPage()
        {
            InitializeComponent();

            // Set the global auth provider
            var scopes = new string[] { "User.Read" };
            ProviderManager.Instance.GlobalProvider = new WindowsProvider(scopes);

            // Initialize the inner CoreWebView2
            MyWebView2.CoreWebView2Initialized += OnCoreWebView2Initialized;
            _ = MyWebView2.EnsureCoreWebView2Async();
        }

        /// <summary>
        /// Prepare the <see cref="CoreWebView2"/> and navigate to the web index page.
        /// </summary>
        private void OnCoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            // Enable the webview to load web content from the web/ folder in the app package.
            MyWebView2.CoreWebView2.SetVirtualHostNameToFolderMapping("web", "web", CoreWebView2HostResourceAccessKind.Allow);

            // Configure interception for web requests going to the Microsoft Graph.
            MyWebView2.CoreWebView2.AddWebResourceRequestedFilter($"{GRAPH_ENDPOINT}/*", CoreWebView2WebResourceContext.XmlHttpRequest);
            MyWebView2.CoreWebView2.WebResourceRequested += OnWebView2WebResourceRequested;

            // Navigate to the index page.
            MyWebView2.Source = new Uri("https://web/index.html");
        }

        /// <summary>
        /// Detect when the webview makes outbound requests that match any configured request filters.
        /// </summary>
        private async void OnWebView2WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            // Detect requests to Microsoft Graph
            if (args.Request.Uri.StartsWith(GRAPH_ENDPOINT))
            {
                //var deferral = useDeferral ? args.GetDeferral() : null;
                using (args.GetDeferral())
                {
                    var tcs = new TaskCompletionSource<CoreWebView2WebResourceResponse>();

                    // Do this on this work on the UI thread to ensure any UI prompts from the auth provider are able to display.
                    var taskQueued = DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
                    {
                        try
                        {
                            // There is no provided client for sending the specific CoreWebView2 request type.
                            // Convert the request from CoreWebView2 into something we can work with.
                            var request = GetHttpRequestMessage(args.Request);

                            // Get the auth token and append it to the request.
                            var token = await ProviderManager.Instance.GlobalProvider.GetTokenAsync();
                            request.Headers.Authorization = new HttpCredentialsHeaderValue("Bearer", token);

                            // Send the request and get the response.
                            var client = new HttpClient();
                            var response = await client.SendRequestAsync(request);

                            // Convert the response to the appropriate type expected by CoreWebView2 and return.
                            tcs.SetResult(await GetWebResourceResponseAsync(sender, response));
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.Message);
                            Debugger.Break();
                            tcs.SetException(e);
                        }
                    });

                    if (taskQueued)
                    {
                        args.Response = await tcs.Task;
                    }
                    else
                    {
                        tcs.SetCanceled();
                    }
                }
            }
        }

        /// <summary>
        /// Converts a <see cref="CoreWebView2WebResourceRequest"/> to a <see cref="HttpRequestMessage"/> for sending.
        /// </summary>
        private static HttpRequestMessage GetHttpRequestMessage(CoreWebView2WebResourceRequest webResourceRequest)
        {
            try
            {
                // Construct the request message to send.
                var request = new HttpRequestMessage()
                {
                    Method = new HttpMethod(webResourceRequest.Method),
                    RequestUri = new Uri(webResourceRequest.Uri)
                };

                // Apply any headers to the request message.
                foreach (var header in webResourceRequest.Headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }

                // Apply any content to the request message.
                if (webResourceRequest.Content != null)
                {
                    request.Content = new HttpStreamContent(webResourceRequest.Content.AsStreamForRead().AsInputStream());
                }

                return request;
            }
            catch (Exception e)
            {
                Debugger.Break();
                throw e;
            }
        }

        /// <summary>
        /// Converts a <see cref="HttpResponseMessage"/> to the response type expected by the <see cref="CoreWebView2.WebResourceRequested"/> event. 
        /// </summary>
        private static async Task<CoreWebView2WebResourceResponse> GetWebResourceResponseAsync(CoreWebView2 coreWebView2, HttpResponseMessage response)
        {
            try
            {
                // Get the response content buffer
                var responseContentBuffer = await response.Content.ReadAsBufferAsync();

                // Convert the content buffer to a stream.
                var contentStream = responseContentBuffer.AsStream();

                // Sanity check, inspect the response content.
                if (Debugger.IsAttached)
                {
                    var reader = new StreamReader(contentStream);
                    var contentString = await reader.ReadToEndAsync();
                    Debug.WriteLine(contentString);
                }

                // Convert the System.Stream to a Windows.Storage.Streams.IRandomAccessStream
                var randomAccessStream = contentStream.AsRandomAccessStream();
                
                // Seek to 0, as recommended here: https://github.com/MicrosoftEdge/WebView2Feedback/issues/1627
                randomAccessStream.Seek(0);

                // Return the appropriate CoreWebView2WebResourceResponse response type.
                return coreWebView2.Environment.CreateWebResourceResponse(randomAccessStream, (int)response.StatusCode, response.ReasonPhrase, response.Headers.ToString());
            }
            catch (Exception e)
            {
                Debugger.Break();
                throw e;
            }
        }
    }
}
