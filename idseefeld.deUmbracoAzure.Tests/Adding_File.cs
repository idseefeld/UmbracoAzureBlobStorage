using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using NUnit.Framework;

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
        public void Overwrites_Anyway_When_File_Exists()
        {
            var stream = CreateTestStream();
            Sut.AddFile("1000/test.dat", stream);
            Sut.AddFile("1000/test.dat", stream);
        }

        private static MemoryStream CreateTestStream()
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write("Test");
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
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
