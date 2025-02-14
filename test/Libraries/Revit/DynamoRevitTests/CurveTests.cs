﻿using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Dynamo.Utilities;
using Dynamo.Nodes;
using NUnit.Framework;
using RevitServices.Persistence;
using RTF.Framework;
using CurveByPoints = Autodesk.Revit.DB.CurveByPoints;

namespace Dynamo.Tests
{
    [TestFixture]
    class CurveTests:DynamoRevitUnitTestBase
    {
        [Test]
        [TestModel(@".\empty.rfa")]
        public void CurveByPoints()
        {
            var model = ViewModel.Model;

            string samplePath = Path.Combine(_testPath, @".\Curve\CurveByPoints.dyn");
            string testPath = Path.GetFullPath(samplePath);

            ViewModel.OpenCommand.Execute(testPath);
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());

            //cerate some points and wire them
            //to the selections
            ReferencePoint p1, p2, p3, p4;

            using (_trans = new Transaction(DocumentManager.Instance.CurrentUIDocument.Document))
            {
                _trans.Start("Create reference points for testing.");

                p1 = DocumentManager.Instance.CurrentUIDocument.Document.FamilyCreate.NewReferencePoint(new XYZ(1, 5, 12));
                p2 = DocumentManager.Instance.CurrentUIDocument.Document.FamilyCreate.NewReferencePoint(new XYZ(5, 1, 12));
                p3 = DocumentManager.Instance.CurrentUIDocument.Document.FamilyCreate.NewReferencePoint(new XYZ(12, 1, 5));
                p4 = DocumentManager.Instance.CurrentUIDocument.Document.FamilyCreate.NewReferencePoint(new XYZ(5, 12, 1));

                _trans.Commit();
            }

            var ptSelectNodes = ViewModel.Model.Nodes.Where(x => x is DSModelElementSelection);
            if (!ptSelectNodes.Any())
                Assert.Fail("Could not find point selection nodes in dynamo graph.");

            ((DSModelElementSelection)ptSelectNodes.ElementAt(0)).SelectedElement = p1.Id;
            ((DSModelElementSelection)ptSelectNodes.ElementAt(1)).SelectedElement = p2.Id;
            ((DSModelElementSelection)ptSelectNodes.ElementAt(2)).SelectedElement = p3.Id;
            ((DSModelElementSelection)ptSelectNodes.ElementAt(3)).SelectedElement = p4.Id;

            ViewModel.Model.RunExpression();

            FilteredElementCollector fec = new FilteredElementCollector(DocumentManager.Instance.CurrentUIDocument.Document);
            fec.OfClass(typeof(CurveElement));

            Assert.AreEqual(fec.ToElements().Count(), 1);

            CurveByPoints mc = (CurveByPoints)fec.ToElements().ElementAt(0);
            Assert.IsTrue(mc.IsReferenceLine);

            //now flip the switch for creating a reference curve
            var boolNode = ViewModel.Model.Nodes.Where(x => x is DSCoreNodesUI.BoolSelector).First();

            ((DSCoreNodesUI.BasicInteractive<bool>)boolNode).Value = false;

            ViewModel.Model.RunExpression();
            Assert.AreEqual(fec.ToElements().Count(), 1);

            mc = (CurveByPoints)fec.ToElements().ElementAt(0);
            Assert.IsFalse(mc.IsReferenceLine);
        }

