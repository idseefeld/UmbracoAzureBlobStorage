/*
include this file into project instead of AzureBlobFileSystem.cs
This code was inspired by Johannes Mueller
*/

using idseefeld.de.UmbracoAzure.Infrastructure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.IO;
using System.Text;

namespace idseefeld.de.UmbracoAzure
{
    public class AzureBlobFileSystem : IFileSystem
    {
        private string _rootPath;
        private string _rootUrl;
        private CloudBlobClient cloudBlobClient;
        private CloudStorageAccount cloudStorageAccount;
        private CloudBlobContainer mediaContainer;
        private Dictionary<string, string> mimeTypes;
        private readonly Dictionary<string, CloudBlockBlob> cachedBlobs = new Dictionary<string, CloudBlockBlob>();
        private readonly ILogger logger;
        private const string redirectFilePath = "redirectListing";

        public AzureBlobFileSystem(
            string containerName,
            string rootUrl,
            string connectionString)
        {
            logger = new LogAdapter();
            Init(containerName, rootUrl, connectionString, null);
        }
        public AzureBlobFileSystem(
            string containerName,
            string rootUrl,
            string connectionString,
            string mimetypes)
        {
            logger = new LogAdapter();
            Init(containerName, rootUrl, connectionString, mimetypes);
        }
        private void Init(string containerName, string rootUrl, string connectionString, string mimetypes)
        {
            cloudStorageAccount = CloudStorageAccount.Parse(connectionString);
            cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            mediaContainer = CreateContainer(containerName, BlobContainerPublicAccessType.Blob);
            RootUrl = rootUrl + containerName + "/";
            RootPath = "/";
            if (mimetypes != null)
            {
                var pairs = mimetypes.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                this.mimeTypes = new Dictionary<string, string>();
                foreach (var pair in pairs)
                {
                    string[] type = pair.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    if (type.Length == 2)
                    {
                        this.mimeTypes.Add(type[0], type[1]);
                    }
                }
            }
            FixDirectoryIssue();
        }

        internal AzureBlobFileSystem(
            ILogger logger,
            CloudStorageAccount account,
            string containerName,
            string rootUrl)
        {
            cloudStorageAccount = account;
            cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            mediaContainer = CreateContainer(containerName, BlobContainerPublicAccessType.Blob);
            RootUrl = rootUrl + containerName + "/";
            RootPath = "/";

            this.logger = logger;
        }

        private string RootPath
        {
            get { return _rootPath; }
            set { _rootPath = value; }
        }

        private string RootUrl
        {
            get { return _rootUrl; }
            set { _rootUrl = value; }
        }

        public void AddFile(string path, Stream stream, bool overrideIfExists)
        {
            var fileExists = FileExists(path);
            if (fileExists && !overrideIfExists)
            {
                logger.Warn<AzureBlobFileSystem>(string.Format("A file at path '{0}' already exists", path));
            }
            AddFile(path, stream);
        }

        public void AddFile(string path, Stream stream)
        {
            if (!path.StartsWith(RootUrl))
            {
                path = RootUrl + path.Replace('\\', '/');
            }
            UploadFileToBlob(path, stream);
        }


        public void DeleteDirectory(string path, bool recursive)
        {
            if (!DirectoryExists(path))
                return;

            DeleteDirectoryInBlob(path);
        }

        public void DeleteDirectory(string path)
        {
            DeleteDirectory(path, false);
        }

        public void DeleteFile(string path)
        {
            DeleteFileFromBlob(path);
        }

        public bool DirectoryExists(string path)
        {
            return DirectoryExistsInBlob(path);
        }

        public bool FileExists(string path)
        {
            bool rVal = FileExistsInBlob(path);
            if (!rVal)
            {
                string redirectPath = GetRedirectPath(path);
                if (!String.IsNullOrEmpty(redirectPath))
                {
                    rVal = FileExistsInBlob(redirectPath);
                }
            }
            return rVal;
        }

        public DateTimeOffset GetCreated(string path)
        {
            return DirectoryExists(path)
                ? Directory.GetCreationTimeUtc(GetFullPath(path))
                : File.GetCreationTimeUtc(GetFullPath(path));
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            return GetDirectoriesFromBlob(path);
        }

        public IEnumerable<string> GetFiles(string path, string filter)
        {
            return GetFilesFromBlob(path, filter);
        }

        public IEnumerable<string> GetFiles(string path)
        {
            var rVal = GetFiles(path, "*.*");
            return rVal;
        }

        public string GetFullPath(string path)
        {
            string rVal = !path.StartsWith(RootUrl)
                 ? Path.Combine(RootUrl, path)
                 : path;
            return rVal;
        }

