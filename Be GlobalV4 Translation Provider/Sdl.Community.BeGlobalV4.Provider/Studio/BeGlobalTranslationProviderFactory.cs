﻿using System;
using System.Collections.ObjectModel;
using Sdl.Community.BeGlobalV4.Provider.Helpers;
using Sdl.Community.BeGlobalV4.Provider.Model;
using Sdl.Community.BeGlobalV4.Provider.Service;
using Sdl.LanguagePlatform.TranslationMemoryApi;

namespace Sdl.Community.BeGlobalV4.Provider.Studio
{
	[TranslationProviderFactory(Id = "SDLMachineTranslationCloudProviderFactory",
		Name = "SDLMachineTranslationCloudProviderFactory",
		Description = "SDL Machine Translation Cloud Provider")]
	public class BeGlobalTranslationProviderFactory : ITranslationProviderFactory
	{
		private string _url = "https://translate-api.sdlbeglobal.com";
		public static readonly Log Log = Log.Instance;

		[STAThread]
		public ITranslationProvider CreateTranslationProvider(Uri translationProviderUri, string translationProviderState,
			ITranslationProviderCredentialStore credentialStore)
		{
			var originalUri = new Uri("sdlmachinetranslationcloudprovider:///");
			var options = new BeGlobalTranslationOptions(translationProviderUri);
			if (credentialStore.GetCredential(originalUri) != null)
			{
				var credentials = credentialStore.GetCredential(originalUri);
				var splitedCredentials = credentials.Credential.Split('#');				
				options.ClientId = splitedCredentials.Length > 2 ? StringExtensions.Decrypt(splitedCredentials[0]) : string.Empty;
				options.ClientSecret = splitedCredentials.Length > 2 ? StringExtensions.Decrypt(splitedCredentials[1]) : string.Empty;
				options.AuthenticationMethod = splitedCredentials.Length == 3 ? splitedCredentials[2] : string.Empty;
				if (options.BeGlobalService == null)
				{
					options.BeGlobalService = new BeGlobalV4Translator(_url, options);
				}
			}
			else
			{
				credentialStore.AddCredential(originalUri, new TranslationProviderCredential(originalUri.ToString(), true));
			}

			int accountId;
			if (options.UseClientAuthentication)
			{
				accountId = options.BeGlobalService== null ? 0 : options.BeGlobalService.GetClientInformation();
			}
			else
			{
				accountId = options.BeGlobalService == null ? 0: options.BeGlobalService.GetUserInformation();
			}

			var subscriptionInfo = options.BeGlobalService.GetLanguagePairs(accountId.ToString());
			options.SubscriptionInfo = subscriptionInfo;

			return new BeGlobalTranslationProvider(options);
		}

		public bool SupportsTranslationProviderUri(Uri translationProviderUri)
		{
			if (translationProviderUri == null)
			{
				throw new ArgumentNullException(nameof(translationProviderUri));
			}

			var supportsProvider = string.Equals(translationProviderUri.Scheme, BeGlobalTranslationProvider.ListTranslationProviderScheme,
				StringComparison.OrdinalIgnoreCase);
			return supportsProvider;
		}

		public TranslationProviderInfo GetTranslationProviderInfo(Uri translationProviderUri, string translationProviderState)
		{
			var info = new TranslationProviderInfo
			{
				TranslationMethod = TranslationMethod.MachineTranslation,
				Name = PluginResources.Plugin_NiceName
			};
			return info;
		}
	}
}