        [Test]
        [TestModel(@".\empty.rfa")]
        public void CurveLoop()
        {
            string samplePath = Path.Combine(_testPath, @".\Curve\CurveLoop.dyn");
            string testPath = Path.GetFullPath(samplePath);

            ViewModel.OpenCommand.Execute(testPath);
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());
        }

        [Test]
        [TestModel(@".\empty.rfa")]
        public void CurvebyPointsArc()
        {
            string samplePath = Path.Combine(_testPath, @".\Curve\CurvebyPointsArc.dyn");
            string testPath = Path.GetFullPath(samplePath);

            ViewModel.OpenCommand.Execute(testPath);
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());

            FilteredElementCollector fec = new FilteredElementCollector(DocumentManager.Instance.CurrentUIDocument.Document);
            fec.OfClass(typeof(CurveElement));

            Assert.AreEqual(fec.ToElements().Count(), 1);
        }

        [Test]
        [TestModel(@".\empty.rfa")]
        public void OffsetCurve()
        {
            string samplePath = Path.Combine(_testPath, @".\Curve\OffsetCurve.dyn");
            string testPath = Path.GetFullPath(samplePath);

            ViewModel.OpenCommand.Execute(testPath);
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());
        }

        [Test]
        [TestModel(@".\empty.rfa")]
        public void ThickenCurve()
        {
            string samplePath = Path.Combine(_testPath, @".\Curve\ThickenCurve.dyn");
            string testPath = Path.GetFullPath(samplePath);

            ViewModel.OpenCommand.Execute(testPath);
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());
        }

        [Test]
        [TestModel(@".\empty.rfa")]
        public void CurveByPointsByLineNode()
        {
            //this sample creates a geometric line
            //then creates a curve by points from that line

            var model = ViewModel.Model;

            string samplePath = Path.Combine(_testPath, @".\Curve\CurveByPointsByLine.dyn");
            string testPath = Path.GetFullPath(samplePath);

            ViewModel.OpenCommand.Execute(testPath);
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());

            FilteredElementCollector fec = new FilteredElementCollector(DocumentManager.Instance.CurrentUIDocument.Document);
            fec.OfClass(typeof(CurveElement));

            Assert.AreEqual(1, fec.ToElements().Count());

            //now change one of the number inputs and rerun
            //verify that there are still only two reference points in
            //the model
            var node = ViewModel.Model.Nodes.OfType<DoubleInput>().First();
            node.Value = "12.0";

            ViewModel.Model.RunExpression();

            fec = null;
            fec = new FilteredElementCollector(DocumentManager.Instance.CurrentUIDocument.Document);
            fec.OfClass(typeof(CurveElement));
            Assert.AreEqual(1, fec.ToElements().Count);
        }

        /*
        [Test]
        public void ClosedCurve()
        {
            var model = ViewModel.Model;

            string samplePath = Path.Combine(_testPath, @".\Curve\ClosedCurve.dyn");
            string testPath = Path.GetFullPath(samplePath);

            model.Open(testPath);
            ViewModel.Model.RunExpression();

            var extrudeNode = ViewModel.Model.Nodes.First(x => x is CreateExtrusionGeometry);

            var result = (Solid)VisualizationManager.GetDrawablesFromNode(extrudeNode).Values.First();
            double volumeMin = 3850;
            double volumeMax = 4050;
            double actualVolume = result.Volume;
            Assert.Greater(actualVolume, volumeMin);
            Assert.Less(actualVolume, volumeMax);
        }

         * */

        [Test, Category("Failure")]
        [TestModel(@".\empty.rfa")]
        public void CurvebyPointsEllipse()
        {
            string samplePath = Path.Combine(_testPath, @".\Curve\CurvebyPointsEllipse.dyn");
            string testPath = Path.GetFullPath(samplePath);

            ViewModel.OpenCommand.Execute(testPath);
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());


            FilteredElementCollector fec = new FilteredElementCollector(DocumentManager.Instance.CurrentUIDocument.Document);
            fec.OfClass(typeof(CurveElement));

            Assert.AreEqual(fec.ToElements().Count(), 1);

            CurveByPoints mc = (CurveByPoints)fec.ToElements().ElementAt(0);
        }

        [Test]
        [TestModel(@".\Curve\GetCurveDomain.rfa")]
        public void GetCurveDomain()
        {
            string samplePath = Path.Combine(_testPath, @".\Curve\GetCurveDomain.dyn");
            string testPath = Path.GetFullPath(samplePath);

            ViewModel.OpenCommand.Execute(testPath);
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());
        }
    }
}
