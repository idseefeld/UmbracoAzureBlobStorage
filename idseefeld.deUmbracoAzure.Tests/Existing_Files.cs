using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace idseefeld.de.UmbracoAzure.Tests
{
    [TestFixture]
    public class Existing_Files : AzureBlobFileSystemTestBase
    {
        private const string TestPath = "1000/test.dat";

        [Test]
        public void Can_Be_Enumerated()
        {
            var paths = new List<string>
            {
                "1001/test1.dat",
                "1001/test2.txt"
            };

            paths.ForEach(path => Sut.AddFile(path, CreateTestStream()));

            var files = Sut.GetFiles("1001/").ToList();
            Assert.That(paths.SequenceEqual(files), String.Join(",", files));
        }

        [Test]
        public void Can_Be_Enumerated_With_Filter_But_Filter_Is_Ignored()
        {
            var paths = new List<string>
            {
                "1001/test1.dat",
                "1001/test2.txt"
            };

            paths.ForEach(path => Sut.AddFile(path, CreateTestStream()));

            var files = Sut.GetFiles("1001/", "*.txt").ToList();
            Assert.That(paths.SequenceEqual(files), String.Join(",", files));
        }

        [Test]
        public void Exists()
        {
            CreateTestFile();

            Assert.That(Sut.FileExists(TestPath));
        }

        [Test]
        public void Can_Be_Opened_But_Stream_Must_Be_Rewinded()
        {
            CreateTestFile();

            var stream = Sut.OpenFile(TestPath);
            stream.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(stream);

            Assert.AreEqual(TestContent, reader.ReadToEnd());
        }

        [Test]
        public void Can_Be_Deleted()
        {
            CreateTestFile();

            Sut.DeleteFile(TestPath);

            Assert.That(Sut.FileExists(TestPath), Is.False);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void Throws_Exception_For_Created_Date()
        {
            CreateTestFile();

            var expectedDate = DateTime.Now.ToUniversalTime();
            var actualDate = Sut.GetCreated(TestPath).DateTime;

            Assert.AreEqual(actualDate, Is.EqualTo(expectedDate).Seconds);
        }

        [Test]
        public void Has_Modified_Date()
        {
            CreateTestFile();

            var expectedDate = DateTime.Now.ToUniversalTime();
            var actualDate = Sut.GetLastModified(TestPath).DateTime;

            Assert.That(actualDate, Is.EqualTo(expectedDate).Within(1).Seconds);
        }

        private void CreateTestFile()
        {
            Sut.AddFile(TestPath, CreateTestStream());
        }
    }
}
