﻿using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Dynamo.Utilities;
using NUnit.Framework;
using RevitServices.Persistence;
using RTF.Framework;

namespace Dynamo.Tests
{
    [TestFixture]
    class RayBounceTests:DynamoRevitUnitTestBase
    {
        [Test, Category("Failure")]
        [TestModel(@".\RayBounce\RayBounce.rvt")]
        public void RayBounce()
        {
            string samplePath = Path.Combine(_testPath, @".\RayBounce\RayBounce.dyn");
            string testPath = Path.GetFullPath(samplePath);

            ViewModel.OpenCommand.Execute(testPath);
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());

            //ensure that the bounce curve count is the same
            var curveColl = new FilteredElementCollector(DocumentManager.Instance.CurrentUIDocument.Document, DocumentManager.Instance.CurrentUIDocument.ActiveView.Id);
            curveColl.OfClass(typeof(CurveElement));
            Assert.AreEqual(curveColl.ToElements().Count(), 36);
        }
    }
}
