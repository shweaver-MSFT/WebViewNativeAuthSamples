using System;
using CommunityToolkit.Authentication;
using Windows.Foundation;
using Windows.Foundation.Metadata;

namespace AuthProviderComponent
{
    [AllowForWeb]
    public sealed class AuthProviderShim
    {
        public IAsyncOperation<string> GetTokenAsync()
        {
            return ProviderManager.Instance.GlobalProvider.GetTokenAsync().AsAsyncOperation();
        }

        public IAsyncAction SignInAsync()
        {
            return ProviderManager.Instance.GlobalProvider.SignInAsync().AsAsyncAction();
        }

        public IAsyncAction SignOutAsync()
        {
            return ProviderManager.Instance.GlobalProvider.SignOutAsync().AsAsyncAction();
        }

        public IAsyncAction TrySilentSignInAsync()
        {
            return ProviderManager.Instance.GlobalProvider.TrySilentSignInAsync().AsAsyncAction();
        }
    }
}
