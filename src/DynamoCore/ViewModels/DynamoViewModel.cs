﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

using Autodesk.DesignScript.Interfaces;

using Dynamo.Interfaces;
using Dynamo.Models;
using Dynamo.Selection;
using Dynamo.UI;
using Dynamo.Services;
using Dynamo.UpdateManager;

using DynamoUnits;

using DynCmd = Dynamo.ViewModels.DynamoViewModel;
using System.Reflection;

namespace Dynamo.ViewModels
{
    public partial class DynamoViewModel : ViewModelBase, IWatchViewModel
    {
        #region properties

        public readonly DynamoModel model;

        private Point transformOrigin;
        private bool runEnabled = true;
        protected bool canRunDynamically = true;
        protected bool debug = false;
        private bool canNavigateBackground = false;
        private bool showStartPage = false;
        private bool watchEscapeIsDown = false;

        /// <summary>
        /// An observable collection of workspace view models which tracks the model
        /// </summary>
        private ObservableCollection<WorkspaceViewModel> workspaces = new ObservableCollection<WorkspaceViewModel>();
        public ObservableCollection<WorkspaceViewModel> Workspaces
        {
            get { return workspaces; }
            set
            {
                workspaces = value;
                RaisePropertyChanged("Workspaces");
            }
        }

        public DynamoModel Model
        {
            get { return model; }
        }

        public Point TransformOrigin
        {
            get { return transformOrigin; }
            set
            {
                transformOrigin = value;
                RaisePropertyChanged("TransformOrigin");
            }
        }

        public bool RunEnabled
        {
            get { return model.RunEnabled; }
            set
            {
                model.RunEnabled = value;
            }
        }

        public virtual bool CanRunDynamically
        {
            get
            {
                //we don't want to be able to run
                //dynamically if we're in debug mode
                return !debug;
            }
            set
            {
                canRunDynamically = value;
                RaisePropertyChanged("CanRunDynamically");
            }
        }

        public virtual bool DynamicRunEnabled
        {
            get
            {
                return model.DynamicRunEnabled; //selecting debug now toggles this on/off
            }
            set
            {
                model.DynamicRunEnabled = value;
                RaisePropertyChanged("DynamicRunEnabled");
            }
        }

        public bool ViewingHomespace
        {
            get { return model.CurrentWorkspace == model.HomeSpace; }
        }

        public bool IsAbleToGoHome { get; set; }

        public WorkspaceModel CurrentSpace
        {
            get { return model.CurrentWorkspace; }
        }

        public double WorkspaceActualHeight { get; set; }
        public double WorkspaceActualWidth { get; set; }

        public void WorkspaceActualSize(double width, double height)
        {
            WorkspaceActualWidth = width;
            WorkspaceActualHeight = height;
            RaisePropertyChanged("WorkspaceActualSize");
        }

        /// <summary>
        /// The index in the collection of workspaces of the current workspace.
        /// This property is bound to the SelectedIndex property in the workspaces tab control
        /// </summary>
        public int CurrentWorkspaceIndex
        {
            get
            {
                var index = model.Workspaces.IndexOf(model.CurrentWorkspace);
                return index;
            }
            set
            {
                if (model.Workspaces.IndexOf(model.CurrentWorkspace) != value)
                    this.ExecuteCommand(new SwitchTabCommand(value));
            }
        }

        /// <summary>
        /// Get the workspace view model whose workspace model is the model's current workspace
        /// </summary>
        public WorkspaceViewModel CurrentSpaceViewModel
        {
            get
            {
                return Workspaces.First(x => x.Model == model.CurrentWorkspace);
            }
        }

        internal AutomationSettings Automation { get { return this.automationSettings; } }

        internal string editName = "";
        public string EditName
        {
            get { return editName; }
            set
            {
                editName = value;
                RaisePropertyChanged("EditName");
            }
        }

        public bool ShowStartPage
        {
            get { return this.showStartPage; }

            set
            {
                // If the caller attempts to show the start page, but we are 
                // currently in playback mode, then this will not be allowed
                // (i.e. the start page will never be shown during a playback).
                // 
                if ((value == true) && (null != automationSettings))
                {
                    if (automationSettings.IsInPlaybackMode)
                        return;
                }

                showStartPage = value;
                RaisePropertyChanged("ShowStartPage");
                if (DisplayStartPageCommand != null)
                    DisplayStartPageCommand.RaiseCanExecuteChanged();
            }
        }

        public bool WatchEscapeIsDown
        {
            get { return watchEscapeIsDown; }
            set
            {
                watchEscapeIsDown = value;
                RaisePropertyChanged("WatchEscapeIsDown");
                RaisePropertyChanged("ShouldBeHitTestVisible");
                RaisePropertyChanged("WatchPreviewHitTest");
            }
        }

        public bool WatchPreviewHitTest
        {
            // This is directly opposite of "ShouldBeHitTestVisible".
            get { return (WatchEscapeIsDown || CanNavigateBackground); }
        }

        public bool ShouldBeHitTestVisible
        {
            // This is directly opposite of "WatchPreviewHitTest".
            get { return (!WatchEscapeIsDown && (!CanNavigateBackground)); }
        }

        public bool IsHomeSpace
        {
            get { return model.CurrentWorkspace == model.HomeSpace; }
        }

        public bool FullscreenWatchShowing
        {
            get { return model.PreferenceSettings.FullscreenWatchShowing; }
            set
            {
                model.PreferenceSettings.FullscreenWatchShowing = value;
                RaisePropertyChanged("FullscreenWatchShowing");

                if (!FullscreenWatchShowing && canNavigateBackground)
                    CanNavigateBackground = false;

                if(value)
                    this.model.OnRequestsRedraw(this, EventArgs.Empty);
            }
        }

        public bool CanNavigateBackground
        {
            get { return canNavigateBackground; }
            set
            {
                canNavigateBackground = value;
                RaisePropertyChanged("CanNavigateBackground");
                RaisePropertyChanged("WatchBackgroundHitTest");

                int workspace_index = CurrentWorkspaceIndex;

                WorkspaceViewModel view_model = Workspaces[workspace_index];

                WatchEscapeIsDown = value;
            }
        }

        public string LogText
        {
            get { return model.Logger.LogText; }
        }

        public int ConsoleHeight
        {
            get
            {
                return model.PreferenceSettings.ConsoleHeight;
            }
            set
            {
                model.PreferenceSettings.ConsoleHeight = value;

                RaisePropertyChanged("ConsoleHeight");
            }
        }

        public bool IsShowingConnectors
        {
            get
            {
                return model.IsShowingConnectors;
            }
            set
            {
                model.IsShowingConnectors = value;

                RaisePropertyChanged("IsShowingConnectors");
            }
        }

        public bool IsMouseDown { get; set; }
        public bool IsPanning { get { return CurrentSpaceViewModel.IsPanning; } }
        public bool IsOrbiting { get { return CurrentSpaceViewModel.IsOrbiting; } }

