﻿using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Sdl.Community.GSVersionFetch.Commands;
using Sdl.Community.GSVersionFetch.Model;
using Sdl.Community.GSVersionFetch.Service;
using Sdl.Community.GSVersionFetch.View;
using UserControl = System.Windows.Controls.UserControl;

namespace Sdl.Community.GSVersionFetch.ViewModel
{
	public class LoginViewModel: ProjectWizardViewModelBase
	{
		private bool _isValid;
		//private string _url;
		//private string _userName;
		private string _textMessage;
		private string _textMessageVisibility;
		private SolidColorBrush _textMessageBrush;
		private ICommand _loginCommand;
		private readonly UserControl _view;
		private readonly WizardModel _wizardModel;

		public LoginViewModel(WizardModel wizardModel,object view): base(view)
		{
			_isValid = false;
			_view =(UserControl)view;
			_wizardModel = wizardModel;
			_textMessageVisibility = "Collapsed";
		}
		
		public override string DisplayName => "Login";
		public override bool IsValid
		{
			get => _isValid;
			set
			{
				if (_isValid == value)
					return;

				_isValid = value;
				OnPropertyChanged(nameof(IsValid));
			}
		}
		public string Url
		{
			get => _wizardModel.UserCredentials.ServiceUrl;
			set
			{
				_wizardModel.UserCredentials.ServiceUrl = value;
				OnPropertyChanged(nameof(Url));
			}
		}
		public string UserName
		{
			get => _wizardModel.UserCredentials.UserName;
			set
			{
				_wizardModel.UserCredentials.UserName = value;
				OnPropertyChanged(nameof(UserName));
			}
		}
		public string TextMessage
		{
			get => _textMessage;
			set
			{
				_textMessage = value;
				OnPropertyChanged(nameof(TextMessage));
			}
		}
		public string TextMessageVisibility
		{
			get => _textMessageVisibility;
			set
			{
				_textMessageVisibility = value;
				OnPropertyChanged(nameof(TextMessageVisibility));
			}
		}

		public SolidColorBrush TextMessageBrush
		{
			get => _textMessageBrush;
			set
			{
				_textMessageBrush = value;
				OnPropertyChanged(nameof(TextMessageBrush));
			}
		}	

	//	public ICommand LoginCommand => _loginCommand ?? (_loginCommand = new AwaitableDelegateCommand(AuthenticateUser));
		public ICommand LoginCommand => _loginCommand ?? (_loginCommand = new ParameterCommand(AuthenticateUser));

		private async void AuthenticateUser(object parameter)
		{
			var passwordBox = parameter as PasswordBox;
				var password = passwordBox?.Password;
			var test = _view.FindName("PasswordBox") as PasswordBox;
			if (!string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(password))
			{
				_wizardModel.UserCredentials.UserName = UserName;
				_wizardModel.UserCredentials.Password = password;
				_wizardModel.UserCredentials.ServiceUrl = Url.TrimEnd().TrimStart();
				
				if (Uri.IsWellFormedUriString(Url, UriKind.Absolute))
				{
						var statusCode = await Authentication.Login(_wizardModel.UserCredentials);
					if (statusCode == HttpStatusCode.OK)
					{
						IsValid = true;
						TextMessage = PluginResources.AuthenticationSuccess;
						TextMessageBrush = (SolidColorBrush)new BrushConverter().ConvertFrom("#017701");
						TextMessageVisibility = "Visible";
						_view.Dispatcher.Invoke(delegate { SendKeys.SendWait("{TAB}"); }, DispatcherPriority.ApplicationIdle);
					}
					else
					{
						ShowErrorMessage(statusCode.ToString());
					}
				}
				else
				{
					ShowErrorMessage(PluginResources.Incorrect_Url_Format);
				}
			}
			else
			{
				ShowErrorMessage(PluginResources.Required_Fields);
			}
		}

		private void ShowErrorMessage(string message)
		{
			TextMessageVisibility = "Visible";
			TextMessage = message;
			TextMessageBrush = new SolidColorBrush(Colors.Red);
		}
	}
}
