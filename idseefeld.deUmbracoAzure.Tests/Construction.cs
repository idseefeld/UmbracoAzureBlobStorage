/*****************************************************************************************************************
 * Tests use the Azure Storage Emulator available in the Azure SDK
 * See http://msdn.microsoft.com/en-us/library/azure/hh403989.aspx
 * 
 * Account name: devstoreaccount1
 * Account key: Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==
 * 
 * http://127.0.0.1:10000/devstoreaccount1/
 * 
 * NB! Current emulator version 3.0 is only compatible with WindowsAzure.Storage 3.x
 * 
 *****************************************************************************************************************/

using Microsoft.WindowsAzure.Storage.Blob;
using NUnit.Framework;

namespace idseefeld.de.UmbracoAzure.Tests
{
    [TestFixture]
    public class Construction : AzureBlobFileSystemTestBase
    {
        protected override void OnSetupComplete()
        {
            Sut = null;
        }

        [Test]
        public void When_No_Container_Creates_Container()
        {
            Assert.IsFalse(Client.GetContainerReference(ContainerName).Exists());
            
            CreateSut();
            
            Assert.IsTrue(Client.GetContainerReference(ContainerName).Exists());
        }

        [Test]
        public void When_Container_Exists_Uses_Container()
        {
            var existingContainer = CreateContainer();
            var directory = existingContainer.GetDirectoryReference("temp");
            var blob = directory.GetBlockBlobReference("temp.dat");
            Assert.IsFalse(blob.Exists());

            blob.UploadText("temp");

            CreateSut();

            Assert.IsTrue(blob.Exists());
        }
    }
}