        public ConnectorType ConnectorType
        {
            get
            {
                return model.ConnectorType;
            }
            set
            {
                model.ConnectorType = value;

                RaisePropertyChanged("ConnectorType");
            }
        }

        public bool IsUsageReportingApproved
        {
            get
            {
                return UsageReportingManager.Instance.IsUsageReportingApproved;
            }
        }

        private ObservableCollection<string> recentFiles =
            new ObservableCollection<string>();
        public ObservableCollection<string> RecentFiles
        {
            get { return recentFiles; }
            set
            {
                recentFiles = value;
                RaisePropertyChanged("RecentFiles");
            }
        }

        public string AlternateContextGeometryDisplayText
        {
            get
            {
                return string.Format("Show Geometry in {0}",
                                     this.VisualizationManager.AlternateContextName);
            }
        }

        public bool WatchIsResizable { get; set; }
        public bool IsBackgroundPreview { get { return true; } }

        public string Version
        {
            get { return model.Version; }
        }

        public bool IsUpdateAvailable
        {
            get
            {
                var um = UpdateManager.UpdateManager.Instance;
                if (um.ForceUpdate)
                {
                    return true;
                }
                return um.AvailableVersion > um.ProductVersion;
            }
        }

        public string LicenseFile
        {
            get
            {
                string executingAssemblyPathName = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string rootModuleDirectory = System.IO.Path.GetDirectoryName(executingAssemblyPathName);
                var licensePath = System.IO.Path.Combine(rootModuleDirectory, "License.rtf");
                return licensePath;
            }
        }

        public int MaxTesselationDivisions
        {
            get { return VisualizationManager.MaxTesselationDivisions; }
            set
            {
               VisualizationManager.MaxTesselationDivisions = value;
                this.model.OnRequestsRedraw(this, EventArgs.Empty);
            }
        }

        public bool VerboseLogging
        {
            get { return model.DebugSettings.VerboseLogging; }
            set
            {
                model.DebugSettings.VerboseLogging = value;
                RaisePropertyChanged("VerboseLogging");
            }
        }

        public bool ShowDebugASTs
        {
            get { return IsDebugBuild && model.DebugSettings.ShowDebugASTs; }
            set
            {
                model.DebugSettings.ShowDebugASTs = value;
                RaisePropertyChanged("ShowDebugASTs");
            }
        }

        internal Dispatcher UIDispatcher { get; set; }

        public IWatchHandler WatchHandler { get; private set; }
        public IVisualizationManager VisualizationManager { get; private set; }
        public SearchViewModel SearchViewModel { get; private set; }
        public PackageManagerClientViewModel PackageManagerClientViewModel { get; private set; }

        #endregion

        public struct StartConfiguration
        {
            public string CommandFilePath { get; set; }
            public IVisualizationManager VisualizationManager { get; set; }
            public IWatchHandler WatchHandler { get; set; }
            public DynamoModel DynamoModel { get; set; }
        }

        public static DynamoViewModel Start()
        {
            return Start(new StartConfiguration());
        }

        public static DynamoViewModel Start(StartConfiguration startConfiguration)
        {
            var model = startConfiguration.DynamoModel ?? DynamoModel.Start();
            var vizManager = startConfiguration.VisualizationManager ?? new VisualizationManager(model);
            var watchHandler = startConfiguration.WatchHandler ?? new DefaultWatchHandler(vizManager, 
                model.PreferenceSettings);
            
            return new DynamoViewModel(model, watchHandler, vizManager, startConfiguration.CommandFilePath);
        }

        protected DynamoViewModel(DynamoModel dynamoModel, IWatchHandler watchHandler,
            IVisualizationManager vizManager, string commandFilePath)
        {
            // initialize core data structures
            this.model = dynamoModel;
            this.WatchHandler = watchHandler;
            this.VisualizationManager = vizManager;
            this.PackageManagerClientViewModel = new PackageManagerClientViewModel(this, model.PackageManagerClient);
            this.SearchViewModel = new SearchViewModel(this, model.SearchModel);

            // Start page should not show up during test mode.
            this.ShowStartPage = !DynamoModel.IsTestMode;

            //add the initial workspace and register for future 
            //updates to the workspaces collection
            workspaces.Add(new WorkspaceViewModel(model.HomeSpace, this));
            model.Workspaces.CollectionChanged += Workspaces_CollectionChanged;

            SubscribeModelChangedHandlers();
            SubscribeUpdateManagerHandlers();
       
            InitializeAutomationSettings(commandFilePath);

            InitializeDelegateCommands();

            SubscribeLoggerHandlers();

            DynamoSelection.Instance.Selection.CollectionChanged += SelectionOnCollectionChanged;

            InitializeRecentFiles();

            UsageReportingManager.Instance.PropertyChanged += CollectInfoManager_PropertyChanged;

            WatchIsResizable = false;

            SubscribeDispatcherHandlers();

#if BLOODSTONE
            this.VisualizationManager.RenderComplete += (sender, args) =>
            {
                var action = new Action(() => GetBranchVisualization(null));
                UIDispatcher.BeginInvoke(action);
            };
#endif
        }

        #region Event handler destroy/create

        internal void UnsubscibeAllEvents()
        {
            UnsubscribeDispatcherEvents();
            UnsubscribeModelChangedEvents();
            UnsubscribeUpdateManagerEvents();
            UnsubscribeLoggerEvents();
        }

        private void InitializeRecentFiles()
        {
            this.RecentFiles = new ObservableCollection<string>(model.PreferenceSettings.RecentFiles);
            this.RecentFiles.CollectionChanged += (sender, args) =>
            {
                model.PreferenceSettings.RecentFiles = this.RecentFiles.ToList();
            };
        }

        private void SubscribeLoggerHandlers()
        {
            model.Logger.PropertyChanged += Instance_PropertyChanged;
        }

        private void UnsubscribeLoggerEvents()
        {
            model.Logger.PropertyChanged -= Instance_PropertyChanged;
        }

        private void SubscribeUpdateManagerHandlers()
        {
            UpdateManager.UpdateManager.Instance.UpdateDownloaded += Instance_UpdateDownloaded;
            UpdateManager.UpdateManager.Instance.ShutdownRequested += updateManager_ShutdownRequested;
        }

        private void UnsubscribeUpdateManagerEvents()
        {
            UpdateManager.UpdateManager.Instance.UpdateDownloaded -= Instance_UpdateDownloaded;
            UpdateManager.UpdateManager.Instance.ShutdownRequested -= updateManager_ShutdownRequested;
        }

        private void SubscribeModelChangedHandlers()
        {
            model.WorkspaceSaved += ModelWorkspaceSaved;
            model.PropertyChanged += _model_PropertyChanged;
            model.WorkspaceCleared += ModelWorkspaceCleared;
            model.RequestCancelActiveStateForNode += this.CancelActiveState;
        }

