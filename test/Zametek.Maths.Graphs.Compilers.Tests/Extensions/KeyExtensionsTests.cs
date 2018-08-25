using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Zametek.Maths.Graphs.Tests
{
    [TestClass]
    public class KeyExtensionsTests
    {
        [TestMethod]
        public void KeyExtensions_NextInt_ValueIsIncrementedByOne()
        {
            int first = new Random().Next();
            int second = KeyExtensions.NextInt(first);
            Assert.AreEqual(first + 1, second);
        }

        [TestMethod]
        public void KeyExtensions_NextTypeInt_ValueIsIncrementedByOne()
        {
            int first = new Random().Next();
            int second = first.Next();
            Assert.AreEqual(first + 1, second);
        }

        [TestMethod]
        public void KeyExtensions_NextGuid_ValueIsDifferent()
        {
            Guid first = Guid.NewGuid();
            Guid second = KeyExtensions.NextGuid(first);
            Assert.AreNotEqual(first, second);
        }

        [TestMethod]
        public void KeyExtensions_NextTypeGuid_ValueIsDifferent()
        {
            Guid first = Guid.NewGuid();
            Guid second = first.Next();
            Assert.AreNotEqual(first, second);
        }

        [TestMethod]
        public void KeyExtensions_PreviousInt_ValueIsDecrementedByOne()
        {
            int first = new Random().Next();
            int second = KeyExtensions.PreviousInt(first);
            Assert.AreEqual(first - 1, second);
        }

        [TestMethod]
        public void KeyExtensions_PreviousTypeInt_ValueIsDecrementedByOne()
        {
            int first = new Random().Next();
            int second = first.Previous();
            Assert.AreEqual(first - 1, second);
        }

        [TestMethod]
        public void KeyExtensions_PreviousGuid_ValueIsDifferent()
        {
            Guid first = Guid.NewGuid();
            Guid second = KeyExtensions.PreviousGuid(first);
            Assert.AreNotEqual(first, second);
        }

        [TestMethod]
        public void KeyExtensions_PreviousTypeGuid_ValueIsDifferent()
        {
            Guid first = Guid.NewGuid();
            Guid second = first.Previous();
            Assert.AreNotEqual(first, second);
        }
    }
}
