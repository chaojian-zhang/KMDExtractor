using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KMDExtractor
{
    internal class Parser
    {
        #region Methods
        public List<KMDResource> TryParse(string filePath)
        {
            List<KMDResource> resources = null;

            // Get as folder
            if (!Directory.Exists(filePath))
            {
                if (!File.Exists(filePath))
                    Console.WriteLine($"Specified path `{filePath}` doesn't exist.");
                else
                {
                    var result = ParseFile(filePath);
                    if (result != null)
                        resources = new List<KMDResource>() { result };
                }
            }
            else
                resources = ParseFolder(filePath);
            return resources;
        }
        public List<KMDResource> ParseFolder(string folderPath)
        {
            // Status report
            Console.WriteLine("Enumerate files...");
            // Enumerate all files and fetch relavant ones
            List<KMDResource> resources = new List<KMDResource>();
            foreach ((string Path, string Type) item in EnumerateFolders(folderPath))
            {
                // Status report
                Console.WriteLine($"Process {item} ...");

                // Handle only text files for now
                if (item.Type == "File")
                    resources.Add(ParseFile(item.Path));
            }
            return resources;
        }
        public KMDResource ParseFile(string filepath)
        {
            // Get potential file-wise tags
            string[] tags = new string[] { };
            string filename = Path.GetFileNameWithoutExtension(filepath);
            if (PureFileNameContainsTag(filename))
                tags = PureFileNameExtractTags(filename, out _);

            // Handle only .md files for now
            if (filepath.EndsWith(".k.md"))
            {
                try
                {
                    return ParseMarkdown(filepath, tags);
                }
                catch (Exception e)
                {
                    throw new ApplicationException($"Error encountered while parsing: {e.Message}");
                }
            }
            else
                throw new ArgumentException("File extension is invalid. Require ending with `.k.md`.");
        }
        /// <summary>
        /// Filter all items by keywords (currently interpreted as tags)
        /// </summary>
        public List<TaggedItem> Filter(string parameters, List<KMDResource> resources)
        {
            if (string.IsNullOrEmpty(parameters))
                return null;
            List<TaggedItem> results = new List<TaggedItem>();
            string[] keywords = parameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(k => k.Trim().ToLower()).ToArray();
            foreach (var r in resources)
            {
                IEnumerable<TaggedItem> result = r.Items.Where(i => i.Tags.Intersect(keywords).Count() == keywords.Length);
                results.AddRange(result);
            }
            return results;
        }
        #endregion

        #region Subroutines
        /// <summary>
        /// Parse a single markdown file as KMD - notice all markdowns are assumed KMD
        /// </summary>
        private KMDResource ParseMarkdown(string path, string[] fileTags)
        {
            // State preparation
            string filename = Path.GetFileNameWithoutExtension(path);
            PureFileNameExtractTags(filename, out filename);
            Stack<string[]> prefixTags = new Stack<string[]>();
            void ReinitializePrefixTags()
            {
                prefixTags.Clear();
                if (!filename.StartsWith('@'))
                    prefixTags.Push(fileTags.Append(filename).ToArray());
                else
                    prefixTags.Push(fileTags);
            }
            ReinitializePrefixTags();
            string nextHeaderLevel = "#";
            List<TaggedItem> items = new List<TaggedItem>();
            // Indetation status
            Stack<TaggedItem> parents = new Stack<TaggedItem>();
            TaggedItem current = null;
            string currentIndentation = "";
            // LFR resources
            bool insideFileEndLFRSection = false;
            string lfrName = null;
            Dictionary<string, Fragment> fragments = new Dictionary<string, Fragment>();
            bool insideCodeSection = false;
            string codeSectionMark = null;
            // Enumerate lines
            foreach (var line in File.ReadAllLines(path))
            {
                if (!insideFileEndLFRSection)
                {
                    #region Regular Sections
                    // Collect list lines
                    // Todo: This is not considering cases where *-+ and numbers are mixed, pending
                    if (Regex.IsMatch(line, "(^[\\*\\-\\+] .*)|(^\\d+\\. .*)"))
                    {
                        // Clear indentation
                        parents.Clear();
                        ReinitializePrefixTags();
                        currentIndentation = "";
                        // Parse line
                        current = ParseLine(prefixTags.First(), line, null);
                        items.Add(current);
                    }
                    // Handle indentation
                    else if (Regex.IsMatch(line, "^\\s+(([\\*\\-\\+] .*)|(\\d+\\. .*))"))
                    {
                        if (parents.Count == 0 && current == null) throw new ApplicationException($"Dangling sibling at line: `{line}`.");
                        string white = Regex.Match(line, "^\\s*").Value;
                        string actualLine = line.Substring(white.Length);
                        // Normalize tab white
                        white = white.Replace("\t", "  ");
                        // Progress indentation
                        if (white.Length > currentIndentation.Length)
                        {
                            // Update indentation
                            currentIndentation = white;
                            parents.Push(current);
                            prefixTags.Push(current.Tags);
                            // Parse line
                            current = ParseLine(prefixTags.First(), actualLine, parents.First());
                        }
                        else if (white.Length == currentIndentation.Length)
                            current = ParseLine(prefixTags.First(), actualLine, parents.First());
                        // Revert indentation
                        else if (white.Length < currentIndentation.Length)
                        {
                            // Update indentation
                            currentIndentation = white;
                            parents.Pop();
                            prefixTags.Pop();
                            if (parents.Count == 0)
                                throw new ApplicationException($"Inconsistent indentation at line: `{line}`.");
                            // Parse line
                            current = ParseLine(prefixTags.First(), actualLine, parents.First());
                        }
                        items.Add(current);
                    }
                    // Special handle header
                    else if (line.StartsWith("# General Indexed Note")
                        || line.StartsWith("# General Indexed Notes"))
                        continue;
                    else if (line.StartsWith("# Local Fragment Reference")
                        || line.StartsWith("# Local Fragment References")
                        || line.StartsWith("# LFR"))
                        insideFileEndLFRSection = true;
                    // Collect headers
                    else if (line.StartsWith(nextHeaderLevel))
                    {
                        prefixTags.Push(prefixTags.First().Append(line.Substring(nextHeaderLevel.Length + 1)).ToArray());
                        nextHeaderLevel += '#';
                    }
                    #endregion
                }
                else
                {
                    #region LFR
                    if (!insideCodeSection && line.StartsWith("## "))
                    {
                        lfrName = line.Substring("## ".Length);
                        // A resource that's not referenced
                        if (!fragments.ContainsKey(lfrName))
                            fragments[lfrName] = new Fragment(lfrName, string.Empty, null);
                        continue;
                    }
                    else if (line.StartsWith("```"))
                    {
                        if (insideCodeSection)
                        {
                            insideCodeSection = false;
                            codeSectionMark = null;
                        }
                        else
                        {
                            insideCodeSection = true;
                            codeSectionMark = Regex.Match(line, "^(`+).*").Groups[1].Value;
                        }
                    }
                    if (lfrName != null)
                        fragments[lfrName].Content += line + '\n';
                    #endregion
                }
            }
            // Connect fragments with items
            ConnectFragments(items, fragments);
            return new KMDResource(items, fragments);
        }
        private TaggedItem ParseLine(string[] prefixTags, string line, TaggedItem parent)
        {
            string bullet = Regex.Match(line, "(^[\\*\\-\\+] )|(^\\d+\\. )").Value;
            string content = line.Substring(bullet.Length);
            (string[] Tags, string Remaining) tagContent = MDLineExtractTags(content);
            var newItem = new TaggedItem()
            {
                Content = tagContent.Remaining,
                Tags = prefixTags.Union(tagContent.Tags).Union(parent?.Tags ?? new string[] { }).Distinct().ToArray(),
                Children = null
            };
            if (parent != null)
            {
                if (parent.Children == null)
                    parent.Children = new List<TaggedItem>();
                parent.Children.Add(newItem);
                newItem.Parent = parent;
            }
            return newItem;
        }

        /// <summary>
        /// Enumerate folders the KMD way, and only go into sub folders if they are not consider items in themselves
        /// </summary>
        private IEnumerable<(string Path, string Type)> EnumerateFolders(string folderPath, bool skipChildren = true)
        {
            // Enumerate files
            foreach (var file in Directory.EnumerateFiles(folderPath))
                yield return (Path: file, Type: "File");

            // Enumerate sub folders
            if (!skipChildren)
            {
                foreach (var subfolder in Directory.EnumerateDirectories(folderPath))
                {
                    // Exclude folders that are considered items
                    if (!PureFileNameContainsTag(Path.GetFileName(subfolder)))
                    {
                        foreach (var item in EnumerateFolders(subfolder, skipChildren))
                            yield return item;
                    }
                    else
                        yield return (Path: subfolder, Type: "Folder");
                }
            }
        }
        /// <summary>
        /// Check whether a filename has any of the accepeted tag attachment (either in prefix  as `(tags)` or `[tags]` or as suffix as `[tags]`)
        /// </summary>
        private bool PureFileNameContainsTag(string filename)
        {
            return filename.StartsWith('[') || filename.StartsWith('(') || filename.EndsWith(']');
        }

        /// <summary>
        /// Extract tags from any of the accepeted tag attachment (either in prefix  as `(tags)` or `[tags]` or as suffix as `[tags]`)
        /// </summary>
        private string[] PureFileNameExtractTags(string filename, out string remainingFilename)
        {
            string tags = Regex.Match(filename, "(^\\(.*?\\))|(^\\[.*?\\])|(\\[.*?\\]$)").Value; // Extract tag part of the string
            if (string.IsNullOrWhiteSpace(tags))
            {
                remainingFilename = filename;
                return new string[] { };
            }
            // Extract values
            string nonTags = filename.Remove(filename.IndexOf(tags), tags.Length).Trim();
            remainingFilename = nonTags;
            tags = tags.Substring(1, tags.Length - 2); // Remove brackets
            if (tags.Contains(','))
                return tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToLower()).ToArray();
            else
                return tags.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToLower()).ToArray();
        }

        /// <summary>
        /// Extract tags from `(tags) Contents....` in a KMD file line
        /// </summary>
        private (string[] Tags, string Remaining) MDLineExtractTags(string lineContent)
        {
            string tags = Regex.Match(lineContent, "^\\(.*?\\)").Value; // Extract tag part of the string
            if (tags.Length != 0 && lineContent != tags) // Tags can be unspecified for a line; Also for some reason we have lines that have only tags without content
            {
                lineContent = lineContent.Substring(tags.Length + 1).Trim();
                tags = tags.Substring(1, tags.Length - 2); // Remove brackets
            }
            return (Tags: tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToLower()).ToArray(), Remaining: lineContent);
        }
        private string ReadResource(string name)
        {
            // Determine path
            var assembly = Assembly.GetExecutingAssembly();
            string resourcePath = name;
            // Format: "{Namespace}.{Folder}.{filename}.{Extension}"
            if (!name.StartsWith(nameof(KMDExtractor)))
            {
                resourcePath = assembly.GetManifestResourceNames()
                    .Single(str => str.EndsWith(name));
            }

            using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
        private void ConnectFragments(List<TaggedItem> items, Dictionary<string, Fragment> fragments)
        {
            // Parse item linkes
            foreach (var item in items)
            {
                // Get all fragment references
                foreach (Match match in Regex.Matches(item.Content, "{{(.*?)}}"))
                {
                    if (fragments.ContainsKey(match.Groups[1].Value))
                    {
                        // Add fragment reference to item
                        Fragment fragment = fragments[match.Groups[1].Value];
                        if (item.References == null)
                            item.References = new List<Fragment>();
                        item.References.Add(fragment);
                        // Add item user to fragment
                        if (fragment.Users == null)
                            fragment.Users = new List<TaggedItem>();
                        fragment.Users.Add(item);
                    }
                }
            }
        }
        #endregion
    }
}
