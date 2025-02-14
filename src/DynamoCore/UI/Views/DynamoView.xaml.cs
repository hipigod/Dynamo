//#define __NO_SAMPLES_MENU

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dynamo.Core;
using Dynamo.Models;
using Dynamo.Nodes;
using Dynamo.Nodes.Prompts;
using Dynamo.PackageManager;
using Dynamo.PackageManager.UI;
using Dynamo.Search;
using Dynamo.Selection;
using Dynamo.UI;
using Dynamo.UI.Views;
using Dynamo.Utilities;
using Dynamo.ViewModels;

using DynamoUtilities;

using String = System.String;
using System.Windows.Data;
using Dynamo.UI.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Dynamo.Services;
using Dynamo.UI.Commands;
using Autodesk.DesignScript.Interfaces;
using System.Collections.Generic;
using Dynamo.Bloodstone;
using System.Collections.Specialized;

namespace Dynamo.Controls
{
    /// <summary>
    ///     Interaction logic for DynamoForm.xaml
    /// </summary>
    public partial class DynamoView : Window
    {
        public const int CANVAS_OFFSET_Y = 0;
        public const int CANVAS_OFFSET_X = 0;

        internal DynamoViewModel dynamoViewModel = null;
        private Stopwatch _timer = null;
        private StartPageViewModel startPage = null;
        private VisualizerHwndHost visualizer = null;

        private int tabSlidingWindowStart, tabSlidingWindowEnd;

        DispatcherTimer _workspaceResizeTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 500), IsEnabled = false };

        public DynamoView(DynamoViewModel dynamoViewModel)
        {
            this.dynamoViewModel = dynamoViewModel;
            this.dynamoViewModel.UIDispatcher = this.Dispatcher;

            this.DataContext = dynamoViewModel;

            tabSlidingWindowStart = tabSlidingWindowEnd = 0;            

            _timer = new Stopwatch();
            _timer.Start();

            InitializeComponent();

            this.Loaded += DynamoView_Loaded;
            this.Unloaded += DynamoView_Unloaded;

            this.SizeChanged += DynamoView_SizeChanged;
            this.LocationChanged += DynamoView_LocationChanged;

            // Check that preference bounds are actually within one
            // of the available monitors.
            if (CheckVirtualScreenSize())
            {
                Left = dynamoViewModel.Model.PreferenceSettings.WindowX;
                Top = dynamoViewModel.Model.PreferenceSettings.WindowY;
                Width = dynamoViewModel.Model.PreferenceSettings.WindowW;
                Height = dynamoViewModel.Model.PreferenceSettings.WindowH;
            }
            else
            {
                Left = 0;
                Top = 0;
                Width = 1024;
                Height = 768;
            }

            _workspaceResizeTimer.Tick += _resizeTimer_Tick;
        }

        bool CheckVirtualScreenSize()
        {
            var w = SystemParameters.VirtualScreenWidth;
            var h = SystemParameters.VirtualScreenHeight;
            var ox = SystemParameters.VirtualScreenLeft;
            var oy = SystemParameters.VirtualScreenTop;

            // TODO: Remove 10 pixel check if others can't reproduce
            // On Ian's Windows 8 setup, when Dynamo is maximized, the origin
            // saves at -8,-8. There doesn't seem to be any documentation on this
            // so we'll put in a 10 pixel check to still allow the window to maximize.
            if (dynamoViewModel.Model.PreferenceSettings.WindowX < ox - 10 ||
                dynamoViewModel.Model.PreferenceSettings.WindowY < oy - 10)
            {
                return false;
            }

            // Check that the window is smaller than the available area.
            if (dynamoViewModel.Model.PreferenceSettings.WindowW > w ||
                dynamoViewModel.Model.PreferenceSettings.WindowH > h)
            {
                return false;
            }

            return true;
        }

        void DynamoView_LocationChanged(object sender, EventArgs e)
        {
            dynamoViewModel.Model.PreferenceSettings.WindowX = Left;
            dynamoViewModel.Model.PreferenceSettings.WindowY = Top;
        }

        void DynamoView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            dynamoViewModel.Model.PreferenceSettings.WindowW = e.NewSize.Width;
            dynamoViewModel.Model.PreferenceSettings.WindowH = e.NewSize.Height;

            Debug.WriteLine("Resizing window to {0}:{1}", e.NewSize.Width, e.NewSize.Height);
        }

        void InitializeShortcutBar()
        {
            ShortcutToolbar shortcutBar = new ShortcutToolbar();
            shortcutBar.Name = "ShortcutToolbar";

            ShortcutBarItem newScriptButton = new ShortcutBarItem();
            newScriptButton.ShortcutToolTip = "New [Ctrl + N]";
            newScriptButton.ShortcutCommand = dynamoViewModel.NewHomeWorkspaceCommand;
            newScriptButton.ShortcutCommandParameter = null;
            newScriptButton.ImgNormalSource = "/DynamoCore;component/UI/Images/new_normal.png";
            newScriptButton.ImgDisabledSource = "/DynamoCore;component/UI/Images/new_disabled.png";
            newScriptButton.ImgHoverSource = "/DynamoCore;component/UI/Images/new_hover.png";

            ShortcutBarItem openScriptButton = new ShortcutBarItem();
            openScriptButton.ShortcutToolTip = "Open [Ctrl + O]";
            openScriptButton.ShortcutCommand = dynamoViewModel.ShowOpenDialogAndOpenResultCommand;
            openScriptButton.ShortcutCommandParameter = null;
            openScriptButton.ImgNormalSource = "/DynamoCore;component/UI/Images/open_normal.png";
            openScriptButton.ImgDisabledSource = "/DynamoCore;component/UI/Images/open_disabled.png";
            openScriptButton.ImgHoverSource = "/DynamoCore;component/UI/Images/open_hover.png";

            ShortcutBarItem saveButton = new ShortcutBarItem();
            saveButton.ShortcutToolTip = "Save [Ctrl + S]";
            saveButton.ShortcutCommand = dynamoViewModel.ShowSaveDialogIfNeededAndSaveResultCommand;
            saveButton.ShortcutCommandParameter = null;
            saveButton.ImgNormalSource = "/DynamoCore;component/UI/Images/save_normal.png";
            saveButton.ImgDisabledSource = "/DynamoCore;component/UI/Images/save_disabled.png";
            saveButton.ImgHoverSource = "/DynamoCore;component/UI/Images/save_hover.png";

            ShortcutBarItem screenShotButton = new ShortcutBarItem();
            screenShotButton.ShortcutToolTip = "Export Workspace As Image";
            screenShotButton.ShortcutCommand = dynamoViewModel.ShowSaveImageDialogAndSaveResultCommand;
            screenShotButton.ShortcutCommandParameter = null;
            screenShotButton.ImgNormalSource = "/DynamoCore;component/UI/Images/screenshot_normal.png";
            screenShotButton.ImgDisabledSource = "/DynamoCore;component/UI/Images/screenshot_disabled.png";
            screenShotButton.ImgHoverSource = "/DynamoCore;component/UI/Images/screenshot_hover.png";

            ShortcutBarItem undoButton = new ShortcutBarItem();
            undoButton.ShortcutToolTip = "Undo [Ctrl + Z]";
            undoButton.ShortcutCommand = dynamoViewModel.UndoCommand;
            undoButton.ShortcutCommandParameter = null;
            undoButton.ImgNormalSource = "/DynamoCore;component/UI/Images/undo_normal.png";
            undoButton.ImgDisabledSource = "/DynamoCore;component/UI/Images/undo_disabled.png";
            undoButton.ImgHoverSource = "/DynamoCore;component/UI/Images/undo_hover.png";

            ShortcutBarItem redoButton = new ShortcutBarItem();
            redoButton.ShortcutToolTip = "Redo [Ctrl + Y]";
            redoButton.ShortcutCommand = dynamoViewModel.RedoCommand;
            redoButton.ShortcutCommandParameter = null;
            redoButton.ImgNormalSource = "/DynamoCore;component/UI/Images/redo_normal.png";
            redoButton.ImgDisabledSource = "/DynamoCore;component/UI/Images/redo_disabled.png";
            redoButton.ImgHoverSource = "/DynamoCore;component/UI/Images/redo_hover.png";

            // PLACEHOLDER FOR FUTURE SHORTCUTS
            //ShortcutBarItem runButton = new ShortcutBarItem();
            //runButton.ShortcutToolTip = "Run [Ctrl + R]";
            ////runButton.ShortcutCommand = viewModel.RunExpressionCommand; // Function implementation in progress
            //runButton.ShortcutCommandParameter = null;
            //runButton.ImgNormalSource = "/DynamoCore;component/UI/Images/run_normal.png";
            //runButton.ImgDisabledSource = "/DynamoCore;component/UI/Images/run_disabled.png";
            //runButton.ImgHoverSource = "/DynamoCore;component/UI/Images/run_hover.png";

            shortcutBar.ShortcutBarItems.Add(newScriptButton);
            shortcutBar.ShortcutBarItems.Add(openScriptButton);
            shortcutBar.ShortcutBarItems.Add(saveButton);
            shortcutBar.ShortcutBarItems.Add(undoButton);
            shortcutBar.ShortcutBarItems.Add(redoButton);

            //shortcutBar.ShortcutBarRightSideItems.Add(updateButton);
            shortcutBar.ShortcutBarRightSideItems.Add(screenShotButton);

            shortcutBarGrid.Children.Add(shortcutBar);
        }

        /// <summary>
        /// This method inserts an instance of "StartPageViewModel" into the 
        /// "startPageItemsControl", results of which displays the Start Page on 
        /// "DynamoView" through the list item's data template. This method also
        /// ensures that there is at most one item in the "startPageItemsControl".
        /// Only when this method is invoked the cost of initializing the start 
        /// page is incurred, when user opts to not display start page at start 
        /// up, then this method will not be called (therefore incurring no cost).
        /// </summary>
        /// 
        private void InitializeStartPage()
        {
            if (DynamoModel.IsTestMode) // No start screen in unit testing.
                return;

            if (this.startPage == null)
            {
                if (startPageItemsControl.Items.Count > 0)
                {
                    var message = "'startPageItemsControl' must be empty";
                    throw new InvalidOperationException(message);
                }

                this.startPage = new StartPageViewModel(this.dynamoViewModel);
                startPageItemsControl.Items.Add(this.startPage);
            }
        }

        void vm_RequestLayoutUpdate(object sender, EventArgs e)
        {
            Dispatcher.Invoke(new Action(UpdateLayout), DispatcherPriority.Render, null);
        }

        void DynamoViewModelRequestViewOperation(ViewOperationEventArgs e)
        {
#if !BLOODSTONE
            if (dynamoViewModel.CanNavigateBackground == false)
                return;

            switch (e.ViewOperation)
            {
                case ViewOperationEventArgs.Operation.FitView:
                    background_preview.View.ZoomExtents();
                    break;

                case ViewOperationEventArgs.Operation.ZoomIn:
                    var camera1 = background_preview.View.CameraController;
                    camera1.Zoom(-0.5 * background_preview.View.ZoomSensitivity);
                    break;

                case ViewOperationEventArgs.Operation.ZoomOut:
                    var camera2 = background_preview.View.CameraController;
                    camera2.Zoom(0.5 * background_preview.View.ZoomSensitivity);
                    break;
            }
#endif
        }

        private void DynamoView_Loaded(object sender, EventArgs e)
        {
            // If first run, Collect Info Prompt will appear
            UsageReportingManager.Instance.CheckIsFirstRun(this);

            this.WorkspaceTabs.SelectedIndex = 0;
            dynamoViewModel = (DataContext as DynamoViewModel);
            dynamoViewModel.Model.RequestLayoutUpdate += vm_RequestLayoutUpdate;
            dynamoViewModel.RequestViewOperation += DynamoViewModelRequestViewOperation;
            dynamoViewModel.PostUiActivationCommand.Execute(null);
            dynamoViewModel.PropertyChanged += OnViewModelPropertyChanged;

            _timer.Stop();
            dynamoViewModel.Model.Logger.Log(String.Format("{0} elapsed for loading Dynamo main window.",
                                                                     _timer.Elapsed));
            InitializeShortcutBar();
            InitializeStartPage();

#if !__NO_SAMPLES_MENU
            LoadSamplesMenu();
#endif
            #region Search initialization

            var search = new SearchView(
                this.dynamoViewModel.SearchViewModel,
                this.dynamoViewModel);
            sidebarGrid.Children.Add(search);
            this.dynamoViewModel.SearchViewModel.Visible = true;

            #endregion

            //PACKAGE MANAGER
            dynamoViewModel.RequestPackagePublishDialog += DynamoViewModelRequestRequestPackageManagerPublish;
            dynamoViewModel.RequestManagePackagesDialog += DynamoViewModelRequestShowInstalledPackages;
            dynamoViewModel.RequestPackageManagerSearchDialog += DynamoViewModelRequestShowPackageManagerSearch;

            //FUNCTION NAME PROMPT
            dynamoViewModel.Model.RequestsFunctionNamePrompt += DynamoViewModelRequestsFunctionNamePrompt;

#if BLOODSTONE
            dynamoViewModel.RequestUpdateBloodstoneVisual += OnRequestUpdateBloodstoneVisual;
            dynamoViewModel.Model.NodeDeleted += OnModelNodeDeleted;
            dynamoViewModel.Model.WorkspaceCleared += OnWorkspaceCleared;

            if (this.visualizer == null)
            {
                var b = this.visualizerHostElement;
                this.visualizer = new VisualizerHwndHost(b.ActualWidth, b.ActualHeight);
                this.visualizer.Visibility = System.Windows.Visibility.Collapsed;
                this.visualizerHostElement.Child = this.visualizer;
            }
#endif

            dynamoViewModel.RequestClose += DynamoViewModelRequestClose;
            dynamoViewModel.RequestSaveImage += DynamoViewModelRequestSaveImage;
            dynamoViewModel.SidebarClosed += DynamoViewModelSidebarClosed;

            dynamoViewModel.Model.RequestsCrashPrompt += Controller_RequestsCrashPrompt;
            dynamoViewModel.Model.RequestTaskDialog += Controller_RequestTaskDialog;

            DynamoSelection.Instance.Selection.CollectionChanged += Selection_CollectionChanged;

            dynamoViewModel.RequestUserSaveWorkflow += DynamoViewModelRequestUserSaveWorkflow;

            dynamoViewModel.Model.ClipBoard.CollectionChanged += ClipBoard_CollectionChanged;

            //ABOUT WINDOW
            dynamoViewModel.RequestAboutWindow += DynamoViewModelRequestAboutWindow;

            // Kick start the automation run, if possible.
            dynamoViewModel.BeginCommandPlayback(this);
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.visualizer != null && (e.PropertyName == "ShowStartPage"))
            {
                var visibility = Visibility.Visible;
                if ((sender as DynamoViewModel).ShowStartPage)
                    visibility = Visibility.Collapsed;

                this.visualizer.Visibility = visibility;
            }
        }

#if BLOODSTONE

        private void OnRequestUpdateBloodstoneVisual(object sender, UpdateBloodstoneVisualEventArgs e)
        {
            if (this.visualizer == null)
                return;

            var geometries = e.Geometries.ToDictionary(
                    item => item.Key.ToString(), item => item.Value);

            var scene = visualizer.CurrentVisualizer.GetScene();
            if (scene == null)
                return;

            scene.UpdateNodeGeometries(geometries);

            var renderModes = new Dictionary<string, RenderMode>();
            var nodeColors = new Dictionary<string, Dynamo.Bloodstone.NodeColor>();

            foreach (var node in dynamoViewModel.Model.Nodes)
            {
                var nodeId = node.GUID.ToString();
                renderModes.Add(nodeId, node.RenderStyle);

                var c = node.NodeColor;
                nodeColors.Add(nodeId, new NodeColor(c.R, c.G, c.B, c.A));
            }

            scene.SetNodeRenderMode(renderModes);
            scene.SetNodeColor(nodeColors);
        }

        private void OnModelNodeDeleted(NodeModel node)
        {
            var scene = visualizer.CurrentVisualizer.GetScene();
            if (scene != null)
            {
                var identifiers = new List<string>();
                identifiers.Add(node.GUID.ToString());
                scene.RemoveNodeGeometries(identifiers);
            }
        }

        private void OnWorkspaceCleared(object sender, EventArgs e)
        {
            var scene = visualizer.CurrentVisualizer.GetScene();
            if (scene != null)
                scene.ClearAllGeometries();
        }

        internal void OnNodePropertyUpdated(NodeModel node)
        {
            var scene = visualizer.CurrentVisualizer.GetScene();
            if (scene != null)
            {
                var renderModes = new Dictionary<string, RenderMode>();
                renderModes.Add(node.GUID.ToString(), node.RenderStyle);
                scene.SetNodeRenderMode(renderModes);

                var c = node.NodeColor;
                var nodeColors = new Dictionary<string, NodeColor>();
                var color = new NodeColor(c.R, c.G, c.B, c.A);
                nodeColors.Add(node.GUID.ToString(), color);
                scene.SetNodeColor(nodeColors);
            }
        }

#endif

        void DynamoView_Unloaded(object sender, RoutedEventArgs e)
        {
#if BLOODSTONE
            if (this.visualizer != null)
            {
                this.visualizer.Dispose();
                this.visualizer = null;
            }
#endif
        }

        private UI.Views.AboutWindow _aboutWindow;
        void DynamoViewModelRequestAboutWindow(DynamoViewModel model)
        {
            if (_aboutWindow == null)
            {
                _aboutWindow = new AboutWindow(model);
                _aboutWindow.Closed += (sender, args) => _aboutWindow = null;
                _aboutWindow.Show();

                if (_aboutWindow.IsLoaded && this.IsLoaded) _aboutWindow.Owner = this;
            }

            _aboutWindow.Focus();
        }

        private PackageManagerPublishView _pubPkgView;
        void DynamoViewModelRequestRequestPackageManagerPublish(PublishPackageViewModel model)
        {
            if (_pubPkgView == null)
            {
                _pubPkgView = new PackageManagerPublishView(model);
                _pubPkgView.Closed += (sender, args) => _pubPkgView = null;
                _pubPkgView.Show();

                if (_pubPkgView.IsLoaded && this.IsLoaded) _pubPkgView.Owner = this;
            }

            _pubPkgView.Focus();
        }

        private PackageManagerSearchView _searchPkgsView;
        private PackageManagerSearchViewModel _pkgSearchVM;
        void DynamoViewModelRequestShowPackageManagerSearch(object s, EventArgs e)
        {
            if (_pkgSearchVM == null)
            {
                _pkgSearchVM = new PackageManagerSearchViewModel(dynamoViewModel.PackageManagerClientViewModel);
            }

            if (_searchPkgsView == null)
            {
                _searchPkgsView = new PackageManagerSearchView(_pkgSearchVM);
                _searchPkgsView.Closed += (sender, args) => _searchPkgsView = null;
                _searchPkgsView.Show();

                if (_searchPkgsView.IsLoaded && this.IsLoaded) _searchPkgsView.Owner = this;
            }
            
            _searchPkgsView.Focus();
            _pkgSearchVM.RefreshAndSearchAsync();
        }

        private InstalledPackagesView _installedPkgsView;
        void DynamoViewModelRequestShowInstalledPackages(object s, EventArgs e)
        {
            if (_installedPkgsView == null)
            {
                _installedPkgsView = new InstalledPackagesView(new InstalledPackagesViewModel(dynamoViewModel, 
                    dynamoViewModel.Model.Loader.PackageLoader));
                _installedPkgsView.Closed += (sender, args) => _installedPkgsView = null;
                _installedPkgsView.Show();

                if (_installedPkgsView.IsLoaded && this.IsLoaded) _installedPkgsView.Owner = this;
            }
            _installedPkgsView.Focus();
        }

        void ClipBoard_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            dynamoViewModel.CopyCommand.RaiseCanExecuteChanged();
            dynamoViewModel.PasteCommand.RaiseCanExecuteChanged();
        }

        void DynamoViewModelRequestUserSaveWorkflow(object sender, WorkspaceSaveEventArgs e)
        {
            var dialogText = "";
            if (e.Workspace is CustomNodeWorkspaceModel)
            {
                dialogText = "You have unsaved changes to custom node workspace: \"" + e.Workspace.Name +
                             "\"\n\n Would you like to save your changes?";
            }
            else // homeworkspace
            {
                if (string.IsNullOrEmpty(e.Workspace.FileName))
                {
                    dialogText = "You have unsaved changes to the Home workspace." +
                                 "\n\n Would you like to save your changes?";
                }
                else
                {
                    dialogText = "You have unsaved changes to " + Path.GetFileName(e.Workspace.FileName) +
                    "\n\n Would you like to save your changes?";
                }
            }

            var buttons = e.AllowCancel ? MessageBoxButton.YesNoCancel : MessageBoxButton.YesNo;
            var result = System.Windows.MessageBox.Show(dialogText, "Confirmation", buttons, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                dynamoViewModel.ShowSaveDialogIfNeededAndSave(e.Workspace);
                e.Success = true;
            }
            else if (result == MessageBoxResult.Cancel)
            {
                //return false;
                e.Success = false;
            }
            else
            {
                e.Success = true;
            }
        }

        void Selection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
#if BLOODSTONE
            var scene = visualizer.CurrentVisualizer.GetScene();
            if (scene != null)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        var list = e.NewItems.OfType<NodeModel>().ToList();
                        var nodes = list.Select(n => n.GUID.ToString());
                        scene.SelectNodes(nodes, Dynamo.Bloodstone.SelectMode.AddToExisting);
                        break;

                    case NotifyCollectionChangedAction.Remove:
                        var old = e.OldItems.OfType<NodeModel>().ToList();
                        var removed = old.Select(n => n.GUID.ToString());
                        scene.SelectNodes(removed, Dynamo.Bloodstone.SelectMode.RemoveFromExisting);
                        break;

                    case NotifyCollectionChangedAction.Reset:
                        var empty = new List<string>(); // Empty node list.
                        scene.SelectNodes(empty, Dynamo.Bloodstone.SelectMode.ClearExisting);
                        break;
                }
            }
#endif
            dynamoViewModel.CopyCommand.RaiseCanExecuteChanged();
            dynamoViewModel.PasteCommand.RaiseCanExecuteChanged();
        }
        
        void Controller_RequestsCrashPrompt(object sender, CrashPromptArgs args)
        {
            var prompt = new CrashPrompt(args);
            prompt.ShowDialog();
        }

        void Controller_RequestTaskDialog(object sender, UI.Prompts.TaskDialogEventArgs e)
        {
            var taskDialog = new Dynamo.UI.Prompts.GenericTaskDialog(e);
            taskDialog.ShowDialog();
        }

        void DynamoViewModelRequestSaveImage(object sender, ImageSaveEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Path))
            {
                var control = WPF.FindChild<DragCanvas>(this, null);

                double width = 1;
                double height = 1;

                // connectors are most often within the bounding box of the nodes and notes

                foreach (NodeModel n in dynamoViewModel.Model.CurrentWorkspace.Nodes)
                {
                    width = Math.Max(n.X + n.Width, width);
                    height = Math.Max(n.Y + n.Height, height);
                }

                foreach (NoteModel n in dynamoViewModel.Model.CurrentWorkspace.Notes)
                {
                    width = Math.Max(n.X + n.Width, width);
                    height = Math.Max(n.Y + n.Height, height);
                }

                var rtb = new RenderTargetBitmap(Math.Max(1, (int)width),
                                                  Math.Max(1, (int)height),
                                                  96,
                                                  96,
                                                  System.Windows.Media.PixelFormats.Default);

                rtb.Render(control);

                //endcode as PNG
                var pngEncoder = new PngBitmapEncoder();
                pngEncoder.Frames.Add(BitmapFrame.Create(rtb));

                try
                {
                    using (var stm = File.Create(e.Path))
                    {
                        pngEncoder.Save(stm);
                    }
                }
                catch
                {
                    dynamoViewModel.Model.Logger.Log("Failed to save the Workspace an image.");
                }
            }
        }

        void DynamoViewModelRequestClose(object sender, EventArgs e)
        {
            Close();
        }

        void DynamoViewModelSidebarClosed(object sender, EventArgs e)
        {
            LibraryClicked(sender, e);
        }

        /// <summary>
        /// Handles the request for the presentation of the function name prompt
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void DynamoViewModelRequestsFunctionNamePrompt(object sender, FunctionNamePromptEventArgs e)
        {
            ShowNewFunctionDialog(e);
        }

        /// <summary>
        /// Presents the function name dialogue. Returns true if the user enters
        /// a function name and category.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="category"></param>
        /// <returns></returns>
        public void ShowNewFunctionDialog(FunctionNamePromptEventArgs e)
        {
            string error = "";

            do
            {
                var dialog = new FunctionNamePrompt(dynamoViewModel.Model.SearchModel.Categories)
                {
                    categoryBox = { Text = e.Category },
                    DescriptionInput = { Text = e.Description },
                    nameView = { Text = e.Name },
                    nameBox = { Text = e.Name }
                };

                if (e.CanEditName)
                {
                    dialog.nameBox.Visibility = Visibility.Visible;
                    dialog.nameView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    dialog.nameView.Visibility = Visibility.Visible;
                    dialog.nameBox.Visibility = Visibility.Collapsed;
                }

                if (dialog.ShowDialog() != true)
                {
                    e.Success = false;
                    return;
                }

                if (String.IsNullOrEmpty(dialog.Text))
                {
                    error = "You must supply a name.";
                    MessageBox.Show(error, "Custom Node Property Error", MessageBoxButton.OK,
                                                   MessageBoxImage.Error);
                }
                else if (e.Name != dialog.Text && dynamoViewModel.Model.BuiltInTypesByNickname.ContainsKey(dialog.Text))
                {
                    error = "A built-in node with the given name already exists.";
                    MessageBox.Show(error, "Custom Node Property Error", MessageBoxButton.OK,
                                                   MessageBoxImage.Error);
                }
                else if (dialog.Category.Equals(""))
                {
                    error = "You must enter a new category or choose one from the existing categories.";
                    MessageBox.Show(error, "Custom Node Property Error", MessageBoxButton.OK,
                                                   MessageBoxImage.Error);
                }
                else
                {
                    error = "";
                }

                e.Name = dialog.Text;
                e.Category = dialog.Category;
                e.Description = dialog.Description;

            } while (!error.Equals(""));

            e.Success = true;
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            if (dynamoViewModel.exitInvoked)
                return;

            var res = dynamoViewModel.AskUserToSaveWorkspacesOrCancel();
            if (!res)
            {
                e.Cancel = true;
                return;
            }

            SizeChanged -= DynamoView_SizeChanged;
            LocationChanged -= DynamoView_LocationChanged;

            if (!DynamoModel.IsTestMode)
            {
                dynamoViewModel.Model.ShutDown(false);
            }

        }

        private void WindowClosed(object sender, EventArgs e)
        {
            Debug.WriteLine("Dynamo window closed.");

            dynamoViewModel.Model.RequestLayoutUpdate -= vm_RequestLayoutUpdate;

            //PACKAGE MANAGER
            dynamoViewModel.RequestPackagePublishDialog -= DynamoViewModelRequestRequestPackageManagerPublish;
            dynamoViewModel.RequestManagePackagesDialog -= DynamoViewModelRequestShowInstalledPackages;
            dynamoViewModel.RequestPackageManagerSearchDialog -= DynamoViewModelRequestShowPackageManagerSearch;

            //FUNCTION NAME PROMPT
            dynamoViewModel.Model.RequestsFunctionNamePrompt -= DynamoViewModelRequestsFunctionNamePrompt;

#if BLOODSTONE
            dynamoViewModel.RequestUpdateBloodstoneVisual -= OnRequestUpdateBloodstoneVisual;
            dynamoViewModel.Model.NodeDeleted -= OnModelNodeDeleted;
            dynamoViewModel.Model.WorkspaceCleared -= OnWorkspaceCleared;
#endif

            dynamoViewModel.RequestClose -= DynamoViewModelRequestClose;
            dynamoViewModel.RequestSaveImage -= DynamoViewModelRequestSaveImage;
            dynamoViewModel.SidebarClosed -= DynamoViewModelSidebarClosed;

            DynamoSelection.Instance.Selection.CollectionChanged -= Selection_CollectionChanged;

            dynamoViewModel.RequestUserSaveWorkflow -= DynamoViewModelRequestUserSaveWorkflow;

            if (dynamoViewModel.Model != null)
            {
                dynamoViewModel.Model.RequestsCrashPrompt -= Controller_RequestsCrashPrompt;
                dynamoViewModel.Model.RequestTaskDialog -= Controller_RequestTaskDialog;
                dynamoViewModel.Model.ClipBoard.CollectionChanged -= ClipBoard_CollectionChanged;
            }

            //ABOUT WINDOW
            dynamoViewModel.RequestAboutWindow -= DynamoViewModelRequestAboutWindow;
        }

        // the key press event is being intercepted before it can get to
        // the active workspace. This code simply grabs the key presses and
        // passes it to thecurrent workspace
        void DynamoView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                dynamoViewModel.WatchEscapeIsDown = true;
        }

        void DynamoView_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                dynamoViewModel.WatchEscapeIsDown = false;
                dynamoViewModel.EscapeCommand.Execute(null);
            }
        }

        private void WorkspaceTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dynamoViewModel != null)
            {
                int workspace_index = dynamoViewModel.CurrentWorkspaceIndex;

                //this condition is added for shutdown when we are clearing
                //the workspace collection
                if (workspace_index == -1) return;

                var workspace_vm = dynamoViewModel.Workspaces[workspace_index];
                workspace_vm.Model.OnCurrentOffsetChanged(this, new PointEventArgs(new Point(workspace_vm.Model.X, workspace_vm.Model.Y)));
                workspace_vm.Model.OnZoomChanged(this, new ZoomEventArgs(workspace_vm.Zoom));

                ToggleWorkspaceTabVisibility(WorkspaceTabs.SelectedIndex);
            }
        }

        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            LogScroller.ScrollToBottom();
        }

#if !__NO_SAMPLES_MENU
        /// <summary>
        ///     Setup the "Samples" sub-menu with contents of samples directory.
        /// </summary>
        private void LoadSamplesMenu()
        {
            if (Directory.Exists(DynamoPathManager.Instance.CommonSamples))
            {
                var sampleFiles = new System.Collections.Generic.List<string>();
                string[] dirPaths = Directory.GetDirectories(DynamoPathManager.Instance.CommonSamples);
                string[] filePaths = Directory.GetFiles(DynamoPathManager.Instance.CommonSamples, "*.dyn");

                // handle top-level files
                if (filePaths.Any())
                {
                    foreach (string path in filePaths)
                    {
                        var item = new MenuItem
                        {
                            Header = Path.GetFileNameWithoutExtension(path),
                            Tag = path
                        };
                        item.Click += OpenSample_Click;
                        SamplesMenu.Items.Add(item);
                        sampleFiles.Add(path);
                    }
                }

                // handle top-level dirs, TODO - factor out to a seperate function, make recusive
                if (dirPaths.Any())
                {
                    foreach (string dirPath in dirPaths)
                    {
                        var dirItem = new MenuItem
                        {
                            Header = Path.GetFileName(dirPath),
                            Tag = Path.GetFileName(dirPath)
                        };

                        filePaths = Directory.GetFiles(dirPath, "*.dyn");
                        if (filePaths.Any())
                        {
                            foreach (string path in filePaths)
                            {
                                var item = new MenuItem
                                {
                                    Header = Path.GetFileNameWithoutExtension(path),
                                    Tag = path
                                };
                                item.Click += OpenSample_Click;
                                dirItem.Items.Add(item);
                                sampleFiles.Add(path);
                            }
                        }
                        SamplesMenu.Items.Add(dirItem);
                    }
                }

                if (dirPaths.Any())
                {
                    var showInFolder = new MenuItem
                    {
                        Header = "Show In Folder",
                        Tag = dirPaths[0]
                    };
                    showInFolder.Click += OnShowInFolder;
                    SamplesMenu.Items.Add(new Separator());
                    SamplesMenu.Items.Add(showInFolder);
                }

                if (sampleFiles.Any()&&this.startPage != null)
                {
                    var firstFilePath=Path.GetDirectoryName(sampleFiles.ToArray()[0]);
                    var rootPath = Path.GetDirectoryName(firstFilePath);
                    var root = new DirectoryInfo(rootPath);
                    var rootProperty = new SampleFileEntry("Samples", "Path");
                    this.startPage.WalkDirectoryTree(root, rootProperty);
                    this.startPage.SampleFiles.Add(rootProperty);
                }
            }
        }

        private static void OnShowInFolder(object sender, RoutedEventArgs e)
        {
            var folderPath = (string)((MenuItem)sender).Tag;
            Process.Start("explorer.exe", "/select," + folderPath);
        }
#endif

        /// <summary>
        /// Setup the "Samples" sub-menu with contents of samples directory.
        /// </summary>
        private void OpenSample_Click(object sender, RoutedEventArgs e)
        {
            var path = (string)((MenuItem)sender).Tag;

            var workspace = dynamoViewModel.Model.HomeSpace;
            if (workspace.HasUnsavedChanges)
            {
                if (!dynamoViewModel.AskUserToSaveWorkspaceOrCancel(workspace))
                    return; // User has not saved his/her work.
            }

            // KILLDYNSETTINGS - CanGoHome should live on the ViewModel
            if (dynamoViewModel.Model.CanGoHome(null))
                dynamoViewModel.Model.Home(null);

            dynamoViewModel.OpenCommand.Execute(path);
        }

        private void TabControlMenuItem_Click(object sender, RoutedEventArgs e)
        {
            BindingExpression be = ((MenuItem)sender).GetBindingExpression(MenuItem.HeaderProperty);
            WorkspaceViewModel wsvm = (WorkspaceViewModel)be.DataItem;
            WorkspaceTabs.SelectedIndex = dynamoViewModel.Workspaces.IndexOf(wsvm);
            ToggleWorkspaceTabVisibility(WorkspaceTabs.SelectedIndex);
        }

        public CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
        {
            Point popupLocation = new Point(targetSize.Width - popupSize.Width, targetSize.Height);

            CustomPopupPlacement placement1 = 
                new CustomPopupPlacement(popupLocation, PopupPrimaryAxis.Vertical);

            CustomPopupPlacement placement2 =
                new CustomPopupPlacement(popupLocation, PopupPrimaryAxis.Horizontal);

            CustomPopupPlacement[] ttplaces = 
                new CustomPopupPlacement[] { placement1, placement2 };
            return ttplaces;
        }

        private void ToggleWorkspaceTabVisibility(int tabSelected)
        {
            SlideWindowToIncludeTab(tabSelected);

            for (int tabIndex = 1; tabIndex < WorkspaceTabs.Items.Count; tabIndex++)
            {
                TabItem tabItem = (TabItem)WorkspaceTabs.ItemContainerGenerator.ContainerFromIndex(tabIndex);
                if (tabIndex < tabSlidingWindowStart || tabIndex > tabSlidingWindowEnd)
                    tabItem.Visibility = Visibility.Collapsed;
                else
                    tabItem.Visibility = Visibility.Visible;
            }
        }

        private int GetSlidingWindowSize()
        {
            int tabCount = WorkspaceTabs.Items.Count;

            // Note: returning -1 for Home tab being always visible.
            // Home tab is not taken account into sliding window
            if (tabCount > Configurations.MinTabsBeforeClipping)
            {
                // Usable tab control width need to exclude tabcontrol menu
                int usableTabControlWidth = (int)WorkspaceTabs.ActualWidth - Configurations.TabControlMenuWidth;

                int fullWidthTabsVisible = usableTabControlWidth / Configurations.TabDefaultWidth;

                if (fullWidthTabsVisible < Configurations.MinTabsBeforeClipping)
                    return Configurations.MinTabsBeforeClipping - 1;
                else
                {
                    if (tabCount < fullWidthTabsVisible)
                        return tabCount - 1;

                    return fullWidthTabsVisible - 1;
                }
            }
            else
                return tabCount - 1;
        }

        private void SlideWindowToIncludeTab(int tabSelected)
        {            
            int newSlidingWindowSize = GetSlidingWindowSize();

            if (newSlidingWindowSize == 0)
            {
                tabSlidingWindowStart = tabSlidingWindowEnd = 0;
                return;
            }

            if (tabSelected != 0)
            {
                // Selection is not home tab
                if (tabSelected < tabSlidingWindowStart)
                {
                    // Slide window towards the front
                    tabSlidingWindowStart = tabSelected;
                    tabSlidingWindowEnd = tabSlidingWindowStart + (newSlidingWindowSize - 1);
                }
                else if (tabSelected > tabSlidingWindowEnd)
                {
                    // Slide window towards the end
                    tabSlidingWindowEnd = tabSelected;
                    tabSlidingWindowStart = tabSlidingWindowEnd - (newSlidingWindowSize - 1);
                }
                else
                {
                    int currentSlidingWindowSize = tabSlidingWindowEnd - tabSlidingWindowStart + 1;
                    int windowDiff = Math.Abs(currentSlidingWindowSize - newSlidingWindowSize);

                    // Handles sliding window size change caused by window resizing
                    if (currentSlidingWindowSize > newSlidingWindowSize)
                    {
                        // Trim window
                        while (windowDiff > 0)
                        {
                            if (tabSelected == tabSlidingWindowEnd)
                                tabSlidingWindowStart++; // Trim from front
                            else
                                tabSlidingWindowEnd--; // Trim from end

                            windowDiff--;
                        }
                    }
                    else if (currentSlidingWindowSize < newSlidingWindowSize)
                    {
                        // Expand window
                        int lastTab = WorkspaceTabs.Items.Count - 1;

                        while (windowDiff > 0)
                        {
                            if (tabSlidingWindowEnd == lastTab)
                                tabSlidingWindowStart--;
                            else
                                tabSlidingWindowEnd++;

                            windowDiff--;
                        }
                    }
                    else
                    {
                        // Handle tab closing

                    }
                }
            }
            else
            {
                // Selection is home tab
                int currentSlidingWindowSize = tabSlidingWindowEnd - tabSlidingWindowStart + 1;
                int windowDiff = Math.Abs(currentSlidingWindowSize - newSlidingWindowSize);

                int lastTab = WorkspaceTabs.Items.Count - 1;

                // Handles sliding window size change caused by window resizing and tab close
                if (currentSlidingWindowSize > newSlidingWindowSize)
                {
                    // Trim window
                    while (windowDiff > 0)
                    {
                        tabSlidingWindowEnd--; // Trim from end

                        windowDiff--;
                    }
                }
                else if (currentSlidingWindowSize < newSlidingWindowSize)
                {
                    // Expand window due to window resize
                    while (windowDiff > 0)
                    {
                        if (tabSlidingWindowEnd == lastTab)
                            tabSlidingWindowStart--;
                        else
                            tabSlidingWindowEnd++;

                        windowDiff--;
                    }
                }
                else
                {
                    // Handle tab closing with no change in window size
                    // Shift window

                    if (tabSlidingWindowEnd > lastTab)
                    {
                        tabSlidingWindowStart--;
                        tabSlidingWindowEnd--;
                    }
                }
            }

        }

		private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            Grid g = (Grid)sender;
            TextBlock tb = (TextBlock)(g.Children[1]);
            var bc = new BrushConverter();
            tb.Foreground = (Brush)bc.ConvertFrom("#cccccc");
            Image collapseIcon = (Image)g.Children[0];
            var imageUri = new Uri(@"pack://application:,,,/DynamoCore;component/UI/Images/expand_hover.png");

            BitmapImage hover = new BitmapImage(imageUri);
            // hover.Rotation = Rotation.Rotate180;

            collapseIcon.Source = hover;
        }

        private void Button_Click(object sender, EventArgs e)
        {
            UserControl view = (UserControl)this.sidebarGrid.Children[0];
            if (view.Visibility == Visibility.Collapsed)
            {
                view.Width = double.NaN;
                view.HorizontalAlignment = HorizontalAlignment.Stretch;
                view.Height = double.NaN;
                view.VerticalAlignment = VerticalAlignment.Stretch;

                this.mainGrid.ColumnDefinitions[0].Width = new System.Windows.GridLength(restoreWidth);
                this.verticalSplitter.Visibility = Visibility.Visible;
                view.Visibility = Visibility.Visible;
                this.sidebarGrid.Visibility = Visibility.Visible;
                this.collapsedSidebar.Visibility = Visibility.Collapsed;
            }
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            Grid g = (Grid)sender;
            TextBlock tb = (TextBlock)(g.Children[1]);
            var bc = new BrushConverter();
            tb.Foreground = (Brush)bc.ConvertFromString("#aaaaaa");
            Image collapseIcon = (Image)g.Children[0];

            // Change the collapse icon and rotate
            var imageUri = new Uri(@"pack://application:,,,/DynamoCore;component/UI/Images/expand_normal.png");
            BitmapImage hover = new BitmapImage(imageUri);

            collapseIcon.Source = hover;
        }

        private double restoreWidth = 0;

        private void LibraryClicked(object sender, EventArgs e)
        {
            restoreWidth = this.sidebarGrid.ActualWidth;

            this.mainGrid.ColumnDefinitions[0].Width = new System.Windows.GridLength(0.0);
            this.verticalSplitter.Visibility = System.Windows.Visibility.Collapsed;
            this.sidebarGrid.Visibility = System.Windows.Visibility.Collapsed;

            this.horizontalSplitter.Width = double.NaN;
            UserControl view = (UserControl)this.sidebarGrid.Children[0];
            view.Visibility = Visibility.Collapsed;

            this.sidebarGrid.Visibility = Visibility.Collapsed;
            this.collapsedSidebar.Visibility = Visibility.Visible;
        }

        private void Workspace_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //http://stackoverflow.com/questions/4474670/how-to-catch-the-ending-resize-window

            // Children of the workspace, including the zoom border and the endless grid
            // are expensive to resize. We use a timer here to defer resizing until 
            // after workspace resizing is complete. This improves the responziveness of
            // the UI during resize.

            _workspaceResizeTimer.IsEnabled = true;
            _workspaceResizeTimer.Stop();
            _workspaceResizeTimer.Start();
        }

        void _resizeTimer_Tick(object sender, EventArgs e)
        {
            _workspaceResizeTimer.IsEnabled = false;

            // end of timer processing
            if (dynamoViewModel == null)
                return;
            dynamoViewModel.WorkspaceActualSize(border.ActualWidth, border.ActualHeight);

            Debug.WriteLine("Resizing workspace children.");
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            dynamoViewModel.IsMouseDown = true;
		}

        private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            dynamoViewModel.IsMouseDown = false;
		}

        private void WorkspaceTabs_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            ToggleWorkspaceTabVisibility(WorkspaceTabs.SelectedIndex);
        }

        private void WorkspaceTabs_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ToggleWorkspaceTabVisibility(WorkspaceTabs.SelectedIndex);
        }
       
        private void RunButton_OnClick(object sender, RoutedEventArgs e)
        {
            dynamoViewModel.ReturnFocusToSearch();
        }

        private void DynamoView_OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (dynamoViewModel.Model.HomeSpace.HasUnsavedChanges && !dynamoViewModel.AskUserToSaveWorkspaceOrCancel(dynamoViewModel.Model.HomeSpace))
                {
                    return;
                }

                if (dynamoViewModel.OpenCommand.CanExecute(files[0]))
                {
                    dynamoViewModel.OpenCommand.Execute(files[0]);
                }
                
            }

            e.Handled = true;
        }
    }
}