        public DateTimeOffset GetLastModified(string path)
        {
            DateTimeOffset rVal;
            string redirectPath = GetRedirectPath(path);
            if (!String.IsNullOrEmpty(redirectPath) && FileExistsInBlob(redirectPath))
            {
                rVal = GetLastModifiedDateOfBlob(redirectPath);
            }
            else
            {
                rVal = GetLastModifiedDateOfBlob(path);
            }
            return rVal;
        }

        public string GetRelativePath(string fullPathOrUrl)
        {
            var relativePath = fullPathOrUrl;
            if (!fullPathOrUrl.StartsWith("http"))
            {
                fullPathOrUrl
                 .TrimStart(_rootUrl)
                 .Replace('/', Path.DirectorySeparatorChar)
                 .TrimStart(Path.DirectorySeparatorChar);
            }
            return relativePath;
        }

        public string GetUrl(string path)
        {
            string rVal = path;
            if (!path.StartsWith("http"))
            {
                rVal = RootUrl.TrimEnd("/") + "/" + path
                     .TrimStart(Path.DirectorySeparatorChar)
                     .Replace(Path.DirectorySeparatorChar, '/')
                     .TrimEnd("/");
            }
            return rVal;
        }

        public Stream OpenFile(string path)
        {
            Stream rVal = null;
            string redirectPath = GetRedirectPath(path);
            if (!String.IsNullOrEmpty(redirectPath) && FileExistsInBlob(redirectPath))
            {
                rVal = DownloadFileFromBlob(redirectPath);
            }
            else
            {
                rVal = DownloadFileFromBlob(path);
            }
            return rVal;
        }

        private CloudBlobContainer CreateContainer(string containerName, BlobContainerPublicAccessType accessType)
        {
            var container = GetContainer(containerName);
            container.CreateIfNotExists();
            container.SetPermissions(new BlobContainerPermissions { PublicAccess = accessType });
            return container;
        }

        private CloudBlobDirectory CreateDirectories(string[] paths)
        {
            var current = mediaContainer.GetDirectoryReference(paths[0]);
            for (var i = 1; i < paths.Count(); i++)
            {
                current = current.GetDirectoryReference(paths[i]);
            }
            return current;
        }
        private void DeleteDirectoryInBlob(string path)
        {
            var blobs = GetDirectoryBlobs(path);
            foreach (var item in blobs)
            {
                if (item is CloudBlockBlob || item.GetType().BaseType == typeof(CloudBlockBlob))
                {
                    ((CloudBlockBlob)item).DeleteIfExists();
                }
            }
        }

        private void DeleteFileFromBlob(string path)
        {
            path = path.Replace('\\', '/');
            string redirectPath = GetRedirectPath(path);
            if (!String.IsNullOrEmpty(redirectPath) && FileExistsInBlob(redirectPath))
            {
                path = redirectPath;
            }
            try
            {
                var blockBlob = GetBlockBlob(MakeUri(path));
                blockBlob.Delete();
            }
            catch (Exception ex)
            {
                logger.Error<AzureBlobFileSystem>("Delete File Error: " + path, ex);
            }
        }

        private bool DirectoryExistsInBlob(string path)
        {
            var blobs = GetDirectoryBlobs(path);
            bool rVal = blobs.Any();
            return rVal;
        }
        private IEnumerable<IListBlobItem> GetDirectoryBlobs(string path, bool useFlatBlobListing = true)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            string dir = path.Substring(path.LastIndexOf('/') + 1);
            return mediaContainer.ListBlobs(dir, useFlatBlobListing);
        }

        private Stream DownloadFileFromBlob(string path)
        {
            var fileStream = new MemoryStream();
            CloudBlockBlob blockBlob;
            string redirectPath = GetRedirectPath(path);
            if (!String.IsNullOrEmpty(redirectPath) && FileExistsInBlob(redirectPath))
            {
                blockBlob = GetBlockBlob(MakeUri(redirectPath));
            }
            else
            {
                blockBlob = GetBlockBlob(MakeUri(path));
            }
            blockBlob.DownloadToStream(fileStream);
            return fileStream;
        }

        private bool FileExistsInBlob(string path)
        {
            bool rVal = false;
            try
            {
                var blob = GetBlockBlob(MakeUri(path));
                rVal = blob.Exists();
            }
            catch (Exception ex)
            {
            }
            return rVal;
        }

        private CloudBlockBlob GetBlockBlob(Uri uri)
        {
            CloudBlockBlob blockBlob;
            if (!cachedBlobs.TryGetValue(uri.ToString(), out blockBlob))
            {
                blockBlob = cloudBlobClient.GetBlobReferenceFromServer(uri) as CloudBlockBlob;
                cachedBlobs.Add(uri.ToString(), blockBlob);
            }
            if (blockBlob == null)
            {
                logger.Warn<AzureBlobFileSystem>("File not found in BLOB: " + uri.AbsoluteUri);
            }
            return blockBlob;
        }
        private CloudBlobContainer GetContainer(string containerName)
        {
            return cloudBlobClient.GetContainerReference(containerName);
        }

        private IEnumerable<string> GetDirectoriesFromBlob(string path)
        {
            var blobs = mediaContainer.ListBlobs(path);
            //always get last segment for media sub folder simulation
            var rVal = blobs.Where(i => i is CloudBlobDirectory).Select(cd => cd.Uri.Segments[cd.Uri.Segments.Length - 1].Split('/')[0]);
            //rVal = blobs.Where(i => i is CloudBlobDirectory).Select(cd => cd.Uri.ToString());
            return rVal;
        }

        private IEnumerable<string> GetFilesFromBlob(string path, string filter = null)
        {
            //TODO: implement filter.
            var blobs = mediaContainer.ListBlobs(path);
            return blobs.Where(i => i is CloudBlockBlob).Select(cd =>
                {
                    var cloudBlockBlob = cd as CloudBlockBlob;
                    //use filter on name???
                    return cloudBlockBlob.Name;
                });
        }

        private DateTimeOffset GetLastModifiedDateOfBlob(string path)
        {
            var blob = GetBlockBlob(MakeUri(path));
            var lastmodified = blob.Properties.LastModified;
            return lastmodified.GetValueOrDefault();
        }

        private string MakePath(string path)
        {
            string rVal = path;
            if (!path.StartsWith("http"))
                rVal = RootUrl + path.Replace('\\', '/');
            return rVal;
        }

        private Uri MakeUri(string path)
        {
            return new Uri(MakePath(path));
        }

        private void UploadFileToBlob(string fileUrl, Stream fileStream)
        {
            string name = fileUrl.Substring(fileUrl.LastIndexOf('/') + 1);
            var dirPart = fileUrl.Substring(0, fileUrl.LastIndexOf('/'));
            dirPart = dirPart.Substring(dirPart.LastIndexOf('/') + 1);
            var directory = CreateDirectories(dirPart.Split('/'));
            var blockBlob = directory.GetBlockBlobReference(name);
            if (fileStream.CanSeek)
            {
                fileStream.Seek(0, SeekOrigin.Begin);
            }
            blockBlob.UploadFromStream(fileStream);
            string contentType = GetMimeType(name);
            if (!String.IsNullOrEmpty(contentType))
            {
                blockBlob.Properties.ContentType = contentType;
                blockBlob.SetProperties();
            }
        }

        private string GetMimeType(string name)
        {
            string rVal = null;
            string ext = name.Substring(name.LastIndexOf('.') + 1).ToLower();
            if (this.mimeTypes != null)
            {
                var type = this.mimeTypes.Where(t => t.Key.Equals(ext)).FirstOrDefault();
                if (!String.IsNullOrEmpty(type.Value))
                {
                    rVal = type.Value;
                }
            }
            if (String.IsNullOrEmpty(rVal))
            {
                switch (ext)
                {
                    case "jpg":
                    case "jpeg":
                        rVal = "image/jpeg";
                        break;
                    case "png":
                        rVal = "image/png";
                        break;
                    case "gif":
                        rVal = "image/gif";
                        break;
                    case "pdf":
                        rVal = "application/pdf";
                        break;
                    case "air":
                        rVal = "application/vnd.adobe.air-application-installer-package+zip";
                        break;
                    default:
                        break;
                }
            }
            return rVal;
        }

