using System;
using System.IO;
using HandSeals.Core;
using UnityEngine;

namespace HandSeals.Game
{
    /// <summary>Keeps the player's progress in a JSON file in the app's private data folder.</summary>
    public sealed class JsonProgressRepository : IProgressRepository
    {
        private readonly string _path;

        public JsonProgressRepository(string path = null)
        {
            _path = path ?? Path.Combine(Application.persistentDataPath, "progress.json");
        }

        /// <summary>Loads and repairs the saved progress; never fails, a damaged file gives fresh progress.</summary>
        public PlayerProgress Load()
        {
            PlayerProgress progress = null;

            try
            {
                if (File.Exists(_path))
                {
                    progress = JsonUtility.FromJson<PlayerProgress>(File.ReadAllText(_path));
                }
            }
            catch (Exception exception)
            {
                // A damaged save must not stop the game; start over instead.
                Debug.LogWarning($"Could not read progress from '{_path}': {exception.Message}");
            }

            if (progress == null)
            {
                progress = new PlayerProgress();
            }

            progress.Normalize();
            return progress;
        }

        public void Save(PlayerProgress progress)
        {
            if (progress == null)
            {
                throw new ArgumentNullException(nameof(progress));
            }

            try
            {
                File.WriteAllText(_path, JsonUtility.ToJson(progress, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not save progress to '{_path}': {exception.Message}");
            }
        }
    }
}
