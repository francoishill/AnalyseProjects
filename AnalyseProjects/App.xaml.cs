using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using SharedClasses;

namespace AnalyseProjects
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			AutoUpdating.CheckForUpdates_ExceptionHandler();
			
			/*AppDomain.CurrentDomain.UnhandledException += (sn, ev) =>
			{
				MessageBox.Show("ERROR: " + ((Exception)ev.ExceptionObject).Message);
			};*/			

			base.OnStartup(e);

			MainWindow mw = new MainWindow();
			mw.ShowDialog();
		}
	}
}
