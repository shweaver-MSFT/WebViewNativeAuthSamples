# WebViewNativeAuthSamples

This repo consists of two samples that demonstrate how to interact with natively configured user accounts on Windows from a web based application hosted inside a WebView/WebView2 control.

Both samples render the same web page and require the same setup steps. On that page there is a button and when clicked it will authenticate the user as needed, obtain an authorization token, and use that token to make a request to Microsoft Graph and display the active user's `UserPrincipalName`.

Each sample has it's own issues supporting the scenario, and only the first sample is functional at all.

## Strategy

To be able to authenticate the user and make authorized requests with MSA/AAD accounts, the strategy is to host web content in a WebView control, and use the `WebResourceRequested` event to intercept requests to the Graph endpoint. When a Graph request is intercepted, we invoke the native account management APIs in Windows to show the local account picker and retrieve an authorization token. The token is appended to the request header, the request is sent, and the response is sent back to the web page in the WebView.

Form the web application's point of view, there is no additional work required to integrate with Windows. Just start making web requests and the WebView will handle authentication flow and authorization headers for outbound requests.

## Sample 1: WebViewNativeAuthUWP

The first sample is a UWP project that leverages the `Windows.UI.XAML.Controls.WebView` control built into the platform to render a simple web page. 

This sample demonstrates a **WORKING** scenario that successfully calls the native account APIs. However, the solution requires synchronous workarounds:

```
// Runs when the hosted web page makes a request. 
private void OnWebViewWebResourceRequested(WebView sender, WebViewWebResourceRequestedEventArgs args)
{
    // Detect requests to Microsoft Graph
    if (args.Request.RequestUri.Host == "graph.microsoft.com")
    {
        // Get the token and append it to the request.
        var token = ProviderManager.Instance.GlobalProvider.GetTokenAsync().Result; // <- Using .Result here
        args.Request.Headers.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("Bearer", token);

        // Send the request and set the response.
        var client = new HttpClient();
        args.Response = client.SendRequestAsync(args.Request).AsTask().Result;  // <- Using .Result here too
    }
}
```

## Sample 2: WebView2NativeAuthWinUI2

The second sample is another UWP project that leverages the `Microsoft.UI.Xaml.Controls.WebView2` control from WinUI 2 version 2.7.0-prerelease.210827001 (More info [here](https://docs.microsoft.com/en-us/microsoft-edge/webview2/get-started/winui2)).

This sample demonstrates a broken scenario, where it is not possible to support the async flow of UI interaction. Using the deferral with async methods causes the app to freeze, and attempting to force the code to run synchornously causes the WebView to ignore the response and execute the request on its own (without the auth header).

```
// Runs when the hosted web page makes a request. 
private async void OnWebView2WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
{
    // Without the deferral, the request is sent properly but our response is ignored.
    // - The WebView won't wait for our async calls and sends the request on it's own, without the special auth header.
    // With the deferral, the request is sent properly but the app freezes because we deviate from the original calling thread.
    const bool useDeferral = false;

    // Detect requests to Microsoft Graph
    if (args.Request.Uri.StartsWith(GRAPH_ENDPOINT))
    {
        var deferral = useDeferral ? args.GetDeferral() : null;
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
                System.Diagnostics.Debug.WriteLine(e.Message);
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

        deferral?.Complete();
    }
}
```

### Sample setup

> Remember: Setup must be done for both sample projects.

1. Open the sample in Visual Studio.
2. Use the menu **Project** -> **Publish** -> **Associate App with the Store...** and follow the menu to create a Store app association. *This step is required for the authentication provider to work.*
3. F5 to run the application from Visual Studio.
4. Once the application is running, click the button and observe the Windows native account manager display.
5. Select an account to login with and observe the chosen user's `UserPrincipalName` is presented next to the button.
