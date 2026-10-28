using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace CrayonTodo
{
    public class TodoItem
    {
        public string Id;
        public string Title;
        public bool Done;
        public int CompletedPomodoros;
    }

    public enum TimerPhase { Idle, Focus, Decision, Break }

    // The model is owned by the UI thread. Tick is driven by the UI timer.
    public class TodoModel
    {
        private const int MaximumItems = 1000;
        private const int MaximumTitleLength = 200;
        private static readonly TimeSpan MaximumFocusLength = TimeSpan.FromMinutes(60);
        private static readonly TimeSpan BreakLength = TimeSpan.FromMinutes(5);
        private readonly string dataDirectory;
        private readonly string dataFile;

        public List<TodoItem> Items;
        public TimerPhase Phase;
        public string ActiveId;
        public DateTime? DeadlineUtc;
        public TimeSpan RemainingWhenPaused;
        public bool Paused;
        public double Opacity;
        public int FocusMinutes;
        public string ThemeKey { get; private set; }
        public event Action Changed;

        public TodoModel(string dataDirectory)
        {
            if (String.IsNullOrWhiteSpace(dataDirectory)) throw new ArgumentException("A data directory is required.", "dataDirectory");
            this.dataDirectory = dataDirectory;
            dataFile = Path.Combine(dataDirectory, "tasks.xml");
            Items = new List<TodoItem>();
            Phase = TimerPhase.Idle;
            Opacity = 1.0;
            FocusMinutes = 25;
            ThemeKey = "shinchan";
        }

        public void Load()
        {
            // A power loss between moving the old file aside and installing the new
            // one can leave only the backup. Never start with an empty list in that case.
            string readFile = dataFile;
            if (!File.Exists(readFile))
            {
                readFile = dataFile + ".bak";
                if (!File.Exists(readFile)) return;
            }
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Prohibit;
            settings.XmlResolver = null;
            settings.MaxCharactersInDocument = 2000000;
            XmlDocument document = new XmlDocument();
            document.XmlResolver = null;
            using (XmlReader reader = XmlReader.Create(readFile, settings)) document.Load(reader);
            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != "crayon-todo" || root.GetAttribute("version") != "1")
                throw new InvalidDataException("Unrecognized task data format.");

            List<TodoItem> loadedItems = new List<TodoItem>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            XmlNodeList nodes = root.SelectNodes("items/item");
            if (nodes.Count > MaximumItems) throw new InvalidDataException("Too many tasks in the saved file.");
            foreach (XmlElement node in nodes)
            {
                Guid parsedId;
                string id = node.GetAttribute("id");
                string title = node.InnerText;
                bool done;
                int count;
                if (!Guid.TryParseExact(id, "N", out parsedId) || !ids.Add(id) || !ValidTitle(title) ||
                    !Boolean.TryParse(node.GetAttribute("done"), out done) ||
                    !Int32.TryParse(node.GetAttribute("pomodoros"), NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 0)
                    throw new InvalidDataException("Invalid task in the saved file.");
                loadedItems.Add(new TodoItem { Id = id, Title = title, Done = done, CompletedPomodoros = count });
            }
            double opacity;
            if (!Double.TryParse(root.GetAttribute("opacity"), NumberStyles.Float, CultureInfo.InvariantCulture, out opacity) ||
                Double.IsNaN(opacity) || Double.IsInfinity(opacity) || opacity < 0.35 || opacity > 1)
                throw new InvalidDataException("Invalid opacity in the saved file.");
            TimerPhase phase;
            if (!Enum.TryParse<TimerPhase>(root.GetAttribute("phase"), out phase) || !Enum.IsDefined(typeof(TimerPhase), phase))
                throw new InvalidDataException("Invalid timer phase in the saved file.");
            string activeId = root.GetAttribute("active");
            int focusMinutes = 25;
            string storedMinutes = root.GetAttribute("focusMinutes");
            if (storedMinutes.Length != 0 && (!Int32.TryParse(storedMinutes, NumberStyles.None, CultureInfo.InvariantCulture, out focusMinutes) || focusMinutes < 25 || focusMinutes > 60))
                throw new InvalidDataException("Invalid focus duration in the saved file.");
            string theme = root.GetAttribute("theme");
            if (!ValidTheme(theme)) theme = "shinchan";
            long ticks;
            if (!Int64.TryParse(root.GetAttribute("remainingTicks"), NumberStyles.None, CultureInfo.InvariantCulture, out ticks) || ticks < 0 || ticks > MaximumFocusLength.Ticks)
                throw new InvalidDataException("Invalid remaining time in the saved file.");
            if (phase != TimerPhase.Idle && !ids.Contains(activeId)) throw new InvalidDataException("The timer task is missing from the saved file.");
            if (phase == TimerPhase.Break && ticks > BreakLength.Ticks) throw new InvalidDataException("Invalid break time in the saved file.");
            if ((phase == TimerPhase.Idle || phase == TimerPhase.Decision) && ticks != 0) throw new InvalidDataException("Invalid timer state in the saved file.");

            Items = loadedItems;
            Phase = phase;
            ActiveId = phase == TimerPhase.Idle ? null : activeId;
            DeadlineUtc = null;
            RemainingWhenPaused = TimeSpan.FromTicks(ticks);
            Paused = phase == TimerPhase.Focus || phase == TimerPhase.Break;
            Opacity = opacity;
            FocusMinutes = focusMinutes;
            ThemeKey = theme;
            Notify();
        }

        private static bool ValidTheme(string theme)
        {
            return theme == "shinchan" || theme == "kitty" || theme == "kuromi" || theme == "spongebob" || theme == "doraemon";
        }

        private static bool ValidTitle(string title)
        {
            if (String.IsNullOrWhiteSpace(title) || title.Length > MaximumTitleLength) return false;
            try { XmlConvert.VerifyXmlChars(title); return true; }
            catch (XmlException) { return false; }
        }

        private TodoItem Find(string id)
        {
            return Items.Find(delegate(TodoItem item) { return item.Id == id; });
        }

        public bool Add(string title)
        {
            if (title == null) return false;
            title = title.Trim();
            if (!ValidTitle(title) || Items.Count >= MaximumItems) return false;
            Items.Add(new TodoItem { Id = Guid.NewGuid().ToString("N"), Title = title });
            PersistAndNotify();
            return true;
        }

        public bool Rename(string id, string title)
        {
            TodoItem item = Find(id);
            if (item == null || title == null) return false;
            title = title.Trim();
            if (!ValidTitle(title)) return false;
            if (item.Title != title) { item.Title = title; PersistAndNotify(); }
            return true;
        }

        public void Toggle(string id)
        {
            TodoItem item = Find(id);
            if (item == null) return;
            item.Done = !item.Done;
            PersistAndNotify();
        }

        public void Delete(string id)
        {
            TodoItem item = Find(id);
            if (item == null) return;
            if (ActiveId == id) ClearTimer();
            Items.Remove(item);
            PersistAndNotify();
        }

        public bool Start(string id)
        {
            // Even a paused timer or decision is an active session; never replace it silently.
            if (Phase != TimerPhase.Idle || Find(id) == null) return false;
            ActiveId = id;
            Begin(TimerPhase.Focus, TimeSpan.FromMinutes(FocusMinutes));
            PersistAndNotify();
            return true;
        }

        public void PauseOrResume()
        {
            Tick();
            if (Phase != TimerPhase.Focus && Phase != TimerPhase.Break) return;
            if (Paused)
            {
                DeadlineUtc = DateTime.UtcNow.Add(RemainingWhenPaused);
                Paused = false;
            }
            else
            {
                RemainingWhenPaused = Remaining();
                DeadlineUtc = null;
                Paused = true;
            }
            PersistAndNotify();
        }

        public void Tick()
        {
            if (!Settle(DateTime.UtcNow)) return;
            PersistAndNotify();
        }

        // Uses a deadline, so delayed UI ticks still end at the correct wall-clock time.
        private bool Settle(DateTime now)
        {
            if (Paused || !DeadlineUtc.HasValue || now < DeadlineUtc.Value) return false;
            if (Phase == TimerPhase.Focus)
            {
                TodoItem item = Find(ActiveId);
                if (item != null && item.CompletedPomodoros < Int32.MaxValue) item.CompletedPomodoros++;
                Phase = TimerPhase.Decision;
                DeadlineUtc = null;
                RemainingWhenPaused = TimeSpan.Zero;
                Paused = false;
                return true;
            }
            if (Phase == TimerPhase.Break)
            {
                ClearTimer();
                return true;
            }
            return false;
        }

        public void ChooseBreak()
        {
            if (Phase != TimerPhase.Decision) return;
            Begin(TimerPhase.Break, BreakLength);
            PersistAndNotify();
        }

        public void ChooseNextFocus()
        {
            if (Phase != TimerPhase.Decision || Find(ActiveId) == null) return;
            Begin(TimerPhase.Focus, TimeSpan.FromMinutes(FocusMinutes));
            PersistAndNotify();
        }

        public void Finish()
        {
            if (Phase == TimerPhase.Idle) return;
            // Settle first so a fully elapsed focus is counted even if the UI timer was delayed.
            Settle(DateTime.UtcNow);
            ClearTimer();
            PersistAndNotify();
        }

        private void Begin(TimerPhase phase, TimeSpan duration)
        {
            Phase = phase;
            Paused = false;
            RemainingWhenPaused = TimeSpan.Zero;
            DeadlineUtc = DateTime.UtcNow.Add(duration);
        }

        private void ClearTimer()
        {
            Phase = TimerPhase.Idle;
            ActiveId = null;
            DeadlineUtc = null;
            RemainingWhenPaused = TimeSpan.Zero;
            Paused = false;
        }

        public TimeSpan Remaining()
        {
            if (Phase != TimerPhase.Focus && Phase != TimerPhase.Break) return TimeSpan.Zero;
            TimeSpan remaining = Paused ? RemainingWhenPaused : (DeadlineUtc.HasValue ? DeadlineUtc.Value - DateTime.UtcNow : TimeSpan.Zero);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        public void SetOpacity(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) return;
            value = Math.Max(0.35, Math.Min(1.0, value));
            if (Math.Abs(Opacity - value) < 0.000001) return;
            Opacity = value;
            PersistAndNotify();
        }

        public bool SetFocusMinutes(int value)
        {
            if (value < 25 || value > 60) return false;
            if (FocusMinutes != value) { FocusMinutes = value; PersistAndNotify(); }
            return true;
        }

        public bool SetTheme(string value)
        {
            if (!ValidTheme(value)) return false;
            if (ThemeKey != value) { ThemeKey = value; PersistAndNotify(); }
            return true;
        }

        public void Save()
        {
            bool changed = Settle(DateTime.UtcNow);
            Directory.CreateDirectory(dataDirectory);
            string temporary = Path.Combine(dataDirectory, "tasks." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Encoding = new UTF8Encoding(false);
                settings.Indent = true;
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (XmlWriter writer = XmlWriter.Create(stream, settings))
                    {
                        writer.WriteStartDocument();
                        writer.WriteStartElement("crayon-todo");
                        writer.WriteAttributeString("version", "1");
                        writer.WriteAttributeString("opacity", Opacity.ToString("R", CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("focusMinutes", FocusMinutes.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("theme", ThemeKey);
                        writer.WriteAttributeString("phase", Phase.ToString());
                        writer.WriteAttributeString("active", ActiveId ?? "");
                        writer.WriteAttributeString("remainingTicks", Remaining().Ticks.ToString(CultureInfo.InvariantCulture));
                        writer.WriteStartElement("items");
                        foreach (TodoItem item in Items)
                        {
                            writer.WriteStartElement("item");
                            writer.WriteAttributeString("id", item.Id);
                            writer.WriteAttributeString("done", item.Done.ToString());
                            writer.WriteAttributeString("pomodoros", item.CompletedPomodoros.ToString(CultureInfo.InvariantCulture));
                            writer.WriteString(item.Title);
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                        writer.WriteEndDocument();
                    }
                }
                if (File.Exists(dataFile)) ReplaceSavedFile(temporary);
                else File.Move(temporary, dataFile);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            if (changed) Notify();
        }

        private void ReplaceSavedFile(string temporary)
        {
            try
            {
                File.Replace(temporary, dataFile, dataFile + ".bak");
                return;
            }
            catch (UnauthorizedAccessException)
            {
                // Some Windows setups allow writing and renaming here but reject ReplaceFile.
                if (!File.Exists(temporary) || !File.Exists(dataFile)) throw;
            }
            catch (IOException)
            {
                if (!File.Exists(temporary) || !File.Exists(dataFile)) throw;
            }

            // Keep a readable backup before changing the original. Both moves are in
            // the same directory; the temporary file was fully written and closed.
            string backup = dataFile + ".bak";
            string previous = Path.Combine(dataDirectory, "tasks." + Guid.NewGuid().ToString("N") + ".previous");
            File.Copy(dataFile, backup, true);
            File.Move(dataFile, previous);
            try
            {
                File.Move(temporary, dataFile);
            }
            catch
            {
                // If restoring also fails, retain both the previous file and backup.
                try { if (!File.Exists(dataFile)) File.Move(previous, dataFile); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw;
            }
            try { File.Delete(previous); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void PersistAndNotify() { Save(); Notify(); }
        private void Notify() { Action handler = Changed; if (handler != null) handler(); }
    }
}
