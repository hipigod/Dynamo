﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media.Media3D;
using Dynamo.Controls;
using Dynamo.DSEngine;
using Dynamo.Models;
using Dynamo.Nodes;
using Dynamo.Utilities;
using NUnit.Framework;

namespace DynamoCoreUITests
{
    [TestFixture]
    public class VisualizationManagerUITests : DynamoTestUIBase
    {
        private Watch3DView BackgroundPreview
        {
            get
            {
                return (Watch3DView)View.background_grid.FindName("background_preview");
            }
        }

        [Test, Category("Failure")]
        public void NothingIsVisualizedWhenThereIsNothingToVisualize()
        {
            var viz = ViewModel.VisualizationManager;

            // run the expression
            ViewModel.Model.RunExpression();

            Assert.AreEqual(0, BackgroundPreview.Points.Count);
            Assert.AreEqual(0, BackgroundPreview.Lines.Count);
            Assert.AreEqual(0, BackgroundPreview.Mesh.Positions.Count);
            Assert.AreEqual(0, BackgroundPreview.XAxes.Count);
            Assert.AreEqual(0, BackgroundPreview.YAxes.Count);
            Assert.AreEqual(0, BackgroundPreview.ZAxes.Count);
        }

        [Test, Category("Failure")]
        public void BackgroundPreviewDrawsOnOpen()
        {
            //var model = ViewModel.Model;
            //var viz = ViewModel.VisualizationManager;

            //string openPath = Path.Combine(GetTestDirectory(), @"core\visualization\ASM_points.dyn");
            //model.Open(openPath);

            //// run the expression
            //ViewModel.Model.RunExpression();

            ////graphics will have been updated at this point
            ////enabled the background preview and ensure that it 

            Assert.Inconclusive("Finish me!");
        }

        [Test, Category("Failure")]
        public void CleansUpGeometryWhenNodeFails()
        {
            Assert.Inconclusive("Can not test post-failure visualization state as we need to " +
                                "throwing testing exception which avoid OnEvaluationComplete being called.");

            //var model = ViewModel.Model;
            //var viz = ViewModel.VisualizationManager;

            //string openPath = Path.Combine(GetTestDirectory(), @"core\visualization\ASM_points.dyn");
            //model.Open(openPath);

            //// check all the nodes and connectors are loaded
            //Assert.AreEqual(3, model.CurrentWorkspace.Nodes.Count);
            //Assert.AreEqual(4, model.CurrentWorkspace.Connectors.Count);

            //// run the expression
            //ViewModel.Model.RunExpression();

            ////adjust the number node's value - currently set to 0..5 to something that makes the XYZ error
            //var numNode = (DoubleInput)model.Nodes.First(x => x is DoubleInput);
            //numNode.Value = "blah";

            //// run the expression
            //// it will fail
            //Assert.Throws(typeof(NUnit.Framework.AssertionException), () => ViewModel.Model.RunExpression());
            //var renderables = viz.Visualizations.SelectMany(x => x.Value.Points);
            //Assert.AreEqual(0, renderables.Count());
        }

        [Test, Category("Failure")]
        public void VisualizationInSyncWithPreview()
        {
            var model = ViewModel.Model;
            var viz = ViewModel.VisualizationManager;

            string openPath = Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\visualization\ASM_points_line.dyn");
            ViewModel.OpenCommand.Execute(openPath);

            // run the expression
            ViewModel.Model.RunExpression();

            //we start with all previews disabled
            //the graph is two points feeding into a line

            //ensure that visulations match our expectations
            Assert.AreEqual(7, BackgroundPreview.Points.Count);
            Assert.AreEqual(12, BackgroundPreview.Lines.Count);
            Assert.AreEqual(0, BackgroundPreview.MeshCount);

            //now flip off the preview on one of the points
            //and ensure that the visualization updates without re-running
            var p1 = model.Nodes.First(x => x.GUID.ToString() == "a7c70c13-cc62-41a6-85ed-dc42e788181d");
            p1.IsVisible = false;

            Assert.AreEqual(1, BackgroundPreview.Points.Count);
            Assert.AreEqual(12, BackgroundPreview.Lines.Count);
            Assert.AreEqual(0, BackgroundPreview.MeshCount);

            //flip off the lines node
            var l1 = model.Nodes.First(x => x.GUID.ToString() == "7c1cecee-43ed-43b5-a4bb-5f71c50341b2");
            l1.IsVisible = false;

            Assert.AreEqual(1, BackgroundPreview.Points.Count);
            Assert.AreEqual(0, BackgroundPreview.Lines.Count);
            Assert.AreEqual(0, BackgroundPreview.MeshCount);

            //flip those back on and ensure the visualization returns
            p1.IsVisible = true;
            l1.IsVisible = true;

            Assert.AreEqual(7, BackgroundPreview.Points.Count);
            Assert.AreEqual(12, BackgroundPreview.Lines.Count);
            Assert.AreEqual(0, BackgroundPreview.MeshCount);
        }