        private void UnsubscribeModelChangedEvents()
        {
            model.PropertyChanged -= _model_PropertyChanged;
            model.WorkspaceCleared -= ModelWorkspaceCleared;
            model.RequestCancelActiveStateForNode -= this.CancelActiveState;
        }

        private void SubscribeDispatcherHandlers()
        {
            this.Model.RequestDispatcherBeginInvoke += TryDispatcherBeginInvoke;
            this.Model.RequestDispatcherInvoke += TryDispatcherInvoke;
        }

        private void UnsubscribeDispatcherEvents()
        {
            this.Model.RequestDispatcherBeginInvoke -= TryDispatcherBeginInvoke;
            this.Model.RequestDispatcherInvoke -= TryDispatcherInvoke;
        }

        #endregion

        private void InitializeAutomationSettings(string commandFilePath)
        {
            if (String.IsNullOrEmpty(commandFilePath) || !File.Exists(commandFilePath))
                commandFilePath = null;

            // Instantiate an AutomationSettings to handle record/playback.
            automationSettings = new AutomationSettings(this, commandFilePath);
        }

        private void TryDispatcherBeginInvoke(Action action)
        {
            if (this.UIDispatcher != null)
            {
                UIDispatcher.BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        private void TryDispatcherInvoke(Action action)
        {
            if (this.UIDispatcher != null)
            {
                UIDispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }

        private void ModelWorkspaceSaved(WorkspaceModel model)
        {
            this.AddToRecentFiles(model.FileName);
        }

        private void ModelWorkspaceCleared(object sender, EventArgs e)
        {
            this.UndoCommand.RaiseCanExecuteChanged();
            this.RedoCommand.RaiseCanExecuteChanged();

            // Reset workspace state
            this.CurrentSpaceViewModel.CancelActiveState();
        }

        public void RequestRedraw()
        {
            this.model.OnRequestsRedraw(this, EventArgs.Empty);
        }

        public void RequestClearDrawables()
        {
            //VisualizationManager.ClearRenderables();
        }

        public void CancelRunCmd(object parameter)
        {
            var command = new DynamoViewModel.RunCancelCommand(false, true);
            this.ExecuteCommand(command);
        }

        internal bool CanCancelRunCmd(object parameter)
        {
            return true;
        }

        public void ReturnFocusToSearch()
        {
            this.SearchViewModel.OnRequestReturnFocusToSearch(null, EventArgs.Empty);
        }

        internal void RunExprCmd(object parameters)
        {
            bool displayErrors = Convert.ToBoolean(parameters);
            var command = new DynamoViewModel.RunCancelCommand(displayErrors, false);
            this.ExecuteCommand(command);
        }

        internal bool CanRunExprCmd(object parameters)
        {
            return true;
        }

        internal void ForceRunExprCmd(object parameters)
        {
            bool displayErrors = Convert.ToBoolean(parameters);
            var command = new DynamoViewModel.ForceRunCancelCommand(displayErrors, false);
            this.ExecuteCommand(command);
        }

        internal void MutateTestCmd(object parameters)
        {
            var command = new DynamoViewModel.MutateTestCommand();
            this.ExecuteCommand(command);
        }

        public void DisplayFunction(object parameters)
        {
            Model.CustomNodeManager.GetFunctionDefinition((Guid)parameters);
        }

        internal bool CanDisplayFunction(object parameters)
        {
            return Model.CustomNodes.Any(x => x.Value == (Guid)parameters);
        }

        public static void ReportABug(object parameter)
        {
            Process.Start(Configurations.GitHubBugReportingLink);
        }

        internal static void DownloadDynamo()
        {
            Process.Start(Configurations.DynamoDownloadLink);
        }

        internal bool CanReportABug(object parameter)
        {
            return true;
        }

        /// <summary>
        /// Clear the UI log.
        /// </summary>
        public void ClearLog(object parameter)
        {
            Model.Logger.ClearLog();
        }

        internal bool CanClearLog(object parameter)
        {
            return true;
        }

        void Instance_UpdateDownloaded(object sender, UpdateManager.UpdateDownloadedEventArgs e)
        {
            RaisePropertyChanged("Version");
            RaisePropertyChanged("IsUpdateAvailable");
        }

        void updateManager_ShutdownRequested(IUpdateManager updateManager)
        {
            if (SetAllowCancelAndRequestUIClose(true))
                return;

            model.ShutDown(true);
            UpdateManager.UpdateManager.Instance.HostApplicationBeginQuit();
        }

        void CollectInfoManager_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "CollectInfoOption":
                    RaisePropertyChanged("CollectInfoOption");
                    break;
            }
        }

        private void SelectionOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            PublishSelectedNodesCommand.RaiseCanExecuteChanged();
            AlignSelectedCommand.RaiseCanExecuteChanged();
            DeleteCommand.RaiseCanExecuteChanged();
        }

