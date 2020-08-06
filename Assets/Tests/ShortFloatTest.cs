using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests
{
    public class ShortFloatTest
    {
        // A Test behaves as an ordinary method
        [Test]
        public void ShortFloatTestSimplePasses()
        {
            float x = -1f;
            byte[] bytes = BitConverter.GetBytes(x);
            byte[] shortFloatBytes = new byte[2];
            Buffer.BlockCopy(bytes, 2, shortFloatBytes, 0, 2);
            
            byte[] resultBytes = new byte[4];
            Buffer.BlockCopy(shortFloatBytes, 0, resultBytes, 2, 2);
            float shortFloat = BitConverter.ToSingle(resultBytes, 0);
            Assert.AreEqual(x, shortFloat);
        }
    }
}
