using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace AnalyseProjects
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{

			AppDomain.CurrentDomain.UnhandledException += (sn, ev) =>
			{
				MessageBox.Show("ERROR: " + ((Exception)ev.ExceptionObject).Message);
			};
			base.OnStartup(e);
		}
	}
}
