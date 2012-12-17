using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using SharedClasses;
using System.Collections.ObjectModel;
using System.IO;
using System.ComponentModel;

namespace AnalyseProjects
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		ObservableCollection<OwnApplicationItem> ApplicationsList = new ObservableCollection<OwnApplicationItem>();

		public MainWindow()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			var applist =
				SettingsSimple.BuildTestSystemSettings.Instance.ListOfApplicationsToBuild
				.Concat(OwnAppsInterop.GetListOfInstalledApplications().Keys)
				.Distinct();

			List<string> errors = new List<string>();
			foreach (var appname in applist)
			{
				string tmperr;
				ApplicationsList.Add(new OwnApplicationItem(appname, out tmperr));
				if (!string.IsNullOrWhiteSpace(tmperr))
					errors.Add(tmperr);
			}
			if (errors.Count > 0)
				UserMessages.ShowWarningMessage(
					"Error messages creating OwnApplicationItems:"
					+ Environment.NewLine + Environment.NewLine
					+ string.Join(Environment.NewLine, errors));

			datagridApplicationsList.ItemsSource = ApplicationsList;
		}

		private void buttonAnalyse_Click(object sender, RoutedEventArgs e)
		{
			foreach (var app in ApplicationsList)
				app.SetCurrentColor(Brushes.Gray);

			List<string> errors = new List<string>();
			foreach (var app in ApplicationsList)
			{
				string tmperr;
				if (!app.DoAnalysis(out tmperr))
					errors.Add(tmperr);
			}

			if (errors.Count > 0)
				UserMessages.ShowWarningMessage(
					"The following errors occurred during analysis:"
					+ Environment.NewLine + Environment.NewLine
					+ string.Join(Environment.NewLine, errors));

			//Is app.ico implemented (not only does the file exist, but also is it used in .csproj file: <ApplicationIcon>app.ico</ApplicationIcon>
			//Is AutoUpdating implemented, check that it is in code (search through all files references in .csproj file), it must not be commented code, Console/Winforms: in void main(), WPF: in override OnStartup
			//Is Licensing implemented (same steps as for AutoUpdating)
			//Ensure that there are not ABSOLUTE paths in .csproj file, also ensure that all RELATIVE paths are CORRECT
			//
		}

		private void datagridApplicationsList_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
		{
			/*if (e.PropertyType == typeof(DateTime) || e.PropertyType == typeof(Nullable<DateTime>))
				(e.Column as DataGridTextColumn).Binding.StringFormat = "yyyy-MM-dd";
			else */
			if (e.PropertyType == typeof(Boolean) || e.PropertyType == typeof(Nullable<Boolean>))
			{
				if (e.PropertyType == typeof(Nullable<bool>))
				{
					DataGridCheckBoxColumn column = new DataGridCheckBoxColumn();
					column.Header = e.PropertyName;
					column.IsThreeState = e.PropertyType == typeof(Nullable<Boolean>);

					column.Binding = new Binding() { Mode = BindingMode.OneWay, Path = new PropertyPath(e.PropertyName) };
					e.Column = column;
				}
			}
			else if (e.PropertyType == typeof(ImageSource))
			{
				DataGridTemplateColumn column = new DataGridTemplateColumn();
				column.Header = e.PropertyName;
				column.CellTemplate = datagridApplicationsList.FindResource("ImageTemplate") as DataTemplate;
				e.Column = column;
			}

			string displayName = ReflectionInterop.GetPropertyDisplayName(e.PropertyDescriptor);
			if (!string.IsNullOrEmpty(displayName))
			{
				e.Column.Header = displayName;
			};
		}
	}

	public class OwnApplicationItem : INotifyPropertyChanged
	{
		[DisplayName("Application Name")]
		public string ApplicationName { get; private set; }
		protected string ApplicationIconPath { get; private set; }
		protected string SolutionFilePath { get; private set; }
		protected Dictionary<string, OwnAppsInterop.ApplicationTypes> CsProjectRelativeToSolutionFilePaths { get; private set; }
		[DisplayName("Installed")]
		public bool IsInstalled { get; private set; }
		[DisplayName("Versioned")]
		public bool IsVersionControlled { get; private set; }

		private Brush _currentcolor;
		protected Brush CurrentColor { get { return _currentcolor; } set { _currentcolor = value; OnPropertyChanged("CurrentColor"); } }

		private bool? _unhandledexceptionshandled;
		//NULL means not checked yet
		[DisplayName("Unhandled Exceptions")]
		public bool? UnhandledExceptionsHandled { get { return _unhandledexceptionshandled; } set { _unhandledexceptionshandled = value; OnPropertyChanged("UnhandledExceptionsHandled"); } }

		private bool? _appiconimplemented;
		//NULL means not checked yet
		[DisplayName("App Icon")]
		public bool? AppIconImplemented { get { return _appiconimplemented; } set { _appiconimplemented = value; OnPropertyChanged("AppIconImplemented"); } }
		
		private bool? _autoupdatingimplemented;
		//NULL means not checked yet
		[DisplayName("Auto Updating")]
		public bool? AutoUpdatingImplemented { get { return _autoupdatingimplemented; } set { _autoupdatingimplemented = value; OnPropertyChanged("AutoUpdatingImplemented"); } }
		private bool? _licensingimplemented;
		//NULL means not checked yet
		[DisplayName("Licensing")]
		public bool? LicensingImplemented { get { return _licensingimplemented; } set { _licensingimplemented = value; OnPropertyChanged("LicensingImplemented"); } }
		private bool? _lastcheckrelativepathscorrect;
		//NULL means not checked yet
		[DisplayName("Relative Paths")]
		public bool? RelativePathsCorrect { get { return _lastcheckrelativepathscorrect; } set { _lastcheckrelativepathscorrect = value; OnPropertyChanged("LastCheckRelativePathsCorrect"); } }

		private ImageSource _applicationicon;
		[DisplayName("Icon")]
		public ImageSource ApplicationIcon
		{
			get { if (_applicationicon == null) _applicationicon = IconsInterop.IconExtractor.Extract(ApplicationIconPath, IconsInterop.IconExtractor.IconSize.Large).IconToImageSource(); return _applicationicon; }
		}

		public OwnApplicationItem(string ApplicationName, out string errorIfFailed)
		{
			errorIfFailed = "";
			string tmperr;

			this.ApplicationName = ApplicationName;
			this.SolutionFilePath = OwnAppsInterop.GetSolutionPathFromApplicationName(ApplicationName, out tmperr);
			if (tmperr != null) errorIfFailed += tmperr + "|";
			this.CsProjectRelativeToSolutionFilePaths = OwnAppsInterop.GetRelativePathsToCsProjsFromSolutionFile(this.SolutionFilePath, out tmperr);
			if (tmperr != null) errorIfFailed += tmperr + "|";
			this.ApplicationIconPath = OwnAppsInterop.GetAppIconPath(ApplicationName, out tmperr);
			if (tmperr != null) errorIfFailed += tmperr + "|";
			this.IsInstalled = PublishInterop.IsInstalled(ApplicationName);
			this.IsVersionControlled = OwnAppsInterop.DirIsValidSvnPath(Path.GetDirectoryName(SolutionFilePath));

			this.CurrentColor = Brushes.Black;

			if (string.IsNullOrWhiteSpace(errorIfFailed))
				errorIfFailed = null;//Just make sure it's not a space or whitespace, its pure NULL
			if (errorIfFailed != null)
				errorIfFailed = errorIfFailed.TrimEnd('|');//Trim the last |
		}

		public void SetCurrentColor(Brush color)
		{
			this.CurrentColor = color;
		}

		public bool DoAnalysis(out string errorIfFailed)
		{
			//IsVersionControlled = false;

			//Reset to NULL
			this.AppIconImplemented = null;
			this.AutoUpdatingImplemented = null;
			this.LicensingImplemented = null;
			this.RelativePathsCorrect = null;

			if (this.SolutionFilePath == null)
			{
				errorIfFailed = "Solution path may not be NULL for application '" + this.ApplicationName + "'";
				return false;
			}
			if (this.CsProjectRelativeToSolutionFilePaths == null)
			{
				errorIfFailed = "CsProjectRelativeToSolutionFilePaths may not be NULL for application '" + this.ApplicationName + "'";
				return false;
			}

			if (!DetermineIfUnhandledExceptionsHandled(out errorIfFailed))
				return false;
			if (!DetermineIfAppIconImplemented(out errorIfFailed))
				return false;
			if (!DetermineIfAutoUpdatingIsImplemented(out errorIfFailed))
				return false;
			if (!DetermineIfLicensingIsImplemented(out errorIfFailed))
				return false;
			if (!DoubleCheckPathsInProjectsAreCorrectAndRelativePaths(out errorIfFailed))
				return false;

			return true;
		}

		private bool DetermineIfUnhandledExceptionsHandled(out string errorIfFailed)
		{
			errorIfFailed = "DetermineIfUnhandledExceptionsHandled not implemented yet.";
			return false;

			//errorIfFailed = null;
			//return true;
		}

		private bool DetermineIfAppIconImplemented(out string errorIfFailed)
		{
			List<string> tmpcachedErrors = new List<string>();//Cache each error because we might have multiple csproj paths for a solution
			if (this.CsProjectRelativeToSolutionFilePaths.Count(kv => kv.Value != OwnAppsInterop.ApplicationTypes.DLL) > 0)
			{
				foreach (var csProjPathsForExeTypes in this.CsProjectRelativeToSolutionFilePaths.Where(kv => kv.Value != OwnAppsInterop.ApplicationTypes.DLL))
				{
					string tmpAppIconRelativePath;
					string tmperr;
					string tmpCsprojFullpath = Path.Combine(Path.GetDirectoryName(this.SolutionFilePath), csProjPathsForExeTypes.Key);
					bool? appIconImplementedInThisProj = OwnAppsInterop.IsAppIconImplemented(tmpCsprojFullpath, out tmpAppIconRelativePath, out tmperr);
					if (appIconImplementedInThisProj.HasValue)
					{
						this.AppIconImplemented = appIconImplementedInThisProj;
						break;
					}
					else
						tmpcachedErrors.Add(tmperr);
				}
			}
			else
				tmpcachedErrors.Add("CsProjectRelativeToSolutionFilePaths.Count(only WinExe or Exe projects) should be > 0.");
			if (this.AppIconImplemented == null)//We did not find a relevant AppIcon in any of the csproj files connected to this solution file
			{
				errorIfFailed = "Unable to determine AppIconImplemented for application '" + this.ApplicationName + "': "
					+ string.Join("|", tmpcachedErrors);
				return false;
			}
			errorIfFailed = null;
			return true;
		}

		private bool DetermineIfAutoUpdatingIsImplemented(out string errorIfFailed)
		{
			List<string> tmpcachedErrors = new List<string>();//Cache each error because we might have multiple csproj paths for a solution
			if (this.CsProjectRelativeToSolutionFilePaths.Count(kv => kv.Value != OwnAppsInterop.ApplicationTypes.DLL) > 0)
			{
				foreach (var csProjPathsForExeTypes in this.CsProjectRelativeToSolutionFilePaths.Where(kv => kv.Value != OwnAppsInterop.ApplicationTypes.DLL))
				{
					string tmperr;
					string tmpCsprojFullpath = Path.Combine(Path.GetDirectoryName(this.SolutionFilePath), csProjPathsForExeTypes.Key);
					bool? autoupdatingImplementedInThisProj = OwnAppsInterop.IsAutoUpdatingImplemented(tmpCsprojFullpath, this.CsProjectRelativeToSolutionFilePaths[csProjPathsForExeTypes.Key], out tmperr);
					if (autoupdatingImplementedInThisProj.HasValue)
					{
						this.AutoUpdatingImplemented = autoupdatingImplementedInThisProj;
					}
					else
						tmpcachedErrors.Add(tmperr);
				}
			}
			else
				tmpcachedErrors.Add("CsProjectRelativeToSolutionFilePaths.Count(only WinExe or Exe projects) should be > 0.");
			if (this.AppIconImplemented == null)
			{
				errorIfFailed = "Unable to determine AutoUpdatingImplemented for application '" + this.ApplicationName + "': "
					+ string.Join("|", tmpcachedErrors);
				return false;
			}
			errorIfFailed = null;
			return true;
		}

		private bool DetermineIfLicensingIsImplemented(out string errorIfFailed)
		{
			errorIfFailed = "DetermineIfLicensingIsImplemented not implemented yet.";
			return false;
		}

		private bool DoubleCheckPathsInProjectsAreCorrectAndRelativePaths(out string errorIfFailed)
		{
			errorIfFailed = "DoubleCheckPathsInProjectsAreCorrectAndRelativePaths not implemented yet.";
			return false;
		}

		public event PropertyChangedEventHandler PropertyChanged = delegate { };
		public void OnPropertyChanged(params string[] propertyNames) { foreach (var pn in propertyNames) PropertyChanged(this, new PropertyChangedEventArgs(pn)); }
	}
}
