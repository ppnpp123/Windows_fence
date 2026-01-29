using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace NoFences.Model
{
    public class FenceManager
    {
        public static FenceManager Instance { get; } = new FenceManager();

        private const string MetaFileName = "__fence_metadata.xml";

        private readonly string basePath;
        private readonly List<FenceWindow> openFences = new List<FenceWindow>();
        private readonly object fencesLock = new object();

        public IReadOnlyList<FenceWindow> OpenFences
        {
            get
            {
                lock (fencesLock)
                {
                    return openFences.ToList().AsReadOnly();
                }
            }
        }

        public event EventHandler FencesChanged;

        public FenceManager()
        {
            basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoFences");
            EnsureDirectoryExists(basePath);
        }

        public void LoadFences()
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(basePath))
                {
                    try
                    {
                        var metaFile = Path.Combine(dir, MetaFileName);
                        if (!File.Exists(metaFile))
                            continue;

                        var serializer = new XmlSerializer(typeof(FenceInfo));
                        using (var reader = new StreamReader(metaFile))
                        {
                            var fence = serializer.Deserialize(reader) as FenceInfo;
                            if (fence != null)
                            {
                                var window = new FenceWindow(fence);
                                RegisterFenceWindow(window);
                                window.Show();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading fence from {dir}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading fences: {ex.Message}");
            }
        }

        public void CreateFence(string name)
        {
            try
            {
                var fenceInfo = new FenceInfo(Guid.NewGuid())
                {
                    Name = name,
                    PosX = 100,
                    PosY = 250,
                    Height = 300,
                    Width = 300
                };

                UpdateFence(fenceInfo);
                var window = new FenceWindow(fenceInfo);
                RegisterFenceWindow(window);
                window.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating fence: {ex.Message}");
            }
        }

        private void RegisterFenceWindow(FenceWindow window)
        {
            lock (fencesLock)
            {
                openFences.Add(window);
            }
            window.FormClosed += (s, e) =>
            {
                lock (fencesLock)
                {
                    openFences.Remove(window);
                }
                try
                {
                    FencesChanged?.Invoke(this, EventArgs.Empty);
                }
                catch { }
            };
            try
            {
                FencesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }

        public void RemoveFence(FenceInfo info)
        {
            try
            {
                Directory.Delete(GetFolderPath(info), true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing fence: {ex.Message}");
            }
        }

        public void UpdateFence(FenceInfo fenceInfo)
        {
            try
            {
                var path = GetFolderPath(fenceInfo);
                EnsureDirectoryExists(path);

                var metaFile = Path.Combine(path, MetaFileName);
                var serializer = new XmlSerializer(typeof(FenceInfo));
                using (var writer = new StreamWriter(metaFile))
                {
                    serializer.Serialize(writer, fenceInfo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating fence: {ex.Message}");
            }
        }

        public void NotifyFenceChanged()
        {
            try
            {
                FencesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }

        private void EnsureDirectoryExists(string dir)
        {
            var di = new DirectoryInfo(dir);
            if (!di.Exists)
                di.Create();
        }

        private string GetFolderPath(FenceInfo fenceInfo)
        {
            return Path.Combine(basePath, fenceInfo.Id.ToString());
        }
    }
}
