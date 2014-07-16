using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace idseefeld.de.UmbracoAzure.Tests
{
    [TestFixture]
    public class Non_Existing_Directories : AzureBlobFileSystemTestBase
    {
        [Test]
        public void Does_Not_Exist()
        {
            Assert.That(Sut.DirectoryExists("1000"), Is.False);
        }

        [Test]
        public void Can_Be_Deleted()
        {
            Sut.DeleteDirectory("1000");
        }
    }
}
