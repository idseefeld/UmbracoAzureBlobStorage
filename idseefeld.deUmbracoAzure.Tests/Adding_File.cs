using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using NUnit.Framework;
using Umbraco.Core.Logging;

namespace idseefeld.de.UmbracoAzure.Tests
{
    [TestFixture]
    public class Adding_File : AzureBlobFileSystemTestBase
    {
        private CloudBlobContainer container;

        protected override void OnSetupComplete()
        {
            container = CreateContainer();
        }

        [Test]
        public void Creates_Blob_In_New_Directory()
        {
            var stream = CreateTestStream();

            Sut.AddFile("1000/test.dat", stream);

            var blob = GetBlob("1000", "test.dat");
            Assert.That(blob.Exists());
        }

        [Test]
        public void Creates_Blob_In_Existing_Directory()
        {
            var stream1 = CreateTestStream();
            var stream2 = CreateTestStream();

            Sut.AddFile("1000/test1.dat", stream1);
            Sut.AddFile("1000/test2.dat", stream2);

            var blob = GetBlob("1000", "test2.dat");
            Assert.That(blob.Exists());
        }

        [Test]
        public void Overwrites_File_When_Adding_Existing_File()
        {
            var stream = CreateTestStream();
            Sut.AddFile("1000/test.dat", stream);
            
            stream = CreateTestStream("tst");
            Sut.AddFile("1000/test.dat", stream);

            var blob = GetBlob("1000", "test.dat");
            Assert.That(blob.DownloadText(), Is.EqualTo("tst"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void Overwrites_File_With_Any_Override_Parameter(bool overwrite)
        {
            var stream = CreateTestStream();
            Sut.AddFile("1000/test.dat", stream);

            stream = CreateTestStream("tst");
            Sut.AddFile("1000/test.dat", stream, overwrite);

            var blob = GetBlob("1000", "test.dat");
            Assert.That(blob.DownloadText(), Is.EqualTo("tst"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void Adds_New_File_With_Any_Override_Parameter(bool overwrite)
        {
            var stream = CreateTestStream("tst");
            Sut.AddFile("1000/test.dat", stream, overwrite);

            var blob = GetBlob("1000", "test.dat");
            Assert.That(blob.DownloadText(), Is.EqualTo("tst"));
        }

        [Test]
        public void Creates_New_Directory_At_Root_For_Last_Directory_Before_FileName()
        {
            const string path = "1000/thumbs/test.dat";
            const string sub = "thumbs";

            var stream = CreateTestStream();
            Sut.AddFile(path, stream);

            Assert.That(Sut.DirectoryExists(sub));
        }

        private CloudBlobDirectory GetDirectory(string directoryName)
        {
            return container.GetDirectoryReference(directoryName);
        }

        protected CloudBlockBlob GetBlob(string directoryName, string fileName)
        {
            var directory = GetDirectory(directoryName);
            return directory.GetBlockBlobReference(fileName);
        }
    }
}
