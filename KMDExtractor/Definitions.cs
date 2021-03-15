using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KMDExtractor
{
    /// <summary>
    /// Represents a single parsed KMD file
    /// </summary>
    public class KMDResource
    {
        public List<TaggedItem> Items { get; set; }
        public Dictionary<string, Fragment> Fragments { get; set; }

        public KMDResource(List<TaggedItem> items, Dictionary<string, Fragment> fragments)
        {
            Items = items;
            Fragments = fragments;
        }
        public override string ToString()
            => $"{Items.Count} Items; {Fragments.Count} Fragments.";
    }
    /// <summary>
    /// Represents a tagged item
    /// </summary>
    public class TaggedItem
    {
        public string Content { get; set; }
        public string[] Tags { get; set; }
        public List<TaggedItem> Children { get; set; }
        public TaggedItem Parent { get; set; }
        public List<Fragment> References { get; set; }
        public override string ToString()
            => $"({string.Join(", ", Tags)}) {Content} <{Children.Count} Children>";
    }

    public class Fragment
    {
        public string Name { get; set; }
        public string Content { get; set; }
        public List<TaggedItem> Users { get; set; }

        public Fragment(string name, string content, List<TaggedItem> users)
        {
            Name = name;
            Content = content;
            Users = users;
        }
        public override string ToString()
            => $"({Name}) Content Length: {Content.Length}.";
    }
}
