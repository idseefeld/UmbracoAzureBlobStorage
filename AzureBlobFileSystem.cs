/*
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
using System.Web.Caching;
using System.Web;

namespace idseefeld.de.UmbracoAzure
{
    public class AzureBlobFileSystem : IFileSystem
    {
        private class CacheIntHelper
        {
            public int Number { get; set; }
        }
        private string _rootPath;
        private string _rootUrl;
        private CloudBlobClient cloudBlobClient;
        private CloudStorageAccount cloudStorageAccount;
        private CloudBlobContainer mediaContainer;
        private Dictionary<string, string> mimeTypes;
        private Dictionary<string, string> cacheControlSettings;
        private readonly Dictionary<string, CloudBlockBlob> cachedBlobs = new Dictionary<string, CloudBlockBlob>();
        private readonly ILogger logger;

        #region FixDirectoryIssue
        //fix for directory numbering issue
        private readonly int _lastMediaFolderNumberBeforeFix = 0;

        private const string fixDirectoryIssueFile = "fixDirectoryIssueFile.txt";
        private int GetLastMediaFolderNumberBeforeFix()
        {
            string cacheKey = "getLastMediaFolderNumberBeforeFix";
            CacheIntHelper cacheItem = GetItemFromCache<CacheIntHelper>(cacheKey);
            if (cacheItem != null)
            {
                return cacheItem.Number;
            }

            int lastMediaFolderNumber = 0;
            if (FileExists(fixDirectoryIssueFile))
            {
                lastMediaFolderNumber = GetFixDirectoryIssueFileText();
            }
            else
            {

                var allDirectories = mediaContainer.ListBlobs().Where(i => i is CloudBlobDirectory).Select(cd => cd.Uri.Segments[cd.Uri.Segments.Length - 1].Split('/')[0]);
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
                var fixDirectoryIssueFileBlob = mediaContainer.GetBlockBlobReference(fixDirectoryIssueFile);
                fixDirectoryIssueFileBlob.UploadText(lastMediaFolderNumber.ToString());
                fixDirectoryIssueFileBlob.Properties.ContentType = "text/plain";
                fixDirectoryIssueFileBlob.SetProperties();
            }

            cacheItem = new CacheIntHelper() { Number = lastMediaFolderNumber };
            CacheItem<CacheIntHelper>(cacheKey, cacheItem);
            return lastMediaFolderNumber;
        }
        private int GetFixDirectoryIssueFileText()
        {
            int rVal = 0;
            var redirectListBlob = mediaContainer.GetBlockBlobReference(fixDirectoryIssueFile);
            try
            {
                rVal = int.Parse(redirectListBlob.DownloadText());
            }
            catch (Exception ex)
            {
                logger.Error<AzureBlobFileSystem>("GetFixDirectoryIssueFileText reading text", ex);
            }
            return rVal;
        }

        private void CacheItem<T>(string key, T value)
        {
            var uCache = HttpContext.Current.Cache;
            uCache.Add(key, value, null, Cache.NoAbsoluteExpiration, new TimeSpan(1, 0, 0), CacheItemPriority.Default, null);
        }
        private static T GetItemFromCache<T>(string key)
        {
            var uCache = HttpContext.Current.Cache;
            T rVal = (T)uCache.Get(key);
            return rVal;
        }
        #endregion

        public AzureBlobFileSystem(
            string containerName,
            string rootUrl,
            string connectionString)
        {
            Init(containerName, rootUrl, connectionString, null, null);
            logger = new LogAdapter();
            _lastMediaFolderNumberBeforeFix = GetLastMediaFolderNumberBeforeFix();
        }
        public AzureBlobFileSystem(
            string containerName,
            string rootUrl,
            string connectionString,
            string mimetypes)
        {
            Init(containerName, rootUrl, connectionString, mimetypes, null);
            logger = new LogAdapter();
            _lastMediaFolderNumberBeforeFix = GetLastMediaFolderNumberBeforeFix();
        }

        public AzureBlobFileSystem(
            string containerName,
            string rootUrl,
            string connectionString,
            string mimetypes,
            string cacheControl)
        {
            Init(containerName, rootUrl, connectionString, mimetypes, cacheControl);
            logger = new LogAdapter();
            _lastMediaFolderNumberBeforeFix = GetLastMediaFolderNumberBeforeFix();
        }
        private void Init(string containerName, string rootUrl, string connectionString, string mimetypes, string cacheControl)
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
            if (cacheControl != null)
            {
                var pairs = cacheControl.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                this.cacheControlSettings = new Dictionary<string, string>();
                foreach (var pair in pairs)
                {
                    string[] type = pair.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    if (type.Length == 2)
                    {
                        string value = CheckCacheControlSetting(type[1]);
                        if (!String.IsNullOrEmpty(value))
                        {
                            this.cacheControlSettings.Add(type[0], value);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// validate a cache-control setting
        /// see: https://developers.google.com/web/fundamentals/performance/optimizing-content-efficiency/http-caching
        /// </summary>
        /// <param name="setting">cache-control setting</param>
        /// <returns>valid cache-control setting or null</returns>
        private string CheckCacheControlSetting(string setting)
        {
            var validPrefixes = "public|privat|no-store|no-cache".Split('|');
            string rVal = null;
            try
            {
                bool valid = false;
                var parts = setting.ToLower().Split(',');
                if (parts.Length > 1)
                {
                    if (validPrefixes.Contains(parts[0])
                        && parts[1].Contains("=")
                        && parts[1].Trim().StartsWith("max-age"))
                    {
                        int seconds = -1;
                        if (int.TryParse(parts[1].Split('=')[1], out seconds))
                        {
                            valid = true;
                        }
                    }
                }
                else
                {
                    if (validPrefixes.Contains(parts[0]))
                    {
                        valid = true;
                    }
                    else if (parts[0].Contains("=")
                        && parts[0].StartsWith("max-age"))
                    {
                        int seconds = -1;
                        if (int.TryParse(parts[0].Split('=')[1], out seconds))
                        {
                            valid = true;
                        }
                    }
                }
                if (valid)
                {
                    rVal = setting.ToLower();
                }
            }
            catch (Exception ex)
            {
                logger.Error<AzureBlobFileSystem>(ex.Message, ex);
            }
            return rVal;
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

            var segments = path.Replace('\\', '/').Split('/');
            string dir = segments[segments.Length - 1];
            int dirNumber = 0;
            if (int.TryParse(dir, out dirNumber)
                && dirNumber <= _lastMediaFolderNumberBeforeFix)
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
            return GetLastModifiedDateOfBlob(path);
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
            Stream rVal = DownloadFileFromBlob(path);
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
            var blockBlob = GetBlockBlob(MakeUri(path));
            var fileStream = new MemoryStream();
            blockBlob.DownloadToStream(fileStream);
            if (fileStream.CanSeek)
            {
                fileStream.Seek(0, SeekOrigin.Begin);
            }
            return fileStream;
        }

        private bool FileExistsInBlob(string path)
        {
            try
            {
                var blob = GetBlockBlob(MakeUri(path));
                return blob.Exists();
            }
            catch (Exception ex)
            {
                return false;
            }
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
            return rVal;
        }

        private IEnumerable<string> GetFilesFromBlob(string path, string filter)
        {
            //TODO: Filter einbinden.
            var blobs = mediaContainer.ListBlobs(path);
            return blobs.Where(i => i is CloudBlockBlob).Select(cd =>
                {
                    var cloudBlockBlob = cd as CloudBlockBlob;
                    //Filter vielleicht über den Namen.
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
            string cacheControl = GetCacheControlByFileType(name);
            if (!String.IsNullOrEmpty(contentType)
                || !String.IsNullOrEmpty(cacheControl))
            {
                if (!String.IsNullOrEmpty(contentType))
                    blockBlob.Properties.ContentType = contentType;
                if (!String.IsNullOrEmpty(cacheControl))
                    blockBlob.Properties.CacheControl = cacheControl;
                blockBlob.SetProperties();
            }
        }
        private string GetCacheControlByFileType(string name)
        {
            string rVal = null;
            if (this.cacheControlSettings.Count == 0)
                return rVal;

            var wildcard = "*";
            string ext = name.Substring(name.LastIndexOf('.') + 1).ToLower();
            if (this.cacheControlSettings.ContainsKey(ext))
            {
                rVal = this.cacheControlSettings[ext];
            }
            else if (this.cacheControlSettings.ContainsKey(wildcard))
            {
                rVal = this.cacheControlSettings[wildcard];
            }
            return rVal;
        }
        private string GetMimeType(string name)
        {
            string ext = name.Substring(name.LastIndexOf('.')).ToLower();
            string mimeType = MimeMapping.GetMimeMapping(ext);
            return mimeType;
        }
    }
}