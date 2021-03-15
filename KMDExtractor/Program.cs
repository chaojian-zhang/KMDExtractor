using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KMDExtractor
{
    class Program
    {
        static int Main(string[] args)
        {
            if(args.Length != 3)
            {
                Console.WriteLine(
@"Extract (anchor and filtuer) KMD files.
Command line arguments: 
  FilePath: Path of source KMD file.
  Filter: Main search filters.
  Output: Output path."
);
                return 1;
            }
            else
            {
                try
                {
                    string inputPath = args[0];
                    string filters = args[1];
                    string outputPath = args[2];
                    // Exception
                    if (!File.Exists(inputPath))
                    {
                        Console.WriteLine($"Input file: `{inputPath}` doesn't exit.");
                        return 1;
                    }
                    if (outputPath == inputPath)
                    {
                        Console.WriteLine("Can't write output to the same file as input.");
                        return 1;
                    }

                    // Read in file
                    string originalReference = File.ReadAllText(inputPath);
                    Parser parser = new Parser();
                    List<KMDResource> resources = parser.TryParse(inputPath);

                    // Filter
                    List<TaggedItem> filteredResult = parser.Filter(filters, resources);
                    if (filteredResult == null)
                    {
                        Console.WriteLine($"Can't find items matching filtering criteria: `{filters}`");
                        return 1;
                    }
                    Console.WriteLine($"{filteredResult.Count()} items found that matches specified tags: {filters}.");

                    // Create output folder directory
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                    // Write
                    File.WriteAllText(outputPath, ReproduceContent(originalReference, filteredResult));

                    return 0;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception during execution: {e.Message}");
                    Console.WriteLine(e.StackTrace);
                    return 1;
                }
            }
        }

        private static string ReproduceContent(string originalReference, List<TaggedItem> filteredResult)
        {
            // Get largest comment set to reduce repeating items
            string[] commonTags = filteredResult.Count > 1 ? filteredResult.SelectMany(r => r.Tags).Distinct().ToArray() : new string[] { };
            filteredResult.ForEach(r => commonTags = commonTags.Intersect(r.Tags).ToArray());

            // Generate output
            StringBuilder builder = new StringBuilder($"# Indexed Notes ({string.Join(", ", commonTags)})\n\n");

            // Iterate and show items in hierarchical fashion
            Stack<TaggedItem> last = new Stack<TaggedItem>();
            for (int i = 0; i < filteredResult.Count; i++)
            {
                TaggedItem item = filteredResult[i];
                // Root
                if (item.Parent == null || !filteredResult.Contains(item.Parent))
                {
                    last.Clear();
                    string tagString = string.Join(", ", item.Tags);
                    tagString = string.IsNullOrEmpty(tagString) ? string.Empty : $"({tagString}) ";
                    string formatted = $"* {tagString}{item.Content} " +
                                $"<!-- {item.Children?.Count ?? 0} children; Is {(item.Parent == null ? "not " : "")}a child";
                    builder.Append(formatted);
                }
                // Children
                else
                {
                    if (last.Count == 0)
                        last.Push(item.Parent);
                    else if (item.Parent != last.First())
                    {
                        if (last.Contains(item.Parent))
                            last.Pop();
                        else last.Push(item.Parent);
                    }
                    string tagString = string.Join(", ", item.Tags.Except(commonTags).Except(item.Parent.Tags));
                    tagString = string.IsNullOrEmpty(tagString) ? string.Empty : $"**({tagString})** ";
                    string formatted = $"{new string('\t', last.Count)}* {tagString}{item.Content} " +
                                $"*{item.Children?.Count ?? 0} children; Is {(item.Parent == null ? "not " : "")}a child";
                    builder.Append(formatted);
                }
                // Line number
                int number = originalReference.Substring(0, originalReference.IndexOf(item.Content)).Where(c => c == '\n').Count() + 1;
                builder.Append($" - Line Number: {number} -->");
                builder.Append("\n");
            }
            builder.AppendLine("\n");

            // Fragments
            var fragments = filteredResult.SelectMany(f => f.References ?? new List<Fragment>()).Distinct();
            builder.AppendLine("# LFR\n");
            foreach (var f in fragments)
            {
                builder.AppendLine($"## {f.Name}");
                builder.AppendLine();
                builder.AppendLine(f.Content.Trim());
                builder.AppendLine();
            }

            return builder.ToString();
        }


        public static void EnterInteractive(List<KMDResource> resources)
        {
            bool endSession = false;
            while (!endSession)
            {
                Console.Write("Enter what you would like to do: ");
                string commandString = Console.ReadLine();
                // Parse command string
                string command = commandString.ToLower();
                string parameters = string.Empty;
                int breakIndex = commandString.IndexOf(' ');
                if (breakIndex != -1)
                {
                    command = commandString.Substring(0, breakIndex).ToLower();
                    parameters = commandString.Substring(breakIndex + 1);
                }
            }
        }
    }
}
