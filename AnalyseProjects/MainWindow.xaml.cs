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
			SettingsSimple.OnErrorOverrideShowingUsermessage += (sn, ev) =>
			{
				this.Dispatcher.Invoke((Action)delegate
				{
					AppendError(null, ev.GetException().Message);
				});
				UserMessages.ShowErrorMessage(ev.GetException().Message);
			};
			SettingsSimple.OnWarningOverrideShowingUsermessage += (sn, ev) =>
			{
				this.Dispatcher.Invoke((Action)delegate
				{
					AppendWarning(null, ev.GetException().Message);
				});
				UserMessages.ShowWarningMessage(ev.GetException().Message);
			};
			//Just keep an eye on when the online settings were actually updated
			SettingsSimple.OnOnlineSettingsSavedSuccessfully += delegate
			{
				ShowNoCallbackNotificationInterop.Notify(
					err => UserMessages.ShowErrorMessage(err),
					"Settings saved online for 'BuildTestSystemSettings'",
					null,
					ShowNoCallbackNotificationInterop.NotificationTypes.Info,
					3);
			};

			var AnalyseAppsList = SettingsSimple.AnalyseProjectsSettings.Instance.ListOfApplicationsToAnalyse;
			//var InstalledApps = OwnAppsInterop.GetListOfInstalledApplications().Keys;
			//SettingsSimple.AnalyseProjectsSettings.EnsureDefaultItemsInList();

			var FilteredUnwanted =
				AnalyseAppsList
				//.Concat(InstalledApps)
				.Where(app =>
					!app.Equals("AutoConnectWifiAdhoc", StringComparison.InvariantCultureIgnoreCase)
					&& !app.Equals("ApplicationManager", StringComparison.InvariantCultureIgnoreCase)
					&& !app.Equals("SharedClasses", StringComparison.InvariantCultureIgnoreCase)
					//&& !app.Equals("AddDependenciesCSharp", StringComparison.InvariantCultureIgnoreCase)
					&& !app.Equals("LicensingServer", StringComparison.InvariantCultureIgnoreCase)
					&& !app.Equals("CodeSnippets", StringComparison.InvariantCultureIgnoreCase))
				.Distinct();

			//List<string> errors = new List<string>();
			foreach (var appname in FilteredUnwanted)
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
			if (string.IsNullOrWhiteSpace(msg))
				return;//We do not want blank messages, pointless

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

			if (relevantApp != null)
			{
				if (!recordedMessages.ContainsKey(relevantApp))
					recordedMessages.Add(relevantApp, new List<KeyValuePair<string, Brush>>());
				recordedMessages[relevantApp].Add(new KeyValuePair<string, Brush>(msgToWrite, foregroundColor));
				UpdateCurrentItemDisplayedMessages();
			}

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
			DataGrid dg = sender as DataGrid;
			if (dg == null) return;

			if (e.PropertyName.Equals("CurrentForeground", StringComparison.InvariantCultureIgnoreCase))//We cannot simply make 'CurrentForeground' protected, otherwise the WPF Binding will not work
			{
				e.Cancel = true;
				return;
			}

			/*if (e.PropertyType == typeof(DateTime) || e.PropertyType == typeof(Nullable<DateTime>))
				(e.Column as DataGridTextColumn).Binding.StringFormat = "yyyy-MM-dd";
			else */
			if (e.PropertyType == typeof(Boolean) || e.PropertyType == typeof(Nullable<Boolean>))
			{
				//if (e.PropertyType == typeof(Nullable<bool>))
				//{
				DataGridCheckBoxColumn column = new DataGridCheckBoxColumn();
				column.Header = e.PropertyName;
				column.IsReadOnly = false;
				column.IsThreeState = e.PropertyType == typeof(Nullable<Boolean>);
				column.Binding = new Binding() { Mode = BindingMode.OneWay, Path = new PropertyPath(e.PropertyName) };
				column.ElementStyle = dg.FindResource("DiscreteCheckBoxStyle_Readonly") as Style;
				e.Column = column;
				//}
			}
			else if (e.PropertyType == typeof(ImageSource))
			{
				DataGridTemplateColumn column = new DataGridTemplateColumn();
				column.Header = e.PropertyName;
				column.CellTemplate = dg.FindResource("ImageTemplate") as DataTemplate;
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

		private void menuitemExploreToCsprojeFile_Click(object sender, RoutedEventArgs e)
		{
			PerformIfNotNullDataContext(sender, (item) =>
			{
				item.ExploreToCsprojFullPath();
			});
		}

		private void menuitemCopyCsProjFileFullPath_Click(object sender, RoutedEventArgs e)
		{
			PerformIfNotNullDataContext(sender, (item) =>
			{
				item.CopyCsprojFileFullPathToClipboard();
			});
		}

		private void datagridApplicationsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateCurrentItemDisplayedMessages();
		}

		private void about_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			AboutWindow2.ShowAboutWindow(new System.Collections.ObjectModel.ObservableCollection<DisplayItem>()
			{
				new DisplayItem("Author", "Francois Hill"),
				new DisplayItem("Icon(s) obtained from", null)//"http://www.icons-land.com", "http://www.icons-land.com/vista-base-software-icons.php")
			});
		}
	}

	public class OwnApplicationItem : INotifyPropertyChanged
	{
		//public Brush CurrentForeground { get { return Brushes.Orange; } }

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

		//protected KeyValuePair<string, OwnAppsInterop.ApplicationTypes>? CsProjectRelativeToSolutionFilePath { get; private set; }
		protected string CsProjectRelativeToSolution { get; private set; }

		[DisplayName("AppType")]
		[Description("The application type for the main .csproj file (can be WPF, Winforms, Console, DLL)")]
		public OwnAppsInterop.ApplicationTypes? CsprojApplicationType { get; private set; }//No OnPropertyChanged as it only needs to be set in Constructor

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

		private bool? _mainwinorformimplemented;
		//NULL means not checked yet
		[DisplayName("Main")]
		[Description("Has got a MainWindow (WPF) or a MainForm (Winforms) which is also implemented inside the application's Main Entry point.")]
		public bool? MainWinOrFormImplemented { get { return _mainwinorformimplemented; } set { _mainwinorformimplemented = value; OnPropertyChanged("MainWinOrFormImplemented"); } }

		private string MainWindowOfFormCodeBehindRelativePath = null;

		private bool? _appiconimplemented;
		//NULL means not checked yet
		[DisplayName("App Icon")]
		[Description("Whether the application's icon is actually implemented in the C# project.")]
		public bool? AppIconImplemented { get { return _appiconimplemented; } set { _appiconimplemented = value; OnPropertyChanged("AppIconImplemented"); } }

		/* Removed for now as it is already included in the AutoUpdating check, ie. part of the method "AutoUpdating.CheckForUpdates_ExceptionHandler("
		private bool? _unhandledexceptionshandled;
		//NULL means not checked yet
		[DisplayName("Exceptions")]
		[Description("Are the Unhandled Exceptions handled via an event handler inside our application's main entry point.")]
		public bool? UnhandledExceptionsHandled { get { return _unhandledexceptionshandled; } set { _unhandledexceptionshandled = value; OnPropertyChanged("UnhandledExceptionsHandled"); } }*/

		private bool? _autoupdatingimplemented;
		//NULL means not checked yet
		[DisplayName("Auto Updating")]
		[Description("Whether Auto Updating is implemented in our application's main entry point.")]
		public bool? AutoUpdatingImplemented { get { return _autoupdatingimplemented; } set { _autoupdatingimplemented = value; OnPropertyChanged("AutoUpdatingImplemented"); } }

		private bool? _tracenvironmentexists;
		//NULL means not checked yet
		[DisplayName("Trac")]
		[Description("Whether Trac environment (ticketing system) exists online.")]
		public bool? TracEnvironmentExists { get { return _tracenvironmentexists; } set { _tracenvironmentexists = value; OnPropertyChanged("TracEnvironmentExists"); } }

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

			var tmpkeyval = OwnAppsInterop.GetRelativePathToCsProjWithSameFilenameAsSolution(this.SolutionFilePath, out tmperr);
			this.CsprojApplicationType = null;
			this.CsProjectRelativeToSolution = null;
			if (tmpkeyval.HasValue)
			{
				this.CsProjectRelativeToSolution = tmpkeyval.Value.Key;
				this.CsprojApplicationType = tmpkeyval.Value.Value;
			}
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
			return Path.Combine(Path.GetDirectoryName(this.SolutionFilePath), this.CsProjectRelativeToSolution);
		}

		private OwnAppsInterop.ApplicationTypes GetCsProjAppType()
		{
			return this.CsprojApplicationType.Value;
		}

		public bool DoAnalysis(out string errorIfFailed, out List<string> warnings)
		{
			warnings = null;
			//IsVersionControlled = false;

			//Reset to NULL
			this.FolderStructureCorrect = null;
			this.RelativePathsCorrect = null;
			this.MainWinOrFormImplemented = null;
			this.AppIconImplemented = null;
			//this.UnhandledExceptionsHandled = null;
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
			if (this.CsProjectRelativeToSolution == null)
			{
				warnings = null;
				errorIfFailed = "CsProjectRelativeToSolutionFilePath may not be NULL for application '" + this.ApplicationName + "'";
				return false;
			}

			if (!DetermineIfFolderStructureForSolutionAndCsprojAreCorrect(out errorIfFailed))
				return false;
			if (!DoubleCheckPathsInCsProjectAreCorrectAndAreRelative(out errorIfFailed, out warnings))
				return false;
			if (!DetermineIfCsProjectHasMainFormOrWindowAndItsImplemented(out errorIfFailed))
				return false;
			if (!DetermineIfAppIconImplemented(out errorIfFailed))
				return false;
			//DONE: In the code of CheckForUpdates_ExceptionHandler, it also registers an event handler for Unhandled Exceptions
			/*if (!DetermineIfUnhandledExceptionsHandled(out errorIfFailed))
				return false;*/
			if (!DetermineIfAutoUpdatingIsImplemented(out errorIfFailed))
				return false;
			if (!DetermineIfTracEnvironmentExists(out errorIfFailed))
				return false;
			if (!DetermineIfLicensingIsImplemented(out errorIfFailed))
				return false;
			if (!DetermineIfAboutBoxIsImplemented(out errorIfFailed))
				return false;
			if (!DetermineIfPoliciesArePartOfNsisSetup(out errorIfFailed))
				return false;
			if (!DetermineIfIssueTrackingIsImplemented(out errorIfFailed))
				return false;

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
			if (this.CsProjectRelativeToSolution == null)
			{
				this.FolderStructureCorrect = null;//Not determined yet
				errorIfFailed = "The CsProjectRelativeToSolutionFilePath is NULL.";
				return false;
			}
			string solutionFileRelativePathToVSroot = OwnAppsInterop.GetPathRelativeToVsRootFolder(this.SolutionFilePath, out errorIfFailed);
			if (solutionFileRelativePathToVSroot == null) return false;//The errorIfFailed is already populated
			string csprojFileRelativePathToVSroot = OwnAppsInterop.GetPathRelativeToVsRootFolder(GetCsProjFullPath(), out errorIfFailed);
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

			//Now ensure we have ScreenShots
			string screenshotsDir = Path.Combine(Path.GetDirectoryName(GetCsProjFullPath()), "ScreenShots");
			if (!Directory.Exists(screenshotsDir)
				|| OwnAppsInterop.GetPicturesInDirectory(screenshotsDir).Length == 0)
			{
				this.FolderStructureCorrect = false;
				errorIfFailed = "No screenshots found for project of '" + this.ApplicationName + "'.";
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

		private bool DetermineIfCsProjectHasMainFormOrWindowAndItsImplemented(out string errorIfFailed)
		{
			var apptype = this.GetCsProjAppType();
			if (apptype != OwnAppsInterop.ApplicationTypes.WPF && apptype != OwnAppsInterop.ApplicationTypes.Winforms)
			{
				this.MainWinOrFormImplemented = null;
				errorIfFailed = null;
				return true;
			}

			//Only WPF and Winforms apptypes will get to here
			string mainWinOrFormCodebehindRelativePathToCsproj;
			this.MainWinOrFormImplemented
				= OwnAppsInterop.CheckIfCsprojHasMainWinOrFormAndIfItsImplemented(
				this.GetCsProjFullPath(),
				this.GetCsProjAppType(),
				out mainWinOrFormCodebehindRelativePathToCsproj,
				out errorIfFailed);
			if (!this.MainWinOrFormImplemented.HasValue)//error
				return false;
			else
			{
				this.MainWindowOfFormCodeBehindRelativePath = mainWinOrFormCodebehindRelativePathToCsproj;
				return this.MainWinOrFormImplemented.Value;
			}
		}

		private bool DetermineIfAppIconImplemented(out string errorIfFailed)
		{
			List<string> tmpcachedErrors = new List<string>();//Cache each error because we might have multiple csproj paths for a solution
			if (this.CsProjectRelativeToSolution == null)
				tmpcachedErrors.Add("CsProjectRelativeToSolution is NULL");
			else if (this.GetCsProjAppType() == OwnAppsInterop.ApplicationTypes.DLL)
				tmpcachedErrors.Add("The .csproj file (with the same name as the solution) is of type DLL.");
			else
			{
				string tmpAppIconRelativePath;
				string tmperr;
				string tmpCsprojFullpath = Path.Combine(Path.GetDirectoryName(this.SolutionFilePath), this.CsProjectRelativeToSolution);
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

		/*private bool DetermineIfUnhandledExceptionsHandled(out string errorIfFailed)
		{
			this.UnhandledExceptionsHandled = false;
			List<string> tmpcachedErrors = new List<string>();//Cache each error because we might have multiple csproj paths for a solution
			if (this.CsProjectRelativeToSolution == null)
				tmpcachedErrors.Add("CsProjectRelativeToSolutionFilePath is NULL");
			else if (this.GetCsProjAppType() == OwnAppsInterop.ApplicationTypes.DLL)
				tmpcachedErrors.Add("The .csproj file (with the same name as the solution) is of type DLL.");
			else
			{
				string tmperr;
				bool? unhandledexceptionhandlingImplementedInThisProj = OwnAppsInterop.IsUnhandledExceptionHandlingImplemented(this.GetCsProjFullPath(), this.GetCsProjAppType(), out tmperr);
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
		}*/

		private bool DetermineIfAutoUpdatingIsImplemented(out string errorIfFailed)
		{
			this.AutoUpdatingImplemented = false;
			List<string> tmpcachedErrors = new List<string>();//Cache each error because we might have multiple csproj paths for a solution
			if (this.CsProjectRelativeToSolution == null)
				tmpcachedErrors.Add("CsProjectRelativeToSolutionFilePath is NULL");
			else if (this.GetCsProjAppType() == OwnAppsInterop.ApplicationTypes.DLL)
				tmpcachedErrors.Add("The .csproj file (with the same name as the solution) is of type DLL.");
			else
			{
				string tmperr;
				this.AutoUpdatingImplemented = OwnAppsInterop.IsAutoUpdatingImplemented_AndNotUsingOwnUnhandledExceptionHandler(this.GetCsProjFullPath(), this.GetCsProjAppType(), out tmperr);
				if (!this.AutoUpdatingImplemented.HasValue)
					tmpcachedErrors.Add(tmperr);

				/*bool? autoupdatingImplementedInThisProj = OwnAppsInterop.IsAutoUpdatingImplemented_AndNotUsingOwnUnhandledExceptionHandler(this.GetCsProjFullPath(), this.GetCsProjAppType(), out tmperr);
				if (autoupdatingImplementedInThisProj.HasValue)
				{
					this.AutoUpdatingImplemented = autoupdatingImplementedInThisProj;
				}
				else
					tmpcachedErrors.Add(tmperr);*/
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

		private bool DetermineIfTracEnvironmentExists(out string errorIfFailed)
		{
			this.TracEnvironmentExists = null;

			bool? tracEnvironmentExists = OwnAppsInterop.DoesTracEnvironmentExistForProject(
				this.ApplicationName,
				out errorIfFailed,
				(doesExist, tracUrlOrPossibleError) =>
				{
					//This callback will only happen if the Trac url existed but does not exist anymore
					if (doesExist.HasValue
						&& doesExist.Value == false)
					{//if doesExist == null or TRUE, we just ignore it, if false it means the url does not exist anymore
						UserMessages.ShowWarningMessage("Please re-analyse, the Trac url does not exist anymore: " + tracUrlOrPossibleError);
					}
				});
			if (!tracEnvironmentExists.HasValue)
				return false;

			this.TracEnvironmentExists = tracEnvironmentExists.Value;
			return tracEnvironmentExists.Value;
		}

		private bool DetermineIfLicensingIsImplemented(out string errorIfFailed)
		{
			this.LicensingImplemented = false;
			List<string> tmpcachedErrors = new List<string>();//Cache each error because we might have multiple csproj paths for a solution
			if (this.CsProjectRelativeToSolution == null)
				tmpcachedErrors.Add("CsProjectRelativeToSolutionFilePath is NULL");
			else if (this.GetCsProjAppType() == OwnAppsInterop.ApplicationTypes.DLL)
				tmpcachedErrors.Add("The .csproj file (with the same name as the solution) is of type DLL.");
			else
			{
				string tmperr;
				bool? licensingImplementedInThisProj = OwnAppsInterop.IsLicensingImplemented(this.GetCsProjFullPath(), this.GetCsProjAppType(), out tmperr);
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
			if (MainWindowOfFormCodeBehindRelativePath == null)
			{
				errorIfFailed = "Value of 'MainWindowOfFormCodeBehindRelativePath' is NULL.";
				return false;
			}
			if (this.CsprojApplicationType != OwnAppsInterop.ApplicationTypes.WPF
				&& this.CsprojApplicationType != OwnAppsInterop.ApplicationTypes.Winforms)
			{
				errorIfFailed = "How would we check if Console application have AboutWindow implemented?";
				return false;
			}

			this.AboutBoxImplemented = false;
			//First check if AboutWindow2 is referenced before even checking that it's called from Main window/form
			Dictionary<string, string> includeRelpathsAndXmltagnames;
			if (!OwnAppsInterop.GetAllIncludedSourceFilesInCsproj(this.GetCsProjFullPath(), out includeRelpathsAndXmltagnames, out errorIfFailed))
				return false;

			string aboutwindowPage = @"..\..\SharedClasses\AboutWindow2.xaml";
			string aboutwindowCodebehind = @"..\..\SharedClasses\AboutWindow2.xaml.cs";
			if (!includeRelpathsAndXmltagnames.ContainsKey(aboutwindowPage)
				|| !includeRelpathsAndXmltagnames.ContainsKey(aboutwindowCodebehind)
				|| !includeRelpathsAndXmltagnames[aboutwindowPage].Equals("Page", StringComparison.InvariantCultureIgnoreCase)
				|| !includeRelpathsAndXmltagnames[aboutwindowCodebehind].Equals("Compile", StringComparison.InvariantCultureIgnoreCase))
			{
				errorIfFailed = "Cannot find <Page Include=\"AboutWindow2.xaml\" or <Compile Include=\"AboutWindow2.xaml.cs\" in the .csproj file.";
				return false;
			}

			string mainWinOrFormFullpath = Path.Combine(Path.GetDirectoryName(GetCsProjFullPath()), MainWindowOfFormCodeBehindRelativePath);
			string expectedStartOfBlock = "AboutWindow2.ShowAboutWindow(";
			string mainSourceCode_removedComments =
				OwnAppsInterop.ExtractMethodBlockFromSourcecodeFile(mainWinOrFormFullpath, expectedStartOfBlock, delegate { return null; }, out errorIfFailed);

			if (mainSourceCode_removedComments == null)
				return false;//errorIfFailed should already be set
			if (mainSourceCode_removedComments == "")//We did not find the block
			{
				this.AboutBoxImplemented = false;
				string mainWinOfOrForm =
					this.CsprojApplicationType == OwnAppsInterop.ApplicationTypes.WPF
					? "MainWindow.xaml.cs"
					: " MainForm.cs";
				errorIfFailed = null;//"AboutWindow2 is not implemented inside the " + mainWinOfOrForm + ".";
				return true;
			}
			else
			{
				this.AboutBoxImplemented = false;
				Dictionary<string, string> keyValuePairsToFind = new Dictionary<string, string>()
				{//If the VALUE is NULL, we basically just try to find the KEY because the VALUE might not be CONSTANT
					{ "Author", "Francois Hill" },
					{ "Icon(s) obtained from", null }
				};
				foreach (string key in keyValuePairsToFind.Keys)
				{
					string val = keyValuePairsToFind[key];
					if (val == null)
					{
						if (mainSourceCode_removedComments.StringIndexOfIgnoreInsideStringOrChar(
							//We do not include the closing " for new DisplayItem("...") as we allow anything after this
							string.Format("new DisplayItem(\"{0}", key), 0, StringComparison.InvariantCultureIgnoreCase)
							== -1)
						{
							errorIfFailed = string.Format("Unable to find the KEY = \"{0}\" for ItemsToDisplay in AboutWindow2.", key);
							return true;
						}
					}
					else// (val != null)
					{
						if (mainSourceCode_removedComments.StringIndexOfIgnoreInsideStringOrChar(
							string.Format("new DisplayItem(\"{0}\", \"{1}\")", key, val), 0, StringComparison.InvariantCultureIgnoreCase)
							== -1)
						{
							errorIfFailed = string.Format("Unable to find the KEY = \"{0}\" AND VALUE = \"{1}\" for ItemsToDisplay in AboutWindow2.", key, val);
							return true;
						}
					}
				}

				this.AboutBoxImplemented = true;
				errorIfFailed = null;
				return true;
			}
		}

		private bool DetermineIfPoliciesArePartOfNsisSetup(out string errorIfFailed)
		{
			this.PoliciesImplemented = false;
			List<string> tmpcachedErrors = new List<string>();//Cache each error because we might have multiple csproj paths for a solution
			if (this.CsProjectRelativeToSolution == null)
				tmpcachedErrors.Add("CsProjectRelativeToSolutionFilePath is NULL");
			else if (this.GetCsProjAppType() == OwnAppsInterop.ApplicationTypes.DLL)
				tmpcachedErrors.Add("The .csproj file (with the same name as the solution) is of type DLL.");
			else
			{
				string tmperr;

				bool? policiesImplementedInThisProj = OwnAppsInterop.ArePoliciesImplemented(this.GetCsProjFullPath(), this.GetCsProjAppType(), out tmperr);
				if (policiesImplementedInThisProj.HasValue)
				{
					this.LicensingImplemented = policiesImplementedInThisProj;
				}
				else
					tmpcachedErrors.Add(tmperr);
			}
			if (this.PoliciesImplemented == null)
			{
				errorIfFailed = "Unable to determine PoliciesImplemented for application '" + this.ApplicationName + "': "
					+ string.Join("|", tmpcachedErrors);
				return false;
			}
			errorIfFailed = null;
			return true;
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

		public void ExploreToSolutionFullpath()
		{
			Process.Start("explorer", "/select,\"" + this.SolutionFilePath + "\"");
		}

		public void ExploreToCsprojFullPath()
		{
			Process.Start("explorer", "/select,\"" + this.GetCsProjFullPath() + "\"");
		}

		public void CopyCsprojFileFullPathToClipboard()
		{
			Clipboard.SetText(this.GetCsProjFullPath());
		}

		public event PropertyChangedEventHandler PropertyChanged = delegate { };
		public void OnPropertyChanged(params string[] propertyNames) { foreach (var pn in propertyNames) PropertyChanged(this, new PropertyChangedEventArgs(pn)); }
	}
}
