using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace WTGNet_Client.utils
{
    public class TagHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TagHelper));
        private static readonly object padlock = new object();
        private static TagHelper tagHelper;
        public ControlTags TagList { get; set; }

        public TagHelper() {
            // LoadDefaultTags();
        }
        public static TagHelper Inst() {
            if (tagHelper == null) {
                lock (padlock) {
                    if (tagHelper == null) {
                        tagHelper = new TagHelper();
                    }
                }
            }
            return tagHelper;
        }
        /// <summary>
        ///  加载指定的 tag 定义文件,并返回解析结果
        /// </summary>
        /// <param name="tagPath"></param>
        /// <param name="overwrite"> 是否覆盖已存已加载的变量列表</param>
        /// <returns></returns>
        public ControlTags Load(string tagPath) {
            using (StreamReader file = File.OpenText(tagPath)) {
                JsonSerializer serializer = new JsonSerializer();
                ControlTags acquire = (ControlTags)serializer.Deserialize(file, typeof(ControlTags));
                TagList = acquire;
                return acquire;
            }
        }

        public ControlTags LoadDefaultTags() {
            string tagfile = AppCfg.GetStringVaue("tagJson", "config/tags.json");
            string fileName = Path.Combine(Directory.GetCurrentDirectory(), tagfile);
            log.InfoFormat("加载点位文件: {0}",fileName);
            TagList = Load(fileName);
            return TagList;
        }
        public Tag GetTag(string id) {
            if (string.IsNullOrEmpty(id)) {
                return null;
            }
            if (TagList != null && TagList.Tags != null && TagList.Tags.Count > 0) {
                Tag tag = TagList.Tags.Find(t => {
                    return t != null && id == t.Id;
                });
                return tag;
            } else {
                return null;
            }
        }
        public static string resolveTagFilePath(string input) {
            string pattern = @"\$\{\w+\}";
            RegexOptions options = RegexOptions.Multiline | RegexOptions.IgnoreCase;
            foreach (Match m in Regex.Matches(input, pattern, options)) {
                string field = m.Value;
                if (string.IsNullOrEmpty(field)) {
                    continue;
                }
                field = field.Replace("$", "").Replace("{", "").Replace("}", "");
                input = input.Replace(m.Value, AppCfg.GetStringVaue(field));
            }
            return input;
        }
    }
    public class PlcInfo
    {
        public string Id { get; set; }
        public string Ip { get; set; }
        public int Port { get; set; }

    }
    public class Tag
    {
        public string Id { get; set; }
        public string AddrName { get; set; }
        public int Count { get; set; }
        
        /// <summary>
        /// 表示循环读取间隔,-1 表示不循环读取
        /// </summary>
        public int Interval { get; set; }
        
    }
    public class ControlTags
    {
        public PlcInfo Plc { get; set; }
        public List<Tag> Tags { get; set; }
    }
}
