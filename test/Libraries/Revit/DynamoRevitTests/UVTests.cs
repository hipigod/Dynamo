﻿using System.IO;
using Dynamo.Utilities;
using NUnit.Framework;
using RTF.Framework;

namespace Dynamo.Tests
{
    [TestFixture]
    internal class UVTests : DynamoRevitUnitTestBase
    {
        [Test]
        [TestModel(@".\empty.rfa")]
        public void UVRandom()
        {
            string samplePath = Path.Combine(_testPath, @".\UV\UVRandom.dyn");
            string testPath = Path.GetFullPath(samplePath);

            ViewModel.OpenCommand.Execute(testPath);
            Assert.DoesNotThrow(() => ViewModel.Model.RunExpression());
        }
    }
}
