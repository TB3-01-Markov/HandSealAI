using System;
using System.Collections.Generic;
using HandSeals.Core;
using UnityEngine;

namespace HandSeals.Game
{
    /// <summary>Reads the techniques from a JSON text asset in a Resources folder.</summary>
    public sealed class JsonJutsuCatalog : IJutsuCatalog
    {
        public const string DefaultResourcePath = "Data/jutsu";

        private readonly List<Jutsu> _all = new List<Jutsu>();

        public JsonJutsuCatalog(string json)
        {
            JutsuFile file = JsonUtility.FromJson<JutsuFile>(json);
            if (file == null || file.jutsu == null)
            {
                throw new ArgumentException("The jutsu file has no 'jutsu' array.", nameof(json));
            }

            foreach (JutsuEntry entry in file.jutsu)
            {
                _all.Add(Jutsu.Create(entry.id, entry.name, entry.seals, entry.timeLimitSeconds, entry.baseScore));
            }
        }

        public static JsonJutsuCatalog LoadFromResources(string resourcePath = DefaultResourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException($"Jutsu file 'Resources/{resourcePath}.json' was not found.");
            }

            return new JsonJutsuCatalog(asset.text);
        }

        public IReadOnlyList<Jutsu> GetAll()
        {
            return _all;
        }

        // Shapes of the JSON file; JsonUtility needs public fields and fills them itself,
        // which the compiler cannot see (hence the suppressed "never assigned" warning).
#pragma warning disable 0649
        [Serializable]
        private sealed class JutsuFile
        {
            public JutsuEntry[] jutsu;
        }

        [Serializable]
        private sealed class JutsuEntry
        {
            public string id;
            public string name;
            public string[] seals;
            public float timeLimitSeconds;
            public int baseScore;
        }
#pragma warning restore 0649
    }
}
