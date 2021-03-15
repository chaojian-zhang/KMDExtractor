using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KMDExtractor
{
    internal class Summary
    {
        /// <summary>
        /// Generate summary
        /// </summary>
        public string GetSummary(List<KMDResource> resources)
        {
            var items = resources.SelectMany(r => r.Items);
            // Unique tags
            HashSet<string> uniqueTags = new HashSet<string>(items.SelectMany(i => i.Tags));

            // Generate
            string template =
@"# Summary

Total Tags (Unique): {{tagCount}}<br/>
Total Items: {{itemsCount}}<br/>
Total Fragments: {{fragmentsCount}}<br/>
Unique Tags: {{uniqueTags}}

## Timeline

Recognized Dates: {{datesCount}}";
            string result = template.Replace("{{tagCount}}", uniqueTags.Count().ToString())
                .Replace("{{uniqueTags}}", string.Join(", ", uniqueTags.OrderBy(t => t)))
                .Replace("{{itemsCount}}", items.Count().ToString())
                .Replace("{{fragmentsCount}}", resources.Sum(r => r.Fragments.Count).ToString());
            return result;
        }
    }
}
