using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace kcode.website_downloader
{
    class Cache
    {
        public enum Type
        {
            KnownGlobal,
            KnownLocal,
            Handled,
            WrittenFiles,
            Redirects,
        }

        private IFormatter BinaryFormatter = new BinaryFormatter();

        public void Read<T>(Type type, out T target) where T : class, new()
        {
            var filepath = GetFilename(type);
            if (!File.Exists(filepath))
            {
                target =  new T();
                return;
            }

            using var stream = OpenRead(filepath);
            target = (T)BinaryFormatter.Deserialize(stream);
        }

        public void Write<T>(Type type, T list) where T : class
        {
            var filename = GetFilename(type);
            (new FileInfo(filename)).Directory.Create();
            using var stream = OpenWrite(filename);
            BinaryFormatter.Serialize(stream, list);
        }

        private string GetFilename(Type type) => Path.Combine("cache", Enum.GetName(typeof(Type), type));
        private Stream OpenRead(string filepath) => Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
        private Stream OpenWrite(string filepath) => Open(filepath, FileMode.Create, FileAccess.Write, FileShare.None);
        private Stream Open(string filepath, FileMode fm, FileAccess fa, FileShare fs) => new FileStream(filepath, fm, fa, fs);
    }
}