        [Test, Category("Failure")]
        public void VisualizationInSyncWithPreviewUpstream()
        {
            var model = ViewModel.Model;
            var viz = ViewModel.VisualizationManager;

            string openPath = Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\visualization\ASM_points_line.dyn");
            ViewModel.OpenCommand.Execute(openPath);

            ViewModel.Model.OnRequestLayoutUpdate(this, EventArgs.Empty);

            // run the expression
            ViewModel.Model.RunExpression();
            Thread.Sleep(1000);

            //we start with all previews disabled
            //the graph is two points feeding into a line

            //ensure that visulations match our expectations
            Assert.AreEqual(7, BackgroundPreview.Points.Count);
            Assert.AreEqual(12, BackgroundPreview.Lines.Count);
            Assert.AreEqual(0, BackgroundPreview.MeshCount);

            //flip off the line node's preview upstream
            var l1 = model.Nodes.First(x => x.GUID.ToString() == "7c1cecee-43ed-43b5-a4bb-5f71c50341b2");
            l1.IsUpstreamVisible = false;

            //ensure that the watch 3d is not showing the upstream
            //the render descriptions will still be around for those
            //nodes, but watch 3D will not be showing them

            var watch = model.Nodes.First(x => x.GetType().Name == "Watch3D");
            var watchView = watch.GetType().GetProperty("View").GetValue(watch, null);
            var points = watchView.GetType().GetProperty("Points").GetValue(watchView, null) as Point3DCollection;
            Assert.AreEqual(0, points.Count);
        }

        [Test, Category("Failure")]
        public void CanVisualizePoints()
        {
            //var model = ViewModel.Model;
            //var viz = ViewModel.VisualizationManager;

            //string openPath = Path.Combine(GetTestDirectory(), @"core\visualization\ASM_points.dyn");
            //model.Open(openPath);

            //// check all the nodes and connectors are loaded
            //Assert.AreEqual(4, model.CurrentWorkspace.Nodes.Count);
            //Assert.AreEqual(4, model.CurrentWorkspace.Connectors.Count);

            //// run the expression
            //ViewModel.Model.RunExpression();

            ////ensure that the number of visualizations matches the 
            ////number of pieces of geometry in the collection
            //Assert.AreEqual(GetTotalDrawablesInModel(), BackgroundPreview.Points.Count);

            ////adjust the number node's value - currently set to 0..5 (6 elements)
            //var numNode = (DoubleInput)model.Nodes.First(x => x is DoubleInput);
            //numNode.Value = "0..10";
            //ViewModel.Model.RunExpression();

            //Assert.AreEqual(GetTotalDrawablesInModel(), BackgroundPreview.Points.Count);

            Assert.Inconclusive("Porting : DoubleInput");
        }

        [Test, Category("Failure")]
        public void CleansUpGeometryWhenNodesAreDisconnected()
        {
            //test to ensure that when nodes are disconnected 
            //their associated geometry is removed
            var model = ViewModel.Model;
            var viz = ViewModel.VisualizationManager;

            string openPath = Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\visualization\ASM_points_line.dyn");
            ViewModel.OpenCommand.Execute(openPath);

            // run the expression
            ViewModel.Model.RunExpression();

            //ensure the correct representations

            //look at the data in the visualization manager
            //ensure that the number of Drawable nodes
            //and the number of entries in the Dictionary match
            Assert.AreEqual(7, BackgroundPreview.Points.Count);
            Assert.AreEqual(6, BackgroundPreview.Lines.Count / 2);
            
            //delete a conector coming into the lines node
            var lineNode = model.Nodes.FirstOrDefault(x => x.GUID.ToString() == "7c1cecee-43ed-43b5-a4bb-5f71c50341b2");
            var port = lineNode.InPorts.First();
            port.Disconnect(port.Connectors.First());

            //ensure that the visualization no longer contains
            //the renderables for the line node
            Assert.AreEqual(7, BackgroundPreview.Points.Count);
            Assert.AreEqual(0, BackgroundPreview.Lines.Count);
        }

