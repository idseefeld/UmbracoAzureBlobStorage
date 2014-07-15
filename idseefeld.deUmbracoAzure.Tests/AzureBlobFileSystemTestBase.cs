using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using NUnit.Framework;

namespace idseefeld.de.UmbracoAzure.Tests
{
    public class AzureBlobFileSystemTestBase
    {
        protected CloudStorageAccount Account;
        protected CloudBlobClient Client;
        protected AzureBlobFileSystem Sut;
        private const string EmulatorPath = @"C:\Program Files (x86)\Microsoft SDKs\Windows Azure\Storage Emulator\";
        private const string EmulatorExe = "WAStorageEmulator.exe";
        protected const string ContainerName = "media";
        protected const string BlobUrl = "http://127.0.0.1:10000/" + AccountName + "/";
        private const string AccountName = "devstoreaccount1";
        private const string AccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

        [SetUp]
        public void Setup()
        {
            //var clearProcessInfo = new ProcessStartInfo(EmulatorPath + EmulatorExe, "clear")
            //{
            //    CreateNoWindow = true,
            //    UseShellExecute = false,
            //    WorkingDirectory = EmulatorPath
            //};
            //var process = Process.Start(clearProcessInfo);
            //process.WaitForExit();

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

        private void RemoveExistingContainer()
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
                Account,
                ContainerName,
                BlobUrl
                );
        }
    }
}