        void Controller_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "IsUILocked":
                    RaisePropertyChanged("IsUILocked");
                    break;
            }
        }

        void Instance_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {

            switch (e.PropertyName)
            {
                case "LogText":
                    RaisePropertyChanged("LogText");
                    RaisePropertyChanged("WarningText");
                    break;
            }

        }

        void _model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentWorkspace")
            {
                IsAbleToGoHome = model.CurrentWorkspace != model.HomeSpace;
                RaisePropertyChanged("IsAbleToGoHome");
                RaisePropertyChanged("CurrentSpace");
                RaisePropertyChanged("BackgroundColor");
                RaisePropertyChanged("CurrentWorkspaceIndex");
                RaisePropertyChanged("ViewingHomespace");
                if (this.PublishCurrentWorkspaceCommand != null)
                    this.PublishCurrentWorkspaceCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged("IsHomeSpace");
                RaisePropertyChanged("IsPanning");
                RaisePropertyChanged("IsOrbiting");
            }
            else if (e.PropertyName == "RunEnabled")
                RaisePropertyChanged("RunEnabled");
        }

        internal bool CanWriteToLog(object parameters)
        {
            if (model.Logger != null)
            {
                return true;
            }

            return false;
        }

        internal bool CanCopy(object parameters)
        {
            if (DynamoSelection.Instance.Selection.Count == 0)
            {
                return false;
            }
            return true;
        }

        internal bool CanPaste(object parameters)
        {
            if (model.ClipBoard.Count == 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// After command framework is implemented, this method should now be only 
        /// called from a menu item (i.e. Ctrl + W). It should not be used as a way
        /// for any other code paths to create a note programmatically. For that we
        /// now have AddNoteInternal which takes in more configurable arguments.
        /// </summary>
        /// <param name="parameters">This is not used and should always be null,
        /// otherwise an ArgumentException will be thrown.</param>
        /// 
        public void AddNote(object parameters)
        {
            if (null != parameters) // See above for details of this exception.
            {
                var message = "Internal error, argument must be null";
                throw new ArgumentException(message, "parameters");
            }

            var command = new DynCmd.CreateNoteCommand(Guid.NewGuid(), null, 0, 0, true);
            this.ExecuteCommand(command);
        }

        internal bool CanAddNote(object parameters)
        {
            return true;
        }

        /// <summary>
        /// Responds to change in the model's workspaces collection, creating or deleting workspace model views.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Workspaces_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var item in e.NewItems)
                        workspaces.Add(new WorkspaceViewModel(item as WorkspaceModel, this));
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var item in e.OldItems)
                        workspaces.Remove(workspaces.ToList().First(x => x.Model == item));
                    break;
            }

            RaisePropertyChanged("Workspaces");
        }

        internal void AddToRecentFiles(string path)
        {
            if (path == null) return;

            if (RecentFiles.Contains(path))
            {
                RecentFiles.Remove(path);
            }

            RecentFiles.Insert(0, path);

            if (RecentFiles.Count > Model.PreferenceSettings.MaxNumRecentFiles)
            {
                RecentFiles = new ObservableCollection<string>(RecentFiles.Take(Model.PreferenceSettings.MaxNumRecentFiles));
            }
        }

        public FileDialog GetSaveDialog(WorkspaceModel workspace)
        {
            FileDialog fileDialog = new SaveFileDialog
            {
                AddExtension = true,
            };

            string ext, fltr;
            if (workspace == model.HomeSpace)
            {
                ext = ".dyn";
                fltr = "Dynamo Workspace (*.dyn)|*.dyn";
            }
            else
            {
                ext = ".dyf";
                fltr = "Dynamo Custom Node (*.dyf)|*.dyf";
            }
            fltr += "|All files (*.*)|*.*";

            fileDialog.FileName = workspace.Name + ext;
            fileDialog.AddExtension = true;
            fileDialog.DefaultExt = ext;
            fileDialog.Filter = fltr;

            return fileDialog;
        }

        /// <summary>
        /// Open a definition or workspace.
        /// </summary>
        /// <param name="parameters">The path the the file.</param>
        private void Open(object parameters)
        {
            // try catch for exceptions thrown while opening files, say from a future version, 
            // that can't be handled reliably
            try
            {
                string xmlFilePath = parameters as string;
                ExecuteCommand(new OpenFileCommand(xmlFilePath));
            }
            catch (Exception e)
            {
                model.Logger.Log("Error opening file:" + e.Message);
                model.Logger.Log(e);
                return;
            }            
            this.ShowStartPage = false; // Hide start page if there's one.
        }

        private bool CanOpen(object parameters)
        {
            var filePath = parameters as string;
            return ((!string.IsNullOrEmpty(filePath)) && File.Exists(filePath));
        }

        /// <summary>
        /// Present the open dialogue and open the workspace that is selected.
        /// </summary>
        /// <param name="parameter"></param>
        private void ShowOpenDialogAndOpenResult(object parameter)
        {
            if (Model.HomeSpace.HasUnsavedChanges)
            {
                if (!AskUserToSaveWorkspaceOrCancel(Model.HomeSpace))
                    return;
            }

            FileDialog _fileDialog = new OpenFileDialog()
            {
                Filter = "Dynamo Definitions (*.dyn; *.dyf)|*.dyn;*.dyf|All files (*.*)|*.*",
                Title = "Open Dynamo Definition..."
            };

            // if you've got the current space path, use it as the inital dir
            if (!string.IsNullOrEmpty(Model.CurrentWorkspace.FileName))
            {
                var fi = new FileInfo(Model.CurrentWorkspace.FileName);
                _fileDialog.InitialDirectory = fi.DirectoryName;
            }
            else // use the samples directory, if it exists
            {
                Assembly dynamoAssembly = Assembly.GetExecutingAssembly();
                string location = Path.GetDirectoryName(dynamoAssembly.Location);
                string path = Path.Combine(location, "samples");

                if (Directory.Exists(path))
                    _fileDialog.InitialDirectory = path;
            }

            if (_fileDialog.ShowDialog() == DialogResult.OK)
            {
                if (CanOpen(_fileDialog.FileName))
                    Open(_fileDialog.FileName);
            }
        }

        private bool CanShowOpenDialogAndOpenResultCommand(object parameter)
        {
            return true;
        }

        private void OpenRecent(object path)
        {
            this.Open(path as string);
        }

        private bool CanOpenRecent(object path)
        {
            return true;
        }

        /// <summary>
        /// Attempts to save an the current workspace.
        /// Assumes that workspace has already been saved.
        /// </summary>
        private void Save(object parameter)
        {
            if (!String.IsNullOrEmpty(Model.CurrentWorkspace.FileName))
                SaveAs(Model.CurrentWorkspace.FileName);
        }

        private bool CanSave(object parameter)
        {
            return true;
        }

        /// <summary>
        /// Save the current workspace.
        /// </summary>
        /// <param name="parameters">The file path.</param>
        private void SaveAs(object parameters)
        {
            var filePath = parameters as string;
            if (string.IsNullOrEmpty(filePath))
                return;

            var fi = new FileInfo(filePath);
            SaveAs(fi.FullName);
        }

        internal bool CanSaveAs(object parameters)
        {
            return (parameters != null);
        }

        /// <summary>
        /// Save the current workspace to a specific file path, if the path is null 
        /// or empty, does nothing. If successful, the CurrentWorkspace.FileName
        /// field is updated as a side effect.
        /// </summary>
        /// <param name="path">The path to save to</param>
        internal void SaveAs(string path)
        {
            Model.CurrentWorkspace.SaveAs(path);
        }

        public virtual bool RunInDebug
        {

            get { return debug; }
            set
            {
                debug = value;

                //toggle off dynamic run
                CanRunDynamically = !debug;

                if (debug)
                    DynamicRunEnabled = false;

                RaisePropertyChanged("RunInDebug");
            }

        }

        /// <summary>
        ///     Attempts to save a given workspace.  Shows a save as dialog if the 
        ///     workspace does not already have a path associated with it
        /// </summary>
        /// <param name="workspace">The workspace for which to show the dialog</param>
        internal void ShowSaveDialogIfNeededAndSave(WorkspaceModel workspace)
        {
            // crash sould always allow save as
            if (workspace.FileName != String.Empty && !DynamoModel.IsCrashing)
            {
                workspace.Save();
            }
            else
            {
                var fd = this.GetSaveDialog(workspace);
                if (fd.ShowDialog() == DialogResult.OK)
                {
                    workspace.SaveAs(fd.FileName);
                }
            }
        }

        public bool exitInvoked = false;

        internal bool CanVisibilityBeToggled(object parameters)
        {
            return true;
        }

        internal bool CanUpstreamVisibilityBeToggled(object parameters)
        {
            return true;
        }

        private void PublishCurrentWorkspace(object parameters)
        {
            PackageManagerClientViewModel.PublishCurrentWorkspace();
        }

        private bool CanPublishCurrentWorkspace(object parameters)
        {
            return PackageManagerClientViewModel.CanPublishCurrentWorkspace();
        }

        private void PublishSelectedNodes(object parameters)
        {
            PackageManagerClientViewModel.PublishSelectedNode();
        }

        private bool CanPublishSelectedNodes(object parameters)
        {
            return PackageManagerClientViewModel.CanPublishSelectedNode(parameters);
        }

        private void ShowPackageManagerSearch(object parameters)
        {
            OnRequestPackageManagerSearchDialog(this, EventArgs.Empty);
        }

        private bool CanShowPackageManagerSearch(object parameters)
        {
            return true;
        }

        private void ShowInstalledPackages(object parameters)
        {
            OnRequestManagePackagesDialog(this, EventArgs.Empty);
        }

        private bool CanShowInstalledPackages(object parameters)
        {
            return true;
        }

        /// <summary>
        ///     Change the currently visible workspace to a custom node's workspace
        /// </summary>
        /// <param name="symbol">The function definition for the custom node workspace to be viewed</param>
        internal void FocusCustomNodeWorkspace(CustomNodeDefinition symbol)
        {
            if (symbol == null)
            {
                throw new Exception("There is a null function definition for this node.");
            }

            if (model.CurrentWorkspace is CustomNodeWorkspaceModel)
            {
                var customNodeWorkspace = model.CurrentWorkspace as CustomNodeWorkspaceModel;
                if (customNodeWorkspace.CustomNodeDefinition.FunctionId
                    == symbol.WorkspaceModel.CustomNodeDefinition.FunctionId)
                {
                    return;
                }
            }

            var newWs = symbol.WorkspaceModel;

            if (!this.model.Workspaces.Contains(newWs))
                this.model.Workspaces.Add(newWs);

            CurrentSpaceViewModel.CancelActiveState();

            model.CurrentWorkspace = newWs;

            //set the zoom and offsets events
            var vm = this.Model.Workspaces.First(x => x == newWs);
            vm.OnCurrentOffsetChanged(this, new PointEventArgs(new Point(newWs.X, newWs.Y)));
            vm.OnZoomChanged(this, new ZoomEventArgs(newWs.Zoom));
        }

        internal void ShowElement(NodeModel e)
        {
            if (DynamicRunEnabled)
                return;

            if (!model.Nodes.Contains(e))
            {
                if (model.HomeSpace != null && model.HomeSpace.Nodes.Contains(e))
                {
                    //Show the homespace
                    model.ViewHomeWorkspace();
                }
                else
                {
                    foreach (CustomNodeDefinition funcDef in 
                        model.CustomNodeManager.GetLoadedDefinitions())
                    {
                        if (funcDef.WorkspaceModel.Nodes.Contains(e))
                        {
                            FocusCustomNodeWorkspace(funcDef);
                            break;
                        }
                    }
                }
            }

            this.CurrentSpaceViewModel.OnRequestCenterViewOnElement(this, new ModelEventArgs(e));
        }

        private void CancelActiveState(NodeModel node)
        {
            WorkspaceViewModel wvm = this.CurrentSpaceViewModel;

            if (wvm.IsConnecting && (node == wvm.ActiveConnector.ActiveStartPort.Owner))
                wvm.CancelActiveState();
        }

        /// <summary>
        /// Present the new function dialogue and create a custom function.
        /// </summary>
        /// <param name="parameter"></param>
        private void ShowNewFunctionDialogAndMakeFunction(object parameter)
        {
            //trigger the event to request the display
            //of the function name dialogue
            var args = new FunctionNamePromptEventArgs();
            this.Model.OnRequestsFunctionNamePrompt(this, args);

            if (args.Success)
            {
                this.ExecuteCommand(new CreateCustomNodeCommand(Guid.NewGuid(),
                    args.Name, args.Category, args.Description, true));
            }
        }

        private bool CanShowNewFunctionDialogCommand(object parameter)
        {
            return true;
        }

        public void ShowSaveDialogIfNeededAndSaveResult(object parameter)
        {
            var vm = this;

            if (string.IsNullOrEmpty(vm.Model.CurrentWorkspace.FileName))
            {
                if (CanShowSaveDialogAndSaveResult(parameter))
                    ShowSaveDialogAndSaveResult(parameter);
            }
            else
            {
                if (CanSave(parameter))
                    Save(parameter);
            }
        }

        internal bool CanShowSaveDialogIfNeededAndSaveResultCommand(object parameter)
        {
            return true;
        }

        public void ShowSaveDialogAndSaveResult(object parameter)
        {
            var vm = this;

            FileDialog _fileDialog = vm.GetSaveDialog(vm.Model.CurrentWorkspace);

            //if the xmlPath is not empty set the default directory
            if (!string.IsNullOrEmpty(vm.Model.CurrentWorkspace.FileName))
            {
                var fi = new FileInfo(vm.Model.CurrentWorkspace.FileName);
                _fileDialog.InitialDirectory = fi.DirectoryName;
                _fileDialog.FileName = fi.Name;
            }
            else if (vm.Model.CurrentWorkspace is CustomNodeWorkspaceModel && 
                model.CustomNodeManager.SearchPath.Any())
            {
                _fileDialog.InitialDirectory = model.CustomNodeManager.SearchPath[0];
            }

            if (_fileDialog.ShowDialog() == DialogResult.OK)
            {
                SaveAs(_fileDialog.FileName);
            }
        }

        internal bool CanShowSaveDialogAndSaveResult(object parameter)
        {
            return true;
        }

        public void ToggleCanNavigateBackground(object parameter)
        {
            if (!FullscreenWatchShowing)
                return;

            CanNavigateBackground = !CanNavigateBackground;

            if (CanNavigateBackground)
                InstrumentationLogger.LogAnonymousScreen("Geometry");
            else
                InstrumentationLogger.LogAnonymousScreen("Nodes");


            if (!CanNavigateBackground)
            {
                // Return focus back to Search View (Search Field)
                this.SearchViewModel.OnRequestReturnFocusToSearch(this, new EventArgs());
            }
        }

        internal bool CanToggleCanNavigateBackground(object parameter)
        {
            return true;
        }

        public void ToggleFullscreenWatchShowing(object parameter)
        {
            FullscreenWatchShowing = !FullscreenWatchShowing;
        }

        internal bool CanToggleFullscreenWatchShowing(object parameter)
        {
            return true;
        }

        public void GoToWorkspace(object parameter)
        {
            if (parameter is Guid && model.CustomNodeManager.Contains((Guid)parameter))
            {
                FocusCustomNodeWorkspace(model.CustomNodeManager.GetFunctionDefinition((Guid)parameter));
            }
        }

        internal bool CanGoToWorkspace(object parameter)
        {
            return true;
        }

        public void AlignSelected(object param)
        {
            //this.CurrentSpaceViewModel.AlignSelectedCommand.Execute(param);
            this.CurrentSpaceViewModel.AlignSelectedCommand.Execute(param.ToString());
        }

        internal bool CanAlignSelected(object param)
        {
            return this.CurrentSpaceViewModel.AlignSelectedCommand.CanExecute(param);
        }

        public void DoGraphAutoLayout(object parameter)
        {
            this.CurrentSpaceViewModel.GraphAutoLayoutCommand.Execute(parameter);
        }

        internal bool CanDoGraphAutoLayout(object parameter)
        {
            return true;
        }

        /// <summary>
        /// Resets the offset and the zoom for a view
        /// </summary>
        public void GoHomeView(object parameter)
        {
            model.CurrentWorkspace.Zoom = 1.0;

            var ws = this.Model.Workspaces.First(x => x == model.CurrentWorkspace);
            ws.OnCurrentOffsetChanged(this, new PointEventArgs(new Point(0, 0)));
        }

        internal bool CanGoHomeView(object parameter)
        {
            return true;
        }

        public void SelectAll(object parameter)
        {
            this.CurrentSpaceViewModel.SelectAll(null);
        }

        internal bool CanSelectAll(object parameter)
        {
            return this.CurrentSpaceViewModel.CanSelectAll(null);
        }

        public void MakeNewHomeWorkspace(object parameter)
        {
            if (ClearHomeWorkspaceInternal())
                this.ShowStartPage = false; // Hide start page if there's one.
        }

        internal bool CanMakeNewHomeWorkspace(object parameter)
        {
            return true;
        }

        private void CloseHomeWorkspace(object parameter)
        {
            if (ClearHomeWorkspaceInternal())
            {
                // If after closing the HOME workspace, and there are no other custom 
                // workspaces opened at the time, then we should show the start page.
                this.ShowStartPage = (Model.Workspaces.Count <= 1);
            }
        }

        private bool CanCloseHomeWorkspace(object parameter)
        {
            return true;
        }

        /// <summary>
        /// TODO(Ben): Both "CloseHomeWorkspace" and "MakeNewHomeWorkspace" are 
        /// quite close in terms of functionality, but because their callers 
        /// have different expectations in different scenarios, they remain 
        /// separate now. A new task has been scheduled for them to be unified 
        /// into one consistent way of handling.
        /// 
        ///     http://adsk-oss.myjetbrains.com/youtrack/issue/MAGN-3813
        /// 
        /// </summary>
        /// <returns>Returns true if the home workspace has been saved and 
        /// cleared, or false otherwise.</returns>
        /// 
        private bool ClearHomeWorkspaceInternal()
        {
            // if the workspace is unsaved, prompt to save
            // otherwise overwrite the home workspace with new workspace
            if (!Model.HomeSpace.HasUnsavedChanges || AskUserToSaveWorkspaceOrCancel(this.Model.HomeSpace))
            {
                Model.CurrentWorkspace = this.Model.HomeSpace;

                model.Clear(null);
                return true;
            }

            return false;
        }

        public void Exit(object allowCancel)
        {
            if (SetAllowCancelAndRequestUIClose(allowCancel))
            {
                return;
            }

            model.ShutDown(false);
        }

        private bool SetAllowCancelAndRequestUIClose(object allowCancel)
        {
            bool allowCancelBool = true;
            if (allowCancel != null)
            {
                allowCancelBool = (bool)allowCancel;
            }
            if (!AskUserToSaveWorkspacesOrCancel(allowCancelBool))
                return true;

            exitInvoked = true;

            //request the UI to close its window
            OnRequestClose(this, EventArgs.Empty);
            return false;
        }

        internal bool CanExit(object allowCancel)
        {
            return !exitInvoked;
        }

        /// <summary>
        /// Requests a message box asking the user to save the workspace and allows saving.
        /// </summary>
        /// <param name="workspace">The workspace for which to show the dialog</param>
        /// <returns>False if the user cancels, otherwise true</returns>
        public bool AskUserToSaveWorkspaceOrCancel(WorkspaceModel workspace, bool allowCancel = true)
        {
            var args = new WorkspaceSaveEventArgs(workspace, allowCancel);
            OnRequestUserSaveWorkflow(this, args);
            if (!args.Success)
                return false;
            return true;
        }

        /// <summary>
        ///     Ask the user if they want to save any unsaved changes, return false if the user cancels.
        /// </summary>
        /// <param name="allowCancel">Whether to show cancel button to user. </param>
        /// <returns>Whether the cleanup was completed or cancelled.</returns>
        public bool AskUserToSaveWorkspacesOrCancel(bool allowCancel = true)
        {
            if (null != automationSettings)
            {
                // In an automation run, Dynamo should not be asking user to save 
                // the modified file. Instead it should be shutting down, leaving 
                // behind unsaved changes (if saving is desired, then the save command 
                // should have been recorded for the test case to it can be replayed).
                // 
                if (automationSettings.IsInPlaybackMode)
                    return true; // In playback mode, just exit without saving.
            }

            foreach (var wvm in Workspaces.Where((wvm) => wvm.Model.HasUnsavedChanges))
            {
                //if (!AskUserToSaveWorkspaceOrCancel(wvm.Model, allowCancel))
                //    return false;

                var args = new WorkspaceSaveEventArgs(wvm.Model, allowCancel);
                OnRequestUserSaveWorkflow(this, args);
                if (!args.Success)
                    return false;
            }
            return true;
        }

        internal bool CanAddToSelection(object parameters)
        {
            var node = parameters as NodeModel;
            if (node == null)
            {
                return false;
            }

            return true;
        }

        internal bool CanClear(object parameter)
        {
            return true;
        }

        internal void Delete(object parameters)
        {
            if (null != parameters) // See above for details of this exception.
            {
                var message = "Internal error, argument must be null";
                throw new ArgumentException(message, "parameters");
            }

            var command = new DynCmd.DeleteModelCommand(Guid.Empty);
            this.ExecuteCommand(command);
        }

        internal bool CanDelete(object parameters)
        {
            return DynamoSelection.Instance.Selection.Count > 0;
        }


        public void SaveImage(object parameters)
        {
            OnRequestSaveImage(this, new ImageSaveEventArgs(parameters.ToString()));
        }

        internal bool CanSaveImage(object parameters)
        {
            return true;
        }

        public void ShowSaveImageDialogAndSaveResult(object parameter)
        {
            FileDialog _fileDialog = null;

            if (_fileDialog == null)
            {
                _fileDialog = new SaveFileDialog()
                {
                    AddExtension = true,
                    DefaultExt = ".png",
                    FileName = "Capture.png",
                    Filter = "PNG Image|*.png",
                    Title = "Save your Workbench to an Image",
                };
            }

            // if you've got the current space path, use it as the inital dir
            if (!string.IsNullOrEmpty(model.CurrentWorkspace.FileName))
            {
                var fi = new FileInfo(model.CurrentWorkspace.FileName);
                _fileDialog.InitialDirectory = fi.DirectoryName;
            }

            if (_fileDialog.ShowDialog() == DialogResult.OK)
            {
                if (CanSaveImage(_fileDialog.FileName))
                    SaveImage(_fileDialog.FileName);
            }

        }

        internal bool CanShowSaveImageDialogAndSaveResult(object parameter)
        {
            return true;
        }

        private void Undo(object parameter)
        {
            var command = new UndoRedoCommand(UndoRedoCommand.Operation.Undo);
            this.ExecuteCommand(command);
        }

        private bool CanUndo(object parameter)
        {
            var workspace = model.CurrentWorkspace;
            return ((null == workspace) ? false : workspace.CanUndo);
        }

        private void Redo(object parameter)
        {
            var command = new UndoRedoCommand(UndoRedoCommand.Operation.Redo);
            this.ExecuteCommand(command);
        }

        private bool CanRedo(object parameter)
        {
            var workspace = model.CurrentWorkspace;
            return ((null == workspace) ? false : workspace.CanRedo);
        }

        public void ToggleConsoleShowing(object parameter)
        {
            if (ConsoleHeight == 0)
            {
                ConsoleHeight = 100;
            }
            else
            {
                ConsoleHeight = 0;
            }
        }

        internal bool CanToggleConsoleShowing(object parameter)
        {
            return true;
        }

        public void SelectNeighbors(object parameters)
        {
            List<ISelectable> sels = DynamoSelection.Instance.Selection.ToList<ISelectable>();

            foreach (ISelectable sel in sels)
            {
                if (sel is NodeModel)
                    ((NodeModel)sel).SelectNeighbors();
            }
        }

        internal bool CanSelectNeighbors(object parameters)
        {
            return true;
        }

        public void ShowConnectors(object parameter)
        {
        }

        internal bool CanShowConnectors(object parameter)
        {
            return true;
        }

        public void SetConnectorType(object parameters)
        {
            if (parameters.ToString() == "BEZIER")
            {
                ConnectorType = ConnectorType.BEZIER;
            }
            else
            {
                ConnectorType = ConnectorType.POLYLINE;
            }
        }

        internal bool CanSetConnectorType(object parameters)
        {
            //parameter object will be BEZIER or POLYLINE
            if (string.IsNullOrEmpty(parameters.ToString()))
            {
                return false;
            }
            return true;
        }

        public void GoToWiki(object parameter)
        {
            Process.Start(Dynamo.UI.Configurations.DynamoWikiLink);
        }

        internal bool CanGoToWiki(object parameter)
        {
            return true;
        }

        public void GoToSourceCode(object parameter)
        {
            Process.Start(Dynamo.UI.Configurations.GitHubDynamoLink);
        }

        internal bool CanGoToSourceCode(object parameter)
        {
            return true;
        }

        private void DisplayStartPage(object parameter)
        {
            this.ShowStartPage = true;
        }

        private bool CanDisplayStartPage(object parameter)
        {
            return !this.ShowStartPage;
        }

        internal void Pan(object parameter)
        {
            Debug.WriteLine(string.Format("Offset: {0},{1}, Zoom: {2}", model.CurrentWorkspace.X, model.CurrentWorkspace.Y, model.CurrentWorkspace.Zoom));
            var panType = parameter.ToString();
            double pan = 10;
            var pt = new Point(model.CurrentWorkspace.X, model.CurrentWorkspace.Y);

            switch (panType)
            {
                case "Left":
                    pt.X += pan;
                    break;
                case "Right":
                    pt.X -= pan;
                    break;
                case "Up":
                    pt.Y += pan;
                    break;
                case "Down":
                    pt.Y -= pan;
                    break;
            }
            model.CurrentWorkspace.X = pt.X;
            model.CurrentWorkspace.Y = pt.Y;

            CurrentSpaceViewModel.Model.OnCurrentOffsetChanged(this, new PointEventArgs(pt));
            CurrentSpaceViewModel.ResetFitViewToggleCommand.Execute(parameter);
        }

        private bool CanPan(object parameter)
        {
            return true;
        }

        internal void ZoomIn(object parameter)
        {
            if (CanNavigateBackground)
            {
                var op = ViewOperationEventArgs.Operation.ZoomIn;
                OnRequestViewOperation(new ViewOperationEventArgs(op));
                return;
            }

            CurrentSpaceViewModel.ZoomInInternal();
            ZoomInCommand.RaiseCanExecuteChanged();
        }

        private bool CanZoomIn(object parameter)
        {
            return CurrentSpaceViewModel.CanZoomIn;
        }

        private void ZoomOut(object parameter)
        {
            if (CanNavigateBackground)
            {
                var op = ViewOperationEventArgs.Operation.ZoomOut;
                OnRequestViewOperation(new ViewOperationEventArgs(op));
                return;
            }

            CurrentSpaceViewModel.ZoomOutInternal();
            ZoomOutCommand.RaiseCanExecuteChanged();
        }

        private bool CanZoomOut(object parameter)
        {
            return CurrentSpaceViewModel.CanZoomOut;
        }

        private void FitView(object parameter)
        {
            if (CanNavigateBackground)
            {
                var op = ViewOperationEventArgs.Operation.FitView;
                OnRequestViewOperation(new ViewOperationEventArgs(op));
                return;
            }

            CurrentSpaceViewModel.FitViewInternal();
        }

        private bool CanFitView(object parameter)
        {
            return true;
        }

#if USE_DSENGINE
        public void ImportLibrary(object parameter)
        {
            string fileFilter = "Library Files (*.dll, *.ds)|*.dll;*.ds|"
                              + "Assembly Library Files (*.dll)|*.dll|"
                              + "DesignScript Files (*.ds)|*.ds|"
                              + "All Files (*.*)|*.*";

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = fileFilter;
            openFileDialog.Title = "Import Library";
            openFileDialog.Multiselect = true;
            openFileDialog.RestoreDirectory = true;

            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    model.EngineController.ImportLibrary(file);
                }
            }
        }

        internal bool CanImportLibrary(object parameter)
        {
            return true;
        }
#endif
        internal void TogglePan(object parameter)
        {
            CurrentSpaceViewModel.RequestTogglePanMode();

            // Since panning and orbiting modes are exclusive from one another,
            // turning one on may turn the other off. This is the reason we must
            // raise property change for both at the same time to update visual.
            RaisePropertyChanged("IsPanning");
            RaisePropertyChanged("IsOrbiting");
        }

        internal bool CanTogglePan(object parameter)
        {
            return true;
        }

        internal void ToggleOrbit(object parameter)
        {
            CurrentSpaceViewModel.RequestToggleOrbitMode();

            // Since panning and orbiting modes are exclusive from one another,
            // turning one on may turn the other off. This is the reason we must
            // raise property change for both at the same time to update visual.
            RaisePropertyChanged("IsPanning");
            RaisePropertyChanged("IsOrbiting");
        }

        internal bool CanToggleOrbit(object parameter)
        {
            return true;
        }

        public void Escape(object parameter)
        {
            CurrentSpaceViewModel.CancelActiveState();

            // Since panning and orbiting modes are exclusive from one another,
            // turning one on may turn the other off. This is the reason we must
            // raise property change for both at the same time to update visual.
            RaisePropertyChanged("IsPanning");
            RaisePropertyChanged("IsOrbiting");
        }

        internal bool CanEscape(object parameter)
        {
            return true;
        }

        internal bool CanShowInfoBubble(object parameter)
        {
            return true;
        }

        private void ExportToSTL(object parameter)
        {
            FileDialog _fileDialog = null;

            if (_fileDialog == null)
            {
                _fileDialog = new SaveFileDialog()
                {
                    AddExtension = true,
                    DefaultExt = ".stl",
                    FileName = "model.stl",
                    Filter = "STL Models|*.stl",
                    Title = "Save your model to STL.",
                };
            }

            // if you've got the current space path, use it as the inital dir
            if (!string.IsNullOrEmpty(model.CurrentWorkspace.FileName))
            {
                var fi = new FileInfo(model.CurrentWorkspace.FileName);
                _fileDialog.InitialDirectory = fi.DirectoryName;
            }

            if (_fileDialog.ShowDialog() == DialogResult.OK)
            {
                STLExport.ExportToSTL(this.Model, _fileDialog.FileName, model.HomeSpace.Name);
            }
        }

        internal bool CanExportToSTL(object parameter)
        {
            return true;
        }

        private void SetLengthUnit(object parameter)
        {
            switch (parameter.ToString())
            {
                case "FractionalInch":
                    model.PreferenceSettings.LengthUnit = DynamoLengthUnit.FractionalInch;
                    return;
                case "DecimalInch":
                    model.PreferenceSettings.LengthUnit = DynamoLengthUnit.DecimalInch;
                    return;
                case "FractionalFoot":
                    model.PreferenceSettings.LengthUnit = DynamoLengthUnit.FractionalFoot;
                    return;
                case "DecimalFoot":
                    model.PreferenceSettings.LengthUnit = DynamoLengthUnit.DecimalFoot;
                    return;
                case "Meter":
                    model.PreferenceSettings.LengthUnit = DynamoLengthUnit.Meter;
                    return;
                case "Millimeter":
                    model.PreferenceSettings.LengthUnit = DynamoLengthUnit.Millimeter;
                    return;
                case "Centimeter":
                    model.PreferenceSettings.LengthUnit = DynamoLengthUnit.Centimeter;
                    return;
                default:
                    model.PreferenceSettings.LengthUnit = DynamoLengthUnit.Meter;
                    return;
            }
        }

        internal bool CanSetLengthUnit(object parameter)
        {
            return true;
        }

        private void SetAreaUnit(object parameter)
        {
            switch (parameter.ToString())
            {
                case "SquareInch":
                    model.PreferenceSettings.AreaUnit = DynamoAreaUnit.SquareInch;
                    return;
                case "SquareFoot":
                    model.PreferenceSettings.AreaUnit = DynamoAreaUnit.SquareFoot;
                    return;
                case "SquareMillimeter":
                    model.PreferenceSettings.AreaUnit = DynamoAreaUnit.SquareMillimeter;
                    return;
                case "SquareCentimeter":
                    model.PreferenceSettings.AreaUnit = DynamoAreaUnit.SquareCentimeter;
                    return;
                case "SquareMeter":
                    model.PreferenceSettings.AreaUnit = DynamoAreaUnit.SquareMeter;
                    return;
                default:
                    model.PreferenceSettings.AreaUnit = DynamoAreaUnit.SquareMeter;
                    return;
            }
        }

        internal bool CanSetAreaUnit(object parameter)
        {
            return true;
        }

        private void SetVolumeUnit(object parameter)
        {
            switch (parameter.ToString())
            {
                case "CubicInch":
                    model.PreferenceSettings.VolumeUnit = DynamoVolumeUnit.CubicInch;
                    return;
                case "CubicFoot":
                    model.PreferenceSettings.VolumeUnit = DynamoVolumeUnit.CubicFoot;
                    return;
                case "CubicMillimeter":
                    model.PreferenceSettings.VolumeUnit = DynamoVolumeUnit.CubicMillimeter;
                    return;
                case "CubicCentimeter":
                    model.PreferenceSettings.VolumeUnit = DynamoVolumeUnit.CubicCentimeter;
                    return;
                case "CubicMeter":
                    model.PreferenceSettings.VolumeUnit = DynamoVolumeUnit.CubicMeter;
                    return;
                default:
                    model.PreferenceSettings.VolumeUnit = DynamoVolumeUnit.CubicMeter;
                    return;
            }
        }

        internal bool CanSetVolumeUnit(object parameter)
        {
            return true;
        }

        private bool CanShowAboutWindow(object obj)
        {
            return true;
        }

        private void ShowAboutWindow(object obj)
        {
            OnRequestAboutWindow(this);
        }

        private void SetNumberFormat(object parameter)
        {
            model.PreferenceSettings.NumberFormat = parameter.ToString();
        }

        private bool CanSetNumberFormat(object parameter)
        {
            return true;
        }

        #region IWatchViewModel interface

        public void GetBranchVisualization(object parameters)
        {
#if BLOODSTONE
            var packages = new Dictionary<Guid, IRenderPackage>();
            foreach (var node in this.Model.Nodes)
            {
                if (node.HasRenderPackages == false)
                    continue;

                lock (node.RenderPackagesMutex)
                {
                    var p = node.RenderPackages[0] as Dynamo.DSEngine.RenderPackage;
                    if (p.IsNotEmpty())
                        packages.Add(node.GUID, p);
                }
            }

            if (packages.Any())
            {
                var args = new UpdateBloodstoneVisualEventArgs(packages);
                OnUpdateBloodstoneVisual(this, args);
            }
#else
            var taskId = (long) parameters;
            this.VisualizationManager.AggregateUpstreamRenderPackages(new RenderTag(taskId,null));
#endif
        }

        public bool CanGetBranchVisualization(object parameter)
        {
            if (FullscreenWatchShowing)
            {
                return true;
            }
            return false;
        }

        private bool CanCheckForLatestRender(object obj)
        {
            return true;
        }

        private void CheckForLatestRender(object obj)
        {
            this.VisualizationManager.CheckIfLatestAndUpdate((long)obj);
        }

        #endregion
    }

}
