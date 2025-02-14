﻿using System.IO;
using Dynamo.Utilities;
using NUnit.Framework;
using RTF.Framework;

namespace Dynamo.Tests
{
    [TestFixture]
    internal class FaceTests : DynamoRevitUnitTestBase
    {
        [Test]
        [TestModel(@".\Face\GetSurfaceDomain.rvt")]
        public void GetSurfaceDomain()
        {
            string samplePath = Path.Combine(_testPath, @".\Face\GetSurfaceDomain.dyn");
            string testPath = Path.GetFullPath(samplePath);

            ViewModel.OpenCommand.Execute(testPath);
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());
        }
    }
}