        private void FixDirectoryIssue()
        {
            if (FileExists(redirectFilePath))
                return;

            var allDirectories = mediaContainer.ListBlobs().Where(i => i is CloudBlobDirectory).Select(cd => cd.Uri.Segments[cd.Uri.Segments.Length - 1].Split('/')[0]);
            int lastMediaFolderNumber = 0;
            foreach (var dir in allDirectories)
            {
                int dirNumber = 0;
                if (int.TryParse(dir, out dirNumber))
                {
                    if (dirNumber > lastMediaFolderNumber)
                    {
                        lastMediaFolderNumber = dirNumber;
                    }
                }
            }
            if (lastMediaFolderNumber > 0)
            {
                StringBuilder sb = new StringBuilder();
                string blobPrefix = null;
                bool useFlatBlobListing = true;
                var allBlobs = mediaContainer.ListBlobs(blobPrefix, useFlatBlobListing).ToList();
                var allThumbs = allBlobs.Where(b => b.Uri.AbsolutePath.Contains("_thumb."));
                List<IListBlobItem> images = new List<IListBlobItem>();
                foreach (var thumb in allThumbs)
                {
                    lastMediaFolderNumber++;
                    string imgRoot = thumb.Uri.AbsolutePath.Substring(0, thumb.Uri.AbsolutePath.LastIndexOf("_thumb."));
                    var siblings = allBlobs.Where(b => b.Uri.AbsolutePath.StartsWith(imgRoot));
                    foreach (var sibling in siblings)
                    {
                        if (sibling is CloudBlockBlob)
                        {
                            images.Add(sibling);
                            string oldPath = null;
                            oldPath = ((CloudBlockBlob)sibling).Name;
                            string[] parts = oldPath.Split('/');
                            string newPath = null;
                            if (parts.Length == 2)
                            {
                                newPath = String.Format("{0}/{1}", lastMediaFolderNumber, parts[1]);
                            }
                            else
                            {
                                parts[0] = lastMediaFolderNumber.ToString();
                                newPath = String.Join("/", parts);
                            }
                            RenameBlob(oldPath, newPath);
                            sb.AppendFormat("{0}|{1}\n", oldPath, newPath);
                        }
                    }
                }
                var remainingBlobs = allBlobs.Where(b => !images.Contains(b));
                foreach (var blob in remainingBlobs)
                {
                    lastMediaFolderNumber++;
                    string oldPath = null;
                    if (blob is CloudBlockBlob)
                    {
                        oldPath = ((CloudBlockBlob)blob).Name;
                        string[] parts = oldPath.Split('/');
                        string newPath = null;
                        if (parts.Length == 2)
                        {
                            newPath = String.Format("{0}/{1}", lastMediaFolderNumber, parts[1]);
                        }
                        else
                        {
                            parts[0] = lastMediaFolderNumber.ToString();
                            newPath = String.Join("/", parts);
                        }
                        RenameBlob(oldPath, newPath);
                        sb.AppendFormat("{0}|{1}\n", oldPath, newPath);
                    }
                }

                var redirectListBlob = mediaContainer.GetBlockBlobReference(redirectFilePath);
                redirectListBlob.UploadText(sb.ToString());
            }
        }

        private string GetBlobNameFromPath(string fullPathOrUrl)
        {
            var relativePath = fullPathOrUrl;
            if (!String.IsNullOrEmpty(fullPathOrUrl) 
                && fullPathOrUrl.StartsWith("http"))
            {
                relativePath = fullPathOrUrl
                 .TrimStart(_rootUrl);
            }
            return relativePath;
        }
        private string GetRedirectPath(string fullPath)
        {
            string relativePath = GetBlobNameFromPath(fullPath);
            string rVal = null;
            string text = GetRedirectFileText();
            if (String.IsNullOrEmpty(text))
                return rVal;

            string[] textLines = text.Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            string textLine = textLines.Where(t => t.StartsWith(relativePath)).FirstOrDefault();
            if (!String.IsNullOrEmpty(textLine))
            {
                var parts = textLine.Split('|');
                if (parts.Length == 2)
                {
                    rVal = GetFullPath(parts[1]);
                }
            }
            return rVal;
        }
        private string GetRedirectFileText()
        {
            string rVal = null;
            var redirectListBlob = mediaContainer.GetBlockBlobReference(redirectFilePath);
            try
            {
                rVal = redirectListBlob.DownloadText();
            }
            catch (Exception ex)
            {
                logger.Error<AzureBlobFileSystem>("GetRedirectFileText reading text", ex);
            }
            return rVal;
        }
        private void RenameBlob(string path, string newPath)
        {
            var oldBlob = this.mediaContainer.GetBlockBlobReference(path);
            if (FileExists(newPath))
                return;

            CloudBlockBlob newBlob = null;
            try
            {
                newBlob = this.mediaContainer.GetBlockBlobReference(newPath);
                if (newBlob != null)
                {
                    newBlob.StartCopyFromBlob(MakeUri(path));
                    //Now wait in the loop for the copy operation to finish
                    while (true)
                    {
                        newBlob.FetchAttributes();
                        if (newBlob.CopyState.Status != CopyStatus.Pending)
                        {
                            break;
                        }
                        //Sleep for a second may be
                        System.Threading.Thread.Sleep(1000);
                    }
                    oldBlob.Delete();
                }
            }
            catch
            {
                if (newBlob != null)
                {
                    newBlob.Delete();
                }
            }
        }

    }
}