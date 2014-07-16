using System;
using System.IO;
using idseefeld.de.UmbracoAzure.Infrastructure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using NUnit.Framework;
using Rhino.Mocks;

namespace idseefeld.de.UmbracoAzure.Tests
{
    public class AzureBlobFileSystemTestBase
    {
        protected CloudStorageAccount Account;
        protected CloudBlobClient Client;
        protected AzureBlobFileSystem Sut;
        protected const string ContainerName = "media";
        protected const string BlobUrl = "http://127.0.0.1:10000/" + AccountName + "/";
        private const string AccountName = "devstoreaccount1";
        private const string AccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
        protected const string TestContent = "Test";

        [SetUp]
        public void Setup()
        {
            CreateAccount();
            CreateClient();
            RemoveExistingContainer();
            CreateSut();

            OnSetupComplete();
        }

        protected virtual void OnSetupComplete()
        {
        }

        private void CreateAccount()
        {
            Account = new CloudStorageAccount(
                new StorageCredentials(AccountName, AccountKey),
                new Uri(BlobUrl),

                // Not used, just need to specify something
                new Uri(BlobUrl),
                new Uri(BlobUrl)
                );
        }

        private void CreateClient()
        {
            Client = Account.CreateCloudBlobClient();
        }

        protected void RemoveExistingContainer()
        {
            var container = Client.GetContainerReference(ContainerName);
            if (container.Exists())
                container.Delete();
        }

        protected CloudBlobContainer CreateContainer()
        {
            var container = Client.GetContainerReference(ContainerName);
            container.CreateIfNotExists();
            return container;
        }

        protected void CreateSut()
        {
            Sut = new AzureBlobFileSystem(
                MockRepository.GenerateStub<ILogger>(),
                Account,
                ContainerName,
                BlobUrl
                );
        }

        protected static MemoryStream CreateTestStream(string content = TestContent)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(content);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        protected static string GetUrl(string path)
        {
            return String.Format("{0}{1}/{2}/", BlobUrl, ContainerName, path);
        }
    }
}