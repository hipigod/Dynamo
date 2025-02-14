﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dynamo.Models;
using Dynamo.Nodes;
using Dynamo.PackageManager;
using Dynamo.Selection;

using Greg.Requests;

namespace Dynamo.ViewModels
{
    /// <summary>
    ///     A thin wrapper on the Greg rest client for performing IO with
    ///     the Package Manager
    /// </summary>
    public class PackageManagerClientViewModel
    {

        #region Properties/Fields

        ObservableCollection<PackageUploadHandle> _uploads = new ObservableCollection<PackageUploadHandle>();
        public ObservableCollection<PackageUploadHandle> Uploads
        {
            get { return _uploads; }
            set { _uploads = value; }
        }

        ObservableCollection<PackageDownloadHandle> _downloads = new ObservableCollection<PackageDownloadHandle>();
        public ObservableCollection<PackageDownloadHandle> Downloads
        {
            get { return _downloads; }
            set { _downloads = value; }
        }

        public List<PackageManagerSearchElement> CachedPackageList { get; private set; }

        private readonly DynamoViewModel dynamoViewModel;
        private readonly PackageManagerClient packageManagerClient;

        #endregion

        public PackageManagerClientViewModel(DynamoViewModel dynamoViewModel, PackageManagerClient packageManagerClient )
        {
            this.dynamoViewModel = dynamoViewModel;
            this.packageManagerClient = packageManagerClient;
            this.CachedPackageList = new List<PackageManagerSearchElement>();
            this.packageManagerClient.RequestAuthentication +=
                dynamoViewModel.OnRequestAuthentication;
        }

        public void PublishCurrentWorkspace()
        {
            var currentFunDef =
                dynamoViewModel.Model.CustomNodeManager.GetDefinitionFromWorkspace(dynamoViewModel.Model.CurrentWorkspace);

            if (currentFunDef != null)
            {
                ShowNodePublishInfo(new List<CustomNodeDefinition> { currentFunDef });
            }
            else
            {
                MessageBox.Show("The selected symbol was not found in the workspace", "Selection Error", MessageBoxButton.OK, MessageBoxImage.Question);
            }

        }

        public bool CanPublishCurrentWorkspace()
        {
            return dynamoViewModel.Model.CurrentWorkspace is CustomNodeWorkspaceModel;
        }

        public void PublishSelectedNode()
        {
            var nodeList = DynamoSelection.Instance.Selection
                                .Where(x => x is Function)
                                .Cast<Function>()
                                .Select(x => x.Definition.FunctionId)
                                .ToList();

            if (!nodeList.Any())
            {
                MessageBox.Show("You must select at least one custom node.", "Selection Error", MessageBoxButton.OK, MessageBoxImage.Question);
                return;
            }

            var defs = nodeList.Select(dynamoViewModel.Model.CustomNodeManager.GetFunctionDefinition).ToList();

            if (defs.Any(x => x == null))
                MessageBox.Show("There was a problem getting the node from the workspace.", "Selection Error", MessageBoxButton.OK, MessageBoxImage.Question);

            ShowNodePublishInfo(defs);
        }

        public bool CanPublishSelectedNode(object m)
        {
            return DynamoSelection.Instance.Selection.Count > 0 &&
                   DynamoSelection.Instance.Selection.All(x => x is Function);
        }

        private void ShowNodePublishInfo(object funcDef)
        {
            if (funcDef is List<CustomNodeDefinition>)
            {
                var fs = funcDef as List<CustomNodeDefinition>;

                foreach (var f in fs)
                {
                    var pkg = dynamoViewModel.Model.Loader.PackageLoader.GetOwnerPackage(f);

                    if (dynamoViewModel.Model.Loader.PackageLoader.GetOwnerPackage(f) != null)
                    {
                        var m = MessageBox.Show("The node is part of the dynamo package called \"" + pkg.Name +
                            "\" - do you want to submit a new version of this package?  \n\nIf not, this node will be moved to the new package you are creating.",
                            "Package Warning", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (m == MessageBoxResult.Yes)
                        {
                            var pkgVm = new PackageViewModel(dynamoViewModel, pkg);
                            pkgVm.PublishNewPackageVersionCommand.Execute();
                            return;
                        }
                    }
                }

                var newPkgVm = new PublishPackageViewModel(this.dynamoViewModel);
                newPkgVm.FunctionDefinitions = fs;
                this.dynamoViewModel.OnRequestPackagePublishDialog(newPkgVm);
            }
            else
            {
                dynamoViewModel.Model.Logger.Log("Failed to obtain function definition from node.");
            }
        }

        public List<PackageManagerSearchElement> ListAll()
        {
            this.CachedPackageList =
                    this.packageManagerClient.ListAll()
                               .Select((header) => new PackageManagerSearchElement(this.dynamoViewModel, header))
                               .ToList();

            return CachedPackageList;
        }

        public List<PackageManagerSearchElement> Search(string search, int maxNumSearchResults)
        {
            return packageManagerClient.Search(search, maxNumSearchResults)
                               .Select((header) => new PackageManagerSearchElement(this.dynamoViewModel, header))
                               .ToList();
        }

        /// <summary>
        /// This method downloads the package represented by the PackageDownloadHandle,
        /// uninstalls its current installation if necessary, and installs the package.
        /// 
        /// Note that, if the package is already installed, it must be uninstallable
        /// </summary>
        /// <param name="packageDownloadHandle"></param>
        internal void DownloadAndInstall(PackageDownloadHandle packageDownloadHandle)
        {
            var pkgDownload = new PackageDownload(packageDownloadHandle.Header._id, packageDownloadHandle.VersionName);
            this.Downloads.Add(packageDownloadHandle);

            Task.Factory.StartNew(() =>
            {
                try
                {
                    var response = packageManagerClient.Client.Execute(pkgDownload);
                    var pathDl = PackageDownload.GetFileFromResponse(response);

                    dynamoViewModel.UIDispatcher.BeginInvoke((Action)(() =>
                    {
                        try
                        {
                            packageDownloadHandle.Done(pathDl);

                            Package dynPkg;

                            var firstOrDefault = dynamoViewModel.Model.Loader.PackageLoader.LocalPackages.FirstOrDefault(pkg => pkg.Name == packageDownloadHandle.Name);
                            if (firstOrDefault != null)
                            {
                                try { firstOrDefault.UninstallCore(); }
                                catch
                                {
                                    MessageBox.Show("Dynamo failed to uninstall the package: " + packageDownloadHandle.Name +
                                        "  The package may need to be reinstalled manually.", "Uninstall Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }

                            if (packageDownloadHandle.Extract(out dynPkg))
                            {
                                var downloadPkg = Package.FromDirectory(dynPkg.RootDirectory, this.dynamoViewModel.Model);
                                downloadPkg.Load();
                                dynamoViewModel.Model.Loader.PackageLoader.LocalPackages.Add(downloadPkg);
                                packageDownloadHandle.DownloadState = PackageDownloadHandle.State.Installed;
                            }
                        }
                        catch (Exception e)
                        {
                            packageDownloadHandle.Error(e.Message);
                        }
                    }));

                }
                catch (Exception e)
                {
                    packageDownloadHandle.Error(e.Message);
                }
            });

        }

        public void ClearCompletedDownloads()
        {
            Downloads.Where((x) => x.DownloadState == PackageDownloadHandle.State.Installed ||
                x.DownloadState == PackageDownloadHandle.State.Error).ToList().ForEach(x => Downloads.Remove(x));
        }

        internal void GoToWebsite()
        {
            Process.Start(packageManagerClient.Client.BaseUrl);
        }

    }

}
