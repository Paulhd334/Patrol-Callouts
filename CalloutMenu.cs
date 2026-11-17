using System;
using System.Collections.Generic;
using Rage;
using System.Windows.Forms;

namespace PatrolsCallouts.Utils
{
    // Small, dependency-free interaction menu. It deliberately uses only RPH input
    // so a missing third-party menu library cannot crash a running callout.
    internal sealed class CalloutMenu : IDisposable
    {
        private readonly List<string> items = new List<string>();
        private readonly HashSet<int> completed = new HashSet<int>();
        private string title = "INTERACTION";
        private string subtitle = string.Empty;
        private string feedback = string.Empty;
        private int selected;
        private bool open;
        private bool eWasDown;
        private bool upWasDown;
        private bool downWasDown;
        private bool disposed;

        internal bool IsOpen { get { return open && !disposed; } }

        internal void SetItems(string menuTitle, string menuSubtitle, params string[] menuItems)
        {
            title = menuTitle ?? "INTERACTION";
            subtitle = menuSubtitle ?? string.Empty;
            items.Clear();
            completed.Clear();
            if (menuItems != null) items.AddRange(menuItems);
            selected = 0;
        }

        internal bool OpenKeyPressed()
        {
            if (disposed) return false;
            bool pressed = Game.IsKeyDown(Keys.E);
            bool result = pressed && !eWasDown;
            eWasDown = pressed;
            return result;
        }

        internal void Open()
        {
            if (!disposed && items.Count > 0) open = true;
        }

        internal void Close()
        {
            open = false;
        }

        internal int Update()
        {
            if (!IsOpen) return -1;

            bool up = Game.IsKeyDown(Keys.Up);
            bool down = Game.IsKeyDown(Keys.Down);
            if (up && !upWasDown) selected = (selected + items.Count - 1) % items.Count;
            if (down && !downWasDown) selected = (selected + 1) % items.Count;
            upWasDown = up;
            downWasDown = down;

            string current = items.Count == 0 ? string.Empty : items[selected];
            string status = completed.Contains(selected) ? " ~g~[done]" : string.Empty;
            Game.DisplayHelp("~b~" + title + "~w~ " + subtitle + "\n~y~" + current + status +
                "~w~\nUp/Down: choose | ~INPUT_CONTEXT~: select | ~INPUT_FRONTEND_CANCEL~: close", 1);
            if (!string.IsNullOrEmpty(feedback)) Game.DisplaySubtitle(feedback, 1000);

            if (Game.IsKeyDown(Keys.E) && !eWasDown)
            {
                eWasDown = true;
                return selected;
            }
            eWasDown = Game.IsKeyDown(Keys.E);

            if (Game.IsKeyDown(Keys.Escape)) Close();
            return -1;
        }

        internal void MarkDone(int index) { if (index >= 0 && index < items.Count) completed.Add(index); }
        internal void SetFeedback(string value) { feedback = value ?? string.Empty; }
        public void Dispose() { disposed = true; open = false; items.Clear(); completed.Clear(); }
    }
}
