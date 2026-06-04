using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Exact432HzPlayerWindows.ViewModels;

namespace Exact432HzPlayerWindows.Services
{
    public class FolderScanner
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".flac", ".wav", ".m4a", ".aac", ".wma", ".ogg", ".opus", ".ape"
        };

        public static List<string> GetAudioFilesRecursively(string folderPath)
        {
            var files = new List<string>();
            try
            {
                foreach (var file in Directory.EnumerateFiles(folderPath))
                {
                    if (SupportedExtensions.Contains(Path.GetExtension(file)))
                    {
                        files.Add(file);
                    }
                }
                
                foreach (var dir in Directory.EnumerateDirectories(folderPath))
                {
                    files.AddRange(GetAudioFilesRecursively(dir));
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning folder {folderPath}: {ex.Message}");
            }
            return files;
        }

        public static ObservableCollection<NodeViewModel> BuildTree(string rootPath)
        {
            var rootNodes = new ObservableCollection<NodeViewModel>();
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) return rootNodes;

            var rootInfo = new DirectoryInfo(rootPath);
            var rootNode = new NodeViewModel
            {
                Name = rootInfo.Name,
                FullPath = rootInfo.FullName
            };
            
            PopulateChildren(rootNode, rootInfo);
            rootNodes.Add(rootNode);

            return rootNodes;
        }

        private static void PopulateChildren(NodeViewModel node, DirectoryInfo dirInfo)
        {
            try
            {
                foreach (var directory in dirInfo.GetDirectories())
                {
                    var childNode = new NodeViewModel
                    {
                        Name = directory.Name,
                        FullPath = directory.FullName
                    };
                    node.Children.Add(childNode);
                    PopulateChildren(childNode, directory);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories without permission
            }
        }
    }
}
