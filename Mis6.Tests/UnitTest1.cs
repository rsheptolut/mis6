using System.Diagnostics;
using Mis6;

namespace Mis6.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void CheckCharmap()
        {
            // Charmap must have 64 characters
            Debug.Assert(System.Charmap.Length == System.ByteRange);

            // Charmap must each character once
            Debug.Assert(System.Charmap.All(c => System.Charmap.Count(cc => cc == c) == 1));
        }
    }
}