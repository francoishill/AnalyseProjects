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
using System.Diagnostics;

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

			//List<string> errors = new List<string>();
			foreach (var appname in applist)
			{
				string tmperr;
				OwnApplicationItem tmpApp = new OwnApplicationItem(appname, out tmperr);
				ApplicationsList.Add(tmpApp);
				if (!string.IsNullOrWhiteSpace(tmperr))
					AppendError(tmpApp, tmperr);
				//errors.Add(tmperr);
			}
			//if (errors.Count > 0)
			//    UserMessages.ShowWarningMessage(
			//        "Error messages creating OwnApplicationItems:"
			//        + Environment.NewLine + Environment.NewLine
			//        + string.Join(Environment.NewLine, errors));

			datagridApplicationsList.ItemsSource = ApplicationsList;
		}

		private static void _appendToRichTextbox(RichTextBox richTextbox, string msg, Brush foregroundColor)
		{
			var run = new Run(msg);
			if (foregroundColor != null)
				run.Foreground = foregroundColor;
			richTextbox.Document.Blocks.Add(new Paragraph(run));
			richTextbox.ScrollToEnd();
		}

		private void UpdateCurrentItemDisplayedMessages()
		{
			richtextboxCurrentItemMessages.Document.Blocks.Clear();
			if (datagridApplicationsList.SelectedItems.Count != 1)
				return;
			OwnApplicationItem app = datagridApplicationsList.SelectedItems[0] as OwnApplicationItem;
			if (app == null)
				return;
			if (recordedMessages.ContainsKey(app))
				foreach (var mesAndBrush in recordedMessages[app])
					_appendToRichTextbox(richtextboxCurrentItemMessages, mesAndBrush.Key, mesAndBrush.Value);
		}

		Dictionary<OwnApplicationItem, List<KeyValuePair<string, Brush>>> recordedMessages = new Dictionary<OwnApplicationItem, List<KeyValuePair<string, Brush>>>();
		private void AppendMessage(OwnApplicationItem relevantApp, string message, Brush foregroundColor = null)
		{
			string datetimeStr = "[" + DateTime.Now.ToString("HH:mm:ss") + "] ";
			string msgToWrite = datetimeStr + message;
			if (!recordedMessages.ContainsKey(relevantApp))
				recordedMessages.Add(relevantApp, new List<KeyValuePair<string, Brush>>());
			recordedMessages[relevantApp].Add(new KeyValuePair<string, Brush>(msgToWrite, foregroundColor));
			UpdateCurrentItemDisplayedMessages();

			_appendToRichTextbox(richtextboxMessages, msgToWrite, foregroundColor);
		}

		private void AppendError(OwnApplicationItem relevantItem, string errmsg)
		{
			AppendMessage(relevantItem, errmsg, Brushes.Red);
		}

		private void AppendWarning(OwnApplicationItem relevantItem, string warnmsg)
		{
			AppendMessage(relevantItem, warnmsg, Brushes.Orange);
		}

		private void buttonAnalyse_Click(object sender, RoutedEventArgs e)
		{
			foreach (var app in ApplicationsList)
				app.SetCurrentColor(Brushes.Gray);

			//Dictionary<string, bool> errorsTrueWarningsFalse = new Dictionary<string, bool>();
			//Cannot use Dictionary<string, bool> because we might have multiple Key's
			//List<KeyValuePair<string, bool>> errorsTrueWarningsFalse = new List<KeyValuePair<string, bool>>();
			foreach (var app in ApplicationsList)
			{
				string tmperr;
				List<string> warnings;
				bool success = app.DoAnalysis(out tmperr, out warnings);

				if (warnings != null && warnings.Count > 0)//Warnings can occur although we have success == true
					foreach (var warn in warnings)
					{
						//errorsTrueWarningsFalse.Add(new KeyValuePair<string,bool>(warn, false));
						AppendWarning(app, warn);
					}
				if (!success)
				{
					AppendError(app, tmperr);
					//errorsTrueWarningsFalse.Add(new KeyValuePair<string,bool>(tmperr, true));
				}
			}

			//foreach (var distinctErr in errorsTrueWarningsFalse.Distinct())
			//    if (distinctErr.Value)//Is error
			//        AppendError(distinctErr.Key);
			//    else
			//        AppendWarning(distinctErr.Key);

			//if (errors.Count > 0)
			//    UserMessages.ShowWarningMessage(
			//        "The following errors occurred during analysis:"
			//        + Environment.NewLine + Environment.NewLine
			//        + string.Join(Environment.NewLine, errors));

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
				e.Column.Header = displayName;
			string descriptionForTooltips = ReflectionInterop.GetPropertyDescription(e.PropertyDescriptor);
			if (!string.IsNullOrWhiteSpace(descriptionForTooltips))
				ToolTipService.SetToolTip(e.Column, descriptionForTooltips);
		}

		private void PerformIfNotNullDataContext(object senderPossibleOwnApplicationItem, Action<OwnApplicationItem> actionOnItemIfNotNull)
		{
			FrameworkElement fe = senderPossibleOwnApplicationItem as FrameworkElement;
			if (fe == null) return;
			OwnApplicationItem item = fe.DataContext as OwnApplicationItem;
			if (item == null) return;
			actionOnItemIfNotNull(item);
		}

		private void menuitemOpenInCSharp_Click(object sender, RoutedEventArgs e)
		{
			PerformIfNotNullDataContext(sender, (item) =>
			{
				item.OpenInCSharpExpress();
			});
		}

		private void datagridApplicationsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateCurrentItemDisplayedMessages();
		}
	}

	public class OwnApplicationItem : INotifyPropertyChanged
	{
		private ImageSource _applicationicon;
		[DisplayName("Icon")]
		public ImageSource ApplicationIcon
		{
			get { if (_applicationicon == null) if (ApplicationIconPath != null) _applicationicon = IconsInterop.IconExtractor.Extract(ApplicationIconPath, IconsInterop.IconExtractor.IconSize.Large).IconToImageSource(); return _applicationicon; }
		}

		[DisplayName("Application Name")]
		public string ApplicationName { get; private set; }
		protected string ApplicationIconPath { get; private set; }
		protected string SolutionFilePath { get; private set; }
		protected KeyValuePair<string, OwnAppsInterop.ApplicationTypes>? CsProjectRelativeToSolutionFilePath { get; private set; }
		[DisplayName("Installed")]
		[Description("Is the application installed locally on this machine.")]
		public bool IsInstalled { get; private set; }
		[DisplayName("Versioned")]
		[Description("Is the source code of this application Version Controlled (like with SVN or GIT).")]
		public bool IsVersionControlled { get; private set; }

		private Brush _currentcolor;
		protected Brush CurrentColor { get { return _currentcolor; } set { _currentcolor = value; OnPropertyChanged("CurrentColor"); } }

		private bool? _folderstructurecorrect;
		//NULL means not checked yet
		[DisplayName("Folders")]
		[Description(@"Whether the folder structure is correct solution must be in MyApp\MyApp.sln and csproj must be in MyApp\MyApp\MyApp.csproj")]
		public bool? FolderStructureCorrect { get { return _folderstructurecorrect; } set { _folderstructurecorrect = value; OnPropertyChanged("FolderStructureCorrect"); } }

		private bool? _relativepathscorrect;
		//NULL means not checked yet
		[DisplayName("Filepaths")]
		[Description("Have we checked that all paths (in csproj files) are relative (and correct) instead of absolute.")]
		public bool? RelativePathsCorrect { get { return _relativepathscorrect; } set { _relativepathscorrect = value; OnPropertyChanged("RelativePathsCorrect"); } }

		private bool? _appiconimplemented;
		//NULL means not checked yet
		[DisplayName("App Icon")]
		[Description("Whether the application's icon is actually implemented in the C# project.")]
		public bool? AppIconImplemented { get { return _appiconimplemented; } set { _appiconimplemented = value; OnPropertyChanged("AppIconImplemented"); } }

		private bool? _unhandledexceptionshandled;
		//NULL means not checked yet
		[DisplayName("Exceptions")]
		[Description("Are the Unhandled Exceptions handled via an event handler inside our application's main entry point.")]
		public bool? UnhandledExceptionsHandled { get { return _unhandledexceptionshandled; } set { _unhandledexceptionshandled = value; OnPropertyChanged("UnhandledExceptionsHandled"); } }

		private bool? _autoupdatingimplemented;
		//NULL means not checked yet
		[DisplayName("Auto Updating")]
		[Description("Whether Auto Updating is implemented in our application's main entry point.")]
		public bool? AutoUpdatingImplemented { get { return _autoupdatingimplemented; } set { _autoupdatingimplemented = value; OnPropertyChanged("AutoUpdatingImplemented"); } }

		private bool? _licensingimplemented;
		//NULL means not checked yet
		[DisplayName("Licensing")]
		[Description("Whether Licensing is implemented in our application's main entry point.")]
		public bool? LicensingImplemented { get { return _licensingimplemented; } set { _licensingimplemented = value; OnPropertyChanged("LicensingImplemented"); } }

		private bool? _aboutboximplemented;
		//NULL means not checked yet
		[DisplayName("AboutBox")]
		[Description("Is the AboutBox implemented in the application.")]
		public bool? AboutBoxImplemented { get { return _aboutboximplemented; } set { _aboutboximplemented = value; OnPropertyChanged("AboutBoxImplemented"); } }

		private bool? _policiesimplemented;
		//NULL means not checked yet
		[DisplayName("Policies")]
		[Description("Are all the correct policies implemented into the NSIS setup file (like privacy policies, etc).")]
		public bool? PoliciesImplemented { get { return _policiesimplemented; } set { _policiesimplemented = value; OnPropertyChanged("PoliciesImplemented"); } }

		private bool? _issuetrackingimplemented;
		//NULL means not checked yet
		[DisplayName("Issues")]
		[Description("Whether an issue tracking system is implemented (like Trac).")]
		public bool? IssueTrackingImplemented { get { return _issuetrackingimplemented; } set { _issuetrackingimplemented = value; OnPropertyChanged("IssueTrackingImplemented"); } }

		public OwnApplicationItem(string ApplicationName, out string errorIfFailed)
		{
			errorIfFailed = "";
			string tmperr;

			this.ApplicationName = ApplicationName;

			this.SolutionFilePath = OwnAppsInterop.GetSolutionPathFromApplicationName(ApplicationName, out tmperr);
			if (tmperr != null) errorIfFailed += tmperr + "|";

			this.CsProjectRelativeToSolutionFilePath = OwnAppsInterop.GetRelativePathToCsProjWithSameFilenameAsSolution(this.SolutionFilePath, out tmperr);
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

		private string GetCsProjFullPath()
		{
			return Path.Combine(Path.GetDirectoryName(this.SolutionFilePath), CsProjectRelativeToSolutionFilePath.Value.Key);
		}

		public bool DoAnalysis(out string errorIfFailed, out List<string> warnings)
		{
			warnings = null;
			//IsVersionControlled = false;

			//Reset to NULL
			this.FolderStructureCorrect = null;
			this.RelativePathsCorrect = null;
			this.AppIconImplemented = null;
			this.UnhandledExceptionsHandled = null;
			this.AutoUpdatingImplemented = null;
			this.LicensingImplemented = null;
			this.AboutBoxImplemented = null;
			this.PoliciesImplemented = null;
			this.IssueTrackingImplemented = null;

			if (this.SolutionFilePath == null)
			{
				warnings = null;
				errorIfFailed = "Solution path may not be NULL for application '" + this.ApplicationName + "'";
				return false;
			}
			if (this.CsProjectRelativeToSolutionFilePath == null)
			{
				warnings = null;
				errorIfFailed = "CsProjectRelativeToSolutionFilePath may not be NULL for application '" + this.ApplicationName + "'";
				return false;
			}

			int todoCheckForUnhandledExceptionsHandledIsRedundant;
			//TODO: In the code of CheckForUpdates_ExceptionHandler, it also registers an event handler for Unhandled Exceptions
			if (!DetermineIfFolderStructureForSolutionAndCsprojAreCorrect(out errorIfFailed))
				return false;
			if (!DoubleCheckPathsInCsProjectAreCorrectAndAreRelative(out errorIfFailed, out warnings))
				return false;
			if (!DetermineIfAppIconImplemented(out errorIfFailed))
				return false;
			if (!DetermineIfUnhandledExceptionsHandled(out errorIfFailed))
				return false;
			if (!DetermineIfAutoUpdatingIsImplemented(out errorIfFailed))
				return false;
			if (!DetermineIfLicensingIsImplemented(out errorIfFailed))
				return false;
			if (!DetermineIfAboutBoxIsImplemented(out errorIfFailed))
				return false;
			if (!DetermineIfPoliciesArePartOfNsisSetup(out errorIfFailed))
				return false;
			if (!DetermineIfIssueTrackingIsImplemented(out errorIfFailed))
				return false;

			int todoCentralizeAutoStartupWithWindows;
			//TODO: Centralize the AutoStartupWithWindows also for all apps, so we can see in this analysis tool if it will by default startup with windows
			return true;
		}

		private bool DetermineIfAppIconImplemented(out string errorIfFailed)
		{
			List<string> tmpcachedErrors = new List<string>();//Cache each error because we might have multiple csproj paths for a solution
			if (!this.CsProjectRelativeToSolutionFilePath.HasValue)
				tmpcachedErrors.Add("CsProjectRelativeToSolutionFilePath is NULL");
			else if (this.CsProjectRelativeToSolutionFilePath.Value.Value == OwnAppsInterop.ApplicationTypes.DLL)
				tmpcachedErrors.Add("The .csproj file (with the same name as the solution) is of type DLL.");
			else
			{
				string tmpAppIconRelativePath;
				string tmperr;
				string tmpCsprojFullpath = Path.Combine(Path.GetDirectoryName(this.SolutionFilePath), CsProjectRelativeToSolutionFilePath.Value.Key);
				bool? appIconImplementedInThisProj = OwnAppsInterop.IsAppIconImplemented(tmpCsprojFullpath, out tmpAppIconRelativePath, out tmperr);
				if (appIconImplementedInThisProj.HasValue)
				{
					this.AppIconImplemented = appIconImplementedInThisProj;
				}
				else
					tmpcachedErrors.Add(tmperr);
			}
			if (this.AppIconImplemented == null)//We did not find a relevant AppIcon in any of the csproj files connected to this solution file
			{
				errorIfFailed = "Unable to determine AppIconImplemented for application '" + this.ApplicationName + "': "
					+ string.Join("|", tmpcachedErrors);
				return false;
			}
			errorIfFailed = null;
			return true;
		}

		private bool DetermineIfUnhandledExceptionsHandled(out string errorIfFailed)
		{
			this.UnhandledExceptionsHandled = false;
			List<string> tmpcachedErrors = new List<string>();//Cache each error because we might have multiple csproj paths for a solution
			if (!this.CsProjectRelativeToSolutionFilePath.HasValue)
				tmpcachedErrors.Add("CsProjectRelativeToSolutionFilePath is NULL");
			else if (this.CsProjectRelativeToSolutionFilePath.Value.Value == OwnAppsInterop.ApplicationTypes.DLL)
				tmpcachedErrors.Add("The .csproj file (with the same name as the solution) is of type DLL.");
			else
			{
				string tmperr;
				string tmpCsprojFullpath = Path.Combine(Path.GetDirectoryName(this.SolutionFilePath), CsProjectRelativeToSolutionFilePath.Value.Key);
				bool? unhandledexceptionhandlingImplementedInThisProj = OwnAppsInterop.IsUnhandledExceptionHandlingImplemented(tmpCsprojFullpath, CsProjectRelativeToSolutionFilePath.Value.Value, out tmperr);
				if (unhandledexceptionhandlingImplementedInThisProj.HasValue)
				{
					this.UnhandledExceptionsHandled = unhandledexceptionhandlingImplementedInThisProj;
				}
				else
					tmpcachedErrors.Add(tmperr);
			}
			if (this.UnhandledExceptionsHandled == null)
			{
				errorIfFailed = "Unable to determine UnhandledExceptionsHandled for application '" + this.ApplicationName + "': "
					+ string.Join("|", tmpcachedErrors);
				return false;
			}
			errorIfFailed = null;
			return true;
		}

		private bool DetermineIfFolderStructureForSolutionAndCsprojAreCorrect(out string errorIfFailed)
		{
			if (this.SolutionFilePath == null)
			{
				this.FolderStructureCorrect = null;//Not determined yet
				errorIfFailed = "The SolutionFilePath is NULL.";
				return false;
			}
			if (this.CsProjectRelativeToSolutionFilePath == null)
			{
				this.FolderStructureCorrect = null;//Not determined yet
				errorIfFailed = "The CsProjectRelativeToSolutionFilePath is NULL.";
				return false;
			}
			string solutionFileRelativePathToVSroot = OwnAppsInterop.GetPathRelativeToVsRootFolder(this.SolutionFilePath, out errorIfFailed);
			if (solutionFileRelativePathToVSroot == null) return false;//The errorIfFailed is already populated
			string csprojFULLpath = Path.Combine(Path.GetDirectoryName(this.SolutionFilePath), this.CsProjectRelativeToSolutionFilePath.Value.Key);
			string csprojFileRelativePathToVSroot = OwnAppsInterop.GetPathRelativeToVsRootFolder(csprojFULLpath, out errorIfFailed);
			if (csprojFileRelativePathToVSroot == null) return false;

			string[] relativeSolutionPathSegments = solutionFileRelativePathToVSroot.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
			if (relativeSolutionPathSegments.Length != 2
				|| !relativeSolutionPathSegments[0].Equals(this.ApplicationName)
				|| !relativeSolutionPathSegments[1].Equals(this.ApplicationName + ".sln"))
			{
				this.FolderStructureCorrect = false;
				errorIfFailed = "The expected solution file relative path for application '" + this.ApplicationName + "' was '"
					+ this.ApplicationName + "\\" + this.ApplicationName + ".sln'"
					+ ", but instead it was '" + solutionFileRelativePathToVSroot + "'";
				return false;
			}

			string[] relativeCsProjPathSegments = csprojFileRelativePathToVSroot.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
			if (relativeCsProjPathSegments.Length != 3
				|| !relativeCsProjPathSegments[0].Equals(this.ApplicationName)
				|| !relativeCsProjPathSegments[1].Equals(this.ApplicationName)
				|| !relativeCsProjPathSegments[2].Equals(this.ApplicationName + ".csproj"))
			{
				this.FolderStructureCorrect = false;
				errorIfFailed = "The expected csproj file relative path for application '" + this.ApplicationName + "' was '"
					+ this.ApplicationName + "\\" + this.ApplicationName + "\\" + this.ApplicationName + ".csproj'"
					+ ", but instead it was '" + csprojFileRelativePathToVSroot + "'";
				return false;
			}

			errorIfFailed = null;
			this.FolderStructureCorrect = true;
			return true;
		}

		private bool DoubleCheckPathsInCsProjectAreCorrectAndAreRelative(out string errorIfFailed, out List<string> warnings)
		{
			//We only check the one csproj (even if there are many), we only care about the main EXE project with same name as solution
			bool success = OwnAppsInterop.FixIncludeFilepathsInCsProjFile(
				GetCsProjFullPath(),
				out errorIfFailed,
				out warnings);//Warnings are like if a specific Filepath was incorrect but we did not fix it
			this.RelativePathsCorrect = success;
			return success;
		}

		private bool DetermineIfAutoUpdatingIsImplemented(out string errorIfFailed)
		{
			this.AutoUpdatingImplemented = false;
			List<string> tmpcachedErrors = new List<string>();//Cache each error because we might have multiple csproj paths for a solution
			if (!this.CsProjectRelativeToSolutionFilePath.HasValue)
				tmpcachedErrors.Add("CsProjectRelativeToSolutionFilePath is NULL");
			else if (this.CsProjectRelativeToSolutionFilePath.Value.Value == OwnAppsInterop.ApplicationTypes.DLL)
				tmpcachedErrors.Add("The .csproj file (with the same name as the solution) is of type DLL.");
			else
			{
				string tmperr;
				string tmpCsprojFullpath = Path.Combine(Path.GetDirectoryName(this.SolutionFilePath), CsProjectRelativeToSolutionFilePath.Value.Key);
				bool? autoupdatingImplementedInThisProj = OwnAppsInterop.IsAutoUpdatingImplemented(tmpCsprojFullpath, this.CsProjectRelativeToSolutionFilePath.Value.Value, out tmperr);
				if (autoupdatingImplementedInThisProj.HasValue)
				{
					this.AutoUpdatingImplemented = autoupdatingImplementedInThisProj;
				}
				else
					tmpcachedErrors.Add(tmperr);
			}
			if (this.AutoUpdatingImplemented == null)
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
			this.LicensingImplemented = false;
			List<string> tmpcachedErrors = new List<string>();//Cache each error because we might have multiple csproj paths for a solution
			if (!this.CsProjectRelativeToSolutionFilePath.HasValue)
				tmpcachedErrors.Add("CsProjectRelativeToSolutionFilePath is NULL");
			else if (this.CsProjectRelativeToSolutionFilePath.Value.Value == OwnAppsInterop.ApplicationTypes.DLL)
				tmpcachedErrors.Add("The .csproj file (with the same name as the solution) is of type DLL.");
			else
			{
				string tmperr;
				string tmpCsprojFullpath = GetCsProjFullPath();
				bool? licensingImplementedInThisProj = OwnAppsInterop.IsLicensingImplemented(tmpCsprojFullpath, CsProjectRelativeToSolutionFilePath.Value.Value, out tmperr);
				if (licensingImplementedInThisProj.HasValue)
				{
					this.LicensingImplemented = licensingImplementedInThisProj;
				}
				else
					tmpcachedErrors.Add(tmperr);
			}
			if (this.LicensingImplemented == null)
			{
				errorIfFailed = "Unable to determine LicensingImplemented for application '" + this.ApplicationName + "': "
					+ string.Join("|", tmpcachedErrors);
				return false;
			}
			errorIfFailed = null;
			return true;
		}

		private bool DetermineIfAboutBoxIsImplemented(out string errorIfFailed)
		{
			errorIfFailed = "DetermineIfAboutBoxIsImplemented not implemented yet.";
			return false;
		}

		private bool DetermineIfPoliciesArePartOfNsisSetup(out string errorIfFailed)
		{
			errorIfFailed = "DetermineIfLicensePoliciesArePartOfNsisSetup not implemented yet.";
			return false;
		}

		private bool DetermineIfIssueTrackingIsImplemented(out string errorIfFailed)
		{
			errorIfFailed = "DetermineIfIssueTrackingIsImplemented not implemented yet.";
			return false;
		}

		public void OpenInCSharpExpress()
		{
			var csharpPath = RegistryInterop.GetAppPathFromRegistry("VCSExpress.exe");
			if (csharpPath == null)
			{
				UserMessages.ShowErrorMessage("Cannot obtain CSharp Express path from registry.");
				return;
			}
			Process.Start(csharpPath, "\"" + this.SolutionFilePath + "\"");
			/*ThreadingInterop.PerformOneArgFunctionSeperateThread<string>((cspath) =>
			{
				var proc = Process.Start(csharpPath, "\"" + this.SolutionFilePath + "\"");
				if (proc != null)
				{
					proc.WaitForExit();
					List<string> csprojectPaths;
					this.PerformBuild(null, out csprojectPaths);
				}
			},
			csharpPath,
			false);*/
		}

		public event PropertyChangedEventHandler PropertyChanged = delegate { };
		public void OnPropertyChanged(params string[] propertyNames) { foreach (var pn in propertyNames) PropertyChanged(this, new PropertyChangedEventArgs(pn)); }
	}
}
