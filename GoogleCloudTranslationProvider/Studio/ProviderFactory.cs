﻿using System;
using GoogleCloudTranslationProvider.Models;
using Sdl.LanguagePlatform.TranslationMemoryApi;

namespace GoogleCloudTranslationProvider.Studio
{
	[TranslationProviderFactory(Id = Constants.Provider_TranslationProviderFactory,
								Name = Constants.Provider_TranslationProviderFactory,
								Description = Constants.Provider_TranslationProviderFactory)]
	public class ProviderFactory : ITranslationProviderFactory
	{
		public ITranslationProvider CreateTranslationProvider(Uri translationProviderUri, string translationProviderState, ITranslationProviderCredentialStore credentialStore)
		{
			if (!SupportsTranslationProviderUri(translationProviderUri))
			{
				throw new Exception(PluginResources.UriNotSupportedMessage);
			}

			var translationOptions = new GCTPTranslationOptions(translationProviderUri);
			if (translationOptions.SelectedGoogleVersion is not ApiVersion.V2)
			{
				return new Provider(translationOptions);
			}

			if ((credentialStore.GetCredential(translationProviderUri) ??
				 credentialStore.GetCredential(new Uri(Constants.GoogleTranslationFullScheme)))
				is not TranslationProviderCredential credentials)
			{
				throw new TranslationProviderAuthenticationException();
			}

			credentials = new TranslationProviderCredential(credentials.Credential, credentials.Persist);
			translationOptions.ApiKey = credentials.Credential;
			translationOptions.PersistGoogleKey = credentials.Persist;
			return new Provider(translationOptions);
		}

		public bool SupportsTranslationProviderUri(Uri translationProviderUri)
		{
			return translationProviderUri switch
			{
				null => throw new ArgumentNullException(PluginResources.UriNotSupportedMessage),
				_ => string.Equals(translationProviderUri.Scheme, Constants.GoogleTranslationScheme, StringComparison.OrdinalIgnoreCase)
			};
		}

		public TranslationProviderInfo GetTranslationProviderInfo(Uri translationProviderUri, string translationProviderState)
		{
			return new TranslationProviderInfo
			{
				TranslationMethod = TranslationMethod.MachineTranslation,
				Name = Constants.GoogleNaming_FullName
			};
		}
	}
}