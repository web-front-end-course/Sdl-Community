﻿using System;
using System.Windows.Input;

namespace GoogleCloudTranslationProvider.Commands
{
	public class CommandHandler : ICommand
	{
		private readonly Action _action;
		private readonly bool _canExecute;

		public event EventHandler CanExecuteChanged;

		public CommandHandler(Action action, bool canExecute)
		{
			_action = action;
			_canExecute = canExecute;
		}

		public CommandHandler(bool canExecute)
		{
			_canExecute = canExecute;
		}

		public bool CanExecute(object parameter)
		{
			return _canExecute;
		}

		public void Execute(object parameter)
		{
			_action();
		}
	}
}