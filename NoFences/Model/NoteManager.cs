using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace NoFences.Model
{
    public class NoteManager
    {
        public static NoteManager Instance { get; } = new NoteManager();

        private const string NotesFolder = "Notes";
        private const string MetaFileName = "__note_metadata.xml";

        private readonly string basePath;
        private readonly List<NoteWindow> openNotes = new List<NoteWindow>();
        private readonly object notesLock = new object();

        public IReadOnlyList<NoteWindow> OpenNotes
        {
            get
            {
                lock (notesLock)
                {
                    return openNotes.ToList().AsReadOnly();
                }
            }
        }

        public NoteManager()
        {
            basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoFences", NotesFolder);
            EnsureDirectoryExists(basePath);
        }

        public void LoadNotes()
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

                        var serializer = new XmlSerializer(typeof(NoteInfo));
                        using (var reader = new StreamReader(metaFile))
                        {
                            var note = serializer.Deserialize(reader) as NoteInfo;
                            if (note != null)
                            {
                                var window = new NoteWindow(note);
                                RegisterNoteWindow(window);
                                window.Show();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading note: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading notes: {ex.Message}");
            }
        }

        public void CreateNote()
        {
            try
            {
                var noteInfo = new NoteInfo(Guid.NewGuid())
                {
                    PosX = 150,
                    PosY = 150
                };

                UpdateNote(noteInfo);
                var window = new NoteWindow(noteInfo);
                RegisterNoteWindow(window);
                window.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating note: {ex.Message}");
            }
        }

        private void RegisterNoteWindow(NoteWindow window)
        {
            lock (notesLock)
            {
                openNotes.Add(window);
            }
            window.FormClosed += (s, e) =>
            {
                lock (notesLock)
                {
                    openNotes.Remove(window);
                }
            };
        }

        public void RemoveNote(NoteInfo info)
        {
            try
            {
                var path = GetFolderPath(info);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing note: {ex.Message}");
            }
        }

        public void UpdateNote(NoteInfo noteInfo)
        {
            try
            {
                var path = GetFolderPath(noteInfo);
                EnsureDirectoryExists(path);

                var metaFile = Path.Combine(path, MetaFileName);
                var serializer = new XmlSerializer(typeof(NoteInfo));
                using (var writer = new StreamWriter(metaFile))
                {
                    serializer.Serialize(writer, noteInfo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating note: {ex.Message}");
            }
        }

        private void EnsureDirectoryExists(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private string GetFolderPath(NoteInfo noteInfo)
        {
            return Path.Combine(basePath, noteInfo.Id.ToString());
        }
    }
}
