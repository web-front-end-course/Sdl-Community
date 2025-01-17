﻿using System;
using System.Reflection;
using System.Web;
using NLog;

namespace GoogleCloudTranslationProvider.Service
{
	public class HtmlUtil
	{
		private readonly Logger _logger = LogManager.GetCurrentClassLogger();

		public string HtmlDecode(string input)
		{
			try
			{
				return HttpUtility.HtmlDecode(input);
			}
			catch (Exception e)
			{
				_logger.Error($"{MethodBase.GetCurrentMethod().Name} {e.Message}\n {e.StackTrace}");
				return input;
			}
		}
	}
}