        [Test, Category("Failure")]
        public void CanVisualizeASMSolids()
        {
            var model = ViewModel.Model;

            string openPath = Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\visualization\ASM_thicken.dyn");
            ViewModel.OpenCommand.Execute(openPath);

            // run the expression
            ViewModel.Model.RunExpression();

            Assert.IsTrue(BackgroundPreview.Mesh.TriangleIndices.Count > 0);

            model.HomeSpace.HasUnsavedChanges = false;
        }

        [Test, Category("Failure")]
        public void CanVisualizeASMSurfaces()
        {
            var viz = ViewModel.VisualizationManager;

            string openPath = Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\visualization\ASM_cuboid.dyn");
            ViewModel.OpenCommand.Execute(openPath);

            // run the expression
            ViewModel.Model.RunExpression();

            //var meshes = viz.Visualizations.SelectMany(x => x.Value.Meshes);
            Assert.AreEqual(36, BackgroundPreview.Mesh.Positions.Count);
        }

        [Test, Category("Failure")]
        public void CanVisualizeCoordinateSystems()
        {
            var viz = ViewModel.VisualizationManager;

            string openPath = Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\visualization\ASM_coordinateSystem.dyn");
            ViewModel.OpenCommand.Execute(openPath);

            // run the expression
            ViewModel.Model.RunExpression();

            Assert.AreEqual(2, BackgroundPreview.XAxes.Count);
            Assert.AreEqual(2, BackgroundPreview.YAxes.Count);
            Assert.AreEqual(2, BackgroundPreview.ZAxes.Count);
        }

        [Test, Category("Failure")]
        public void CanVisualizeGeometryFromPython()
        {
            var viz = ViewModel.VisualizationManager;

            string openPath = Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\visualization\ASM_python.dyn");
            ViewModel.OpenCommand.Execute(openPath);

            // run the expression
            ViewModel.Model.RunExpression();

            //total points are the two strips of points at the top and
            //bottom of the mesh, duplicated 11x2x2 plus the one mesh
            Assert.AreEqual(1000, BackgroundPreview.Points.Count);
            Assert.AreEqual(1000, BackgroundPreview.MeshCount);

        }

        [Test, Category("Failure")]
        public void VisualizationIsDeletedWhenNodeIsRemoved()
        {
            var model = ViewModel.Model;

            string openPath = Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\visualization\ASM_points.dyn");
            ViewModel.OpenCommand.Execute(openPath);

            // check all the nodes and connectors are loaded
            Assert.AreEqual(4, model.CurrentWorkspace.Nodes.Count);
            Assert.AreEqual(4, model.CurrentWorkspace.Connectors.Count);

            // run the expression
            ViewModel.Model.RunExpression();

            Assert.AreEqual(6, BackgroundPreview.Points.Count);

            //delete a node and ensure that the renderables are cleaned up
            var pointNode = model.Nodes.FirstOrDefault(x => x.GUID.ToString() == "0b472626-e18f-404a-bec4-d84ad7f33011");
            var modelsToDelete = new List<ModelBase> {pointNode};
            model.DeleteModelInternal(modelsToDelete);

            model.HomeSpace.HasUnsavedChanges = false;

            Assert.AreEqual(0, BackgroundPreview.Points.Count);
        }

        [Test, Category("Failure")]
        public void VisualizationsAreClearedWhenWorkspaceIsCleared()
        {
            var model = ViewModel.Model;

            string openPath = Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\visualization\ASM_points.dyn");
            ViewModel.OpenCommand.Execute(openPath);

            // run the expression
            ViewModel.Model.RunExpression();

            //ensure that we have some visualizations
            Assert.Greater(BackgroundPreview.Points.Count, 0);

            //now clear the workspace
            model.Clear(null);

            //ensure that we have no visualizations
            Assert.AreEqual(0, BackgroundPreview.Points.Count);
        }

        [Test, Category("Failure")]
        public void VisualizationsAreCreatedForCustomNodes()
        {
            Assert.IsTrue(
                ViewModel.Model.CustomNodeManager.AddFileToPath(Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\visualization\Points.dyf"))
                != null);
            string openPath = Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\visualization\ASM_customNode.dyn");
            ViewModel.OpenCommand.Execute(openPath);

            // run the expression
            ViewModel.Model.RunExpression();

            //ensure that we have some visualizations
            Assert.Greater(BackgroundPreview.Points.Count, 0);
        }

        [Test, Category("Failure")]
        public void HonorsPreviewSaveState()
        {
            string openPath = Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\visualization\ASM_points_line_noPreview.dyn");
            ViewModel.OpenCommand.Execute(openPath);

            // run the expression
            ViewModel.Model.RunExpression();

            //all nodes are set to not preview in the file
            //ensure that we have no visualizations
            Assert.AreEqual(0, BackgroundPreview.Lines.Count);
        }

        [Test, Category("Failure")]
        public void CanDrawNodeLabels()
        {
            var model = ViewModel.Model;

            string openPath = Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\visualization\Labels.dyn");
            ViewModel.OpenCommand.Execute(openPath);

            // check all the nodes and connectors are loaded
            Assert.AreEqual(2, model.CurrentWorkspace.Nodes.Count);

            //before we run the expression, confirm that all nodes
            //have label display set to false - the default
            Assert.IsTrue(model.AllNodes.All(x => x.DisplayLabels != true));

            var cbn = model.Nodes.FirstOrDefault(x => x.GUID.ToString() == "fdec3b9b-56ae-4d01-85c2-47b8425e3130") as CodeBlockNodeModel;
            Assert.IsNotNull(cbn);
            cbn.Code = "Point.ByCoordinates(a<1>,a<1>,a<1>);";
            
            // run the expression
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());
            Assert.AreEqual(4, BackgroundPreview.Points.Count());

            //label displayed should be possible now because
            //some nodes have values. toggle on label display
            cbn.DisplayLabels = true;
            Assert.AreEqual(BackgroundPreview.Text.Count(), 4);

            cbn.Code = "Point.ByCoordinates(a<1>,a<2>,a<3>);";

            //change the lacing to cross product 
            //ensure that the labels update to match
            //ptNode.ArgumentLacing = LacingStrategy.CrossProduct;
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());
            Assert.AreEqual(64, BackgroundPreview.Points.Count());
            Assert.AreEqual(64, BackgroundPreview.Text.Count());

            cbn.DisplayLabels = false;
            Assert.AreEqual(0, BackgroundPreview.Text.Count());
        }

        [Test, Category("Failure")]
        public void CanDrawNodeLabelsOnCurves()
        {
            var model = ViewModel.Model;

            string openPath = Path.Combine(GetTestDirectory(ExecutingDirectory), @"core\GeometryTestFiles\BSplineCurveTest.dyn");
            ViewModel.OpenCommand.Execute(openPath);

            // check all the nodes and connectors are loaded
            Assert.AreEqual(6, model.CurrentWorkspace.Nodes.Count);

            //before we run the expression, confirm that all nodes
            //have label display set to false - the default
            Assert.IsTrue(model.AllNodes.All(x => x.DisplayLabels != true));

            // run the expression
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());

            //10 lines segments in this file
            Assert.AreEqual(60, BackgroundPreview.Lines.Count());

            //label displayed should be possible now because
            //some nodes have values. toggle on label display
            var crvNode = model.Nodes.FirstOrDefault(x => x.GUID.ToString() == "e9e53fe0-a0b0-4cf7-93d5-5eea8f0428f2");
            Assert.IsNotNull(crvNode);
            crvNode.DisplayLabels = true;

            Assert.AreEqual(6,BackgroundPreview.Text.Count());
        }

        private int GetTotalDrawablesInModel()
        {
            return ViewModel.Model.Nodes
                .SelectMany(x=>x.RenderPackages)
                .Cast<RenderPackage>()
                .Where(x=>x.IsNotEmpty())
                .Aggregate(0,(a, b) => a + b.ItemsCount);
        }
    }
}
