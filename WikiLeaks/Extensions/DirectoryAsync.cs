using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace WikiLeaks.Extensions
{
    public class DirectoryEx
    {
       public static Task<DirectoryInfo> GetParent(String path)
        {
            return Task.Run(() => { return Directory.GetParent(path); });
        }

        public static Task<DirectoryInfo> CreateDirectory(String path)
        {
            return Task.Run(() => { return Directory.CreateDirectory(path); });
        }

        public static Task<DirectoryInfo> CreateDirectory(String path, System.Security.AccessControl.DirectorySecurity directorySecurity)
        {
            return Task.Run(() => { return Directory.CreateDirectory(path, directorySecurity); });
        }

        public static Task<Boolean> Exists(String path)
        {
            return Task.Run(() => { return Directory.Exists(path); });
        }

        public static Task SetCreationTime(String path, DateTime creationTime)
        {
            return Task.Run(() => { Directory.SetCreationTime(path, creationTime); });
        }

        public static Task SetCreationTimeUtc(String path, DateTime creationTimeUtc)
        {
            return Task.Run(() => { Directory.SetCreationTimeUtc(path, creationTimeUtc); });
        }

        public static Task<DateTime> GetCreationTime(String path)
        {
            return Task.Run(() => { return Directory.GetCreationTime(path); });
        }

        public static Task<DateTime> GetCreationTimeUtc(String path)
        {
            return Task.Run(() => { return Directory.GetCreationTimeUtc(path); });
        }

        public static Task SetLastWriteTime(String path, DateTime lastWriteTime)
        {
            return Task.Run(() => { Directory.SetLastWriteTime(path, lastWriteTime); });
        }

        public static Task SetLastWriteTimeUtc(String path, DateTime lastWriteTimeUtc)
        {
            return Task.Run(() => { Directory.SetLastWriteTimeUtc(path, lastWriteTimeUtc); });
        }

        public static Task<DateTime> GetLastWriteTime(String path)
        {
            return Task.Run(() => { return Directory.GetLastWriteTime(path); });
        }

        public static Task<DateTime> GetLastWriteTimeUtc(String path)
        {
            return Task.Run(() => { return Directory.GetLastWriteTimeUtc(path); });
        }

        public static Task SetLastAccessTime(String path, DateTime lastAccessTime)
        {
            return Task.Run(() => { Directory.SetLastAccessTime(path, lastAccessTime); });
        }

        public static Task SetLastAccessTimeUtc(String path, DateTime lastAccessTimeUtc)
        {
            return Task.Run(() => { Directory.SetLastAccessTimeUtc(path, lastAccessTimeUtc); });
        }

        public static Task<DateTime> GetLastAccessTime(String path)
        {
            return Task.Run(() => { return Directory.GetLastAccessTime(path); });
        }

        public static Task<DateTime> GetLastAccessTimeUtc(String path)
        {
            return Task.Run(() => { return Directory.GetLastAccessTimeUtc(path); });
        }

        public static Task<System.Security.AccessControl.DirectorySecurity> GetAccessControl(String path)
        {
            return Task.Run(() => { return Directory.GetAccessControl(path); });
        }

        public static Task<System.Security.AccessControl.DirectorySecurity> GetAccessControl(String path, System.Security.AccessControl.AccessControlSections includeSections)
        {
            return Task.Run(() => { return Directory.GetAccessControl(path, includeSections); });
        }

        public static Task SetAccessControl(String path, System.Security.AccessControl.DirectorySecurity directorySecurity)
        {
            return Task.Run(() => { Directory.SetAccessControl(path, directorySecurity); });
        }

        public static Task<String[]> GetFiles(String path)
        {
            return Task.Run(() => { return Directory.GetFiles(path); });
        }

        public static Task<String[]> GetFiles(String path, String searchPattern)
        {
            return Task.Run(() => { return Directory.GetFiles(path, searchPattern); });
        }

        public static Task<String[]> GetFiles(String path, String searchPattern, SearchOption searchOption)
        {
            return Task.Run(() => { return Directory.GetFiles(path, searchPattern, searchOption); });
        }

        public static Task<String[]> GetDirectories(String path)
        {
            return Task.Run(() => { return Directory.GetDirectories(path); });
        }

        public static Task<String[]> GetDirectories(String path, String searchPattern)
        {
            return Task.Run(() => { return Directory.GetDirectories(path, searchPattern); });
        }

        public static Task<String[]> GetDirectories(String path, String searchPattern, SearchOption searchOption)
        {
            return Task.Run(() => { return Directory.GetDirectories(path, searchPattern, searchOption); });
        }

        public static Task<String[]> GetFileSystemEntries(String path)
        {
            return Task.Run(() => { return Directory.GetFileSystemEntries(path); });
        }

        public static Task<String[]> GetFileSystemEntries(String path, String searchPattern)
        {
            return Task.Run(() => { return Directory.GetFileSystemEntries(path, searchPattern); });
        }

        public static Task<String[]> GetFileSystemEntries(String path, String searchPattern, SearchOption searchOption)
        {
            return Task.Run(() => { return Directory.GetFileSystemEntries(path, searchPattern, searchOption); });
        }

        public static Task<IEnumerable<String>> EnumerateDirectories(String path)
        {
            return Task.Run(() => { return Directory.EnumerateDirectories(path); });
        }

        public static Task<IEnumerable<String>> EnumerateDirectories(String path, String searchPattern)
        {
            return Task.Run(() => { return Directory.EnumerateDirectories(path, searchPattern); });
        }

        public static Task<IEnumerable<String>> EnumerateDirectories(String path, String searchPattern, SearchOption searchOption)
        {
            return Task.Run(() => { return Directory.EnumerateDirectories(path, searchPattern, searchOption); });
        }

        public static Task<IEnumerable<String>> EnumerateFiles(String path)
        {
            return Task.Run(() => { return Directory.EnumerateFiles(path); });
        }

        public static Task<IEnumerable<String>> EnumerateFiles(String path, String searchPattern)
        {
            return Task.Run(() => { return Directory.EnumerateFiles(path, searchPattern); });
        }

        public static Task<IEnumerable<String>> EnumerateFiles(String path, String searchPattern, SearchOption searchOption)
        {
            return Task.Run(() => { return Directory.EnumerateFiles(path, searchPattern, searchOption); });
        }

        public static Task<IEnumerable<String>> EnumerateFileSystemEntries(String path)
        {
            return Task.Run(() => { return Directory.EnumerateFileSystemEntries(path); });
        }

        public static Task<IEnumerable<String>> EnumerateFileSystemEntries(String path, String searchPattern)
        {
            return Task.Run(() => { return Directory.EnumerateFileSystemEntries(path, searchPattern); });
        }

        public static Task<IEnumerable<String>> EnumerateFileSystemEntries(String path, String searchPattern, SearchOption searchOption)
        {
            return Task.Run(() => { return Directory.EnumerateFileSystemEntries(path, searchPattern, searchOption); });
        }

        public static Task<String[]> GetLogicalDrives()
        {
            return Task.Run(() => { return Directory.GetLogicalDrives(); });
        }

        public static Task<String> GetDirectoryRoot(String path)
        {
            return Task.Run(() => { return Directory.GetDirectoryRoot(path); });
        }

        public static Task<String> GetCurrentDirectory()
        {
            return Task.Run(() => { return Directory.GetCurrentDirectory(); });
        }

        public static Task SetCurrentDirectory(String path)
        {
            return Task.Run(() => { Directory.SetCurrentDirectory(path); });
        }

        public static Task Move(String sourceDirName, String destDirName)
        {
            return Task.Run(() => { Directory.Move(sourceDirName, destDirName); });
        }

        public static Task Delete(String path)
        {
            return Task.Run(() => { Directory.Delete(path); });
        }

        public static Task Delete(String path, Boolean recursive)
        {
            return Task.Run(() => { Directory.Delete(path, recursive); });
        }
    }
}
