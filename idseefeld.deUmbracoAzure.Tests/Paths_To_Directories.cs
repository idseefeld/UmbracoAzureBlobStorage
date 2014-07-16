using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using NUnit.Framework;

namespace idseefeld.de.UmbracoAzure.Tests
{
    [TestFixture]
    public class Paths_To_Directories : AzureBlobFileSystemTestBase
    {
        private const string TempFileName = "temp.dat";
        private CloudBlobContainer container;

        protected override void OnSetupComplete()
        {
            container = CreateContainer();
        }

        [Test]
        public void Full_Url_Has_Relative_Path_Equal_To_Full_Url()
        {
            //const string expectedPath = "1000/";
            var fullPath = GetUrl("1000");

            CreateDirectory("1000");

            var actualPath = Sut.GetRelativePath(fullPath);

            Assert.That(actualPath, Is.EqualTo(fullPath));
        }

        [Test]
        public void Has_Full_Path_Equal_To_Full_Url()
        {
            var fullPath = GetUrl("1000");

            CreateDirectory("1000");

            var actualPath = Sut.GetFullPath("1000/");

            Assert.That(actualPath, Is.EqualTo(fullPath));
        }

        [Test]
        public void Has_Url_Equal_To_Full_Url_Without_Trailing_Slash()
        {
            var fullPath = GetUrl("1000").TrimEnd('/');

            CreateDirectory("1000");

            var actualPath = Sut.GetUrl("1000/");

            Assert.That(actualPath, Is.EqualTo(fullPath));
        }

        private CloudBlobDirectory CreateDirectory(string name)
        {
            var directory = GetDirectory(name);
            CreateTempBlob(directory);
            return directory;
        }

        private static CloudBlockBlob CreateTempBlob(CloudBlobDirectory directory)
        {
            var blob = GetTempBlob(directory);
            blob.UploadText("data");
            return blob;
        }

        private CloudBlockBlob GetTempBlob(string directoryName)
        {
            return GetBlob(directoryName, TempFileName);
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

        private static CloudBlockBlob GetTempBlob(CloudBlobDirectory directory)
        {
            return directory.GetBlockBlobReference(TempFileName);
        }
    }
}
