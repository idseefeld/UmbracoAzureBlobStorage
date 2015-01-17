using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Blob;
using NUnit.Framework;

namespace idseefeld.de.UmbracoAzure.Tests
{
    [TestFixture]
    public class Existing_Directories : AzureBlobFileSystemTestBase
    {
        private const string TempFileName = "temp.dat";
        private CloudBlobContainer container;

        protected override void OnSetupComplete()
        {
            container = CreateContainer();
        }

        [Test]
        public void Can_Be_Enumerated()
        {
            var expectedDirectoryNames = new List<string>{ "1000", "1001" };
            var expectedUrls = expectedDirectoryNames.Select(GetUrl);

            expectedDirectoryNames.ForEach(name => CreateDirectory(name));

            var directories = Sut.GetDirectories("").ToList();

            Assert.That(expectedDirectoryNames.SequenceEqual(directories), String.Join(",", directories));
        }

        [Test]
        public void Can_Be_Deleted()
        {
            const string directoryName = "1000";

            CreateDirectory(directoryName);

            var blob = GetTempBlob(directoryName);
            Assert.That(blob.Exists());

            Sut.DeleteDirectory(directoryName);

            Assert.That(blob.Exists(), Is.False);
        }

        [Test]
        public void Can_Be_Deleted_When_Contains_Subdirectories()
        {
            Can_Be_Deleted_Template(name => Sut.DeleteDirectory(name));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void Can_Be_Deleted_When_Contains_Subdirectories_Using_Any_Recursive_Overload(bool recursive)
        {
            Can_Be_Deleted_Template(name => Sut.DeleteDirectory(name, recursive));
        }

        private void Can_Be_Deleted_Template(Action<string> deleteAction)
        {
            const string rootDirectoryName = "1000";

            var directory = CreateDirectory(rootDirectoryName);
            var subdirectory = directory.GetSubdirectoryReference("subdirectory");
            var subBlob = CreateTempBlob(subdirectory);

            deleteAction(rootDirectoryName);

            Assert.That(subBlob.Exists(), Is.False);
        }

        [Test]
        public void Exists()
        {
            const string name = "1000";
            CreateDirectory(name);
            
            Assert.That(Sut.DirectoryExists(name));
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
