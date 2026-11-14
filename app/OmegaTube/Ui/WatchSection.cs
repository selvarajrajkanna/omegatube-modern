using System;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using static OmegaTube.I18n;

namespace OmegaTube.Ui
{
    // The watch page's description box below the player's suggestions row.
    // Focus moves with Up / Down; Up or Back leaves the section (Key returns false).
    public class WatchSection : View
    {
        public const int SectionWidth = 1780;
        public const int Viewport = 930;       // visible height while the section has the focus
        private const int Pad = 32;
        private const int DescLines = 3;
        private const float LineFactor = 1.35f;

        public float Scroll { get; private set; }
        public bool EntryOpen => false;
        public Action<string> Toast;
        public Action ScrollChanged;

        private readonly View background;
        private readonly View descBox;
        private readonly TextLabel descMeta, descText, descMore;

        private string meta = "", description = "";
        private bool descExpanded, descTruncated;
        private float descHeight, total;
        private bool focused;

        public WatchSection(string videoId, View overlayHost)
        {
            Size = new Size(SectionWidth, 100);

            // Opaque enough to read over the video; extends left / right to the screen edges.
            background = new View { BackgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.94f), Position = new Position(-70, -40) };
            Add(background);

            descBox = Widgets.Rounded(new Color(1, 1, 1, 0.1f), SectionWidth, 100, 16);
            Add(descBox);
            descMeta = Widgets.Bold(Widgets.Label("", Theme.BodySize, Theme.Text, SectionWidth - 2 * Pad, 36));
            descMeta.Position = new Position(Pad, 22);
            descBox.Add(descMeta);
            descText = Widgets.Label("", Theme.BodySize, Theme.Text, SectionWidth - 2 * Pad, 10);
            descText.MultiLine = true;
            descText.Ellipsis = false;
            descText.VerticalAlignment = VerticalAlignment.Top;
            descText.Position = new Position(Pad, 64);
            descBox.Add(descText);
            descMore = Widgets.Bold(Widgets.Label("", Theme.BodySize, Theme.Text, 500, 36));
            descBox.Add(descMore);

            ArrangeParts();
        }

        public void Close() { }

        public void Begin() { }

        public void SetDescription(string metaLine, string text)
        {
            metaLine = metaLine ?? "";
            text = (text ?? "").Trim();
            if (metaLine == meta && text == description)
            {
                return;
            }
            meta = metaLine;
            description = text;
            FillDescription();
            ArrangeParts();
        }

        private void FillDescription()
        {
            descMeta.Text = meta;
            string text = description.Length > 0 ? description : T("No description.");
            int perLine = CharsPerLine(SectionWidth - 2 * Pad, Theme.BodySize);
            List<string> lines = WrapLines(text, perLine);
            descTruncated = lines.Count > DescLines;
            int shown = descExpanded || !descTruncated ? lines.Count : DescLines;
            string body = string.Join("\n", lines.GetRange(0, shown));
            descText.Text = descTruncated && !descExpanded ? body + "…" : body;
            descText.TextColor = description.Length > 0 ? Theme.Text : Theme.TextDim;
            float textH = Math.Max(1, shown) * Theme.BodySize * LineFactor;
            descText.SizeHeight = textH + 8;
            descText.PositionY = meta.Length > 0 ? 64 : 22;
            float y = descText.PositionY + textH + 8;
            if (descTruncated)
            {
                descMore.Text = descExpanded ? T("Show less") : T("Show more");
                descMore.Position = new Position(Pad, y);
                descMore.Show();
                y += 40;
            }
            else
            {
                descMore.Hide();
            }
            descHeight = y + 20;
            descBox.SizeHeight = descHeight;
        }

        private void ArrangeParts()
        {
            if (descHeight == 0)
            {
                FillDescription();
            }
            total = descHeight + 80;
            SizeHeight = total;
            background.Size = new Size(1920, Math.Max(1200, total + 120));
            Scroll = Math.Min(Scroll, MaxScroll);
        }

        private float MaxScroll => Math.Max(0, total - Viewport);

        public void SetFocused(bool value)
        {
            if (value == focused)
            {
                return;
            }
            focused = value;
            if (value)
            {
                Reveal(+1);
            }
            Refresh();
        }

        private void Refresh()
        {
            descBox.BackgroundColor = focused ? new Color(1, 1, 1, 0.2f) : new Color(1, 1, 1, 0.1f);
        }

        private void Reveal(int direction)
        {
            float margin = 30;
            if (descHeight > Viewport - 2 * margin)
            {
                Scroll = direction > 0 ? 0 : descHeight - Viewport + margin;
            }
            else
            {
                Scroll = 0;
            }
            Scroll = Math.Max(0, Math.Min(MaxScroll, Scroll));
        }

        public bool Key(string key)
        {
            switch (key)
            {
                case "Down":
                    if (descHeight + 30 > Scroll + Viewport && Scroll < MaxScroll)
                    {
                        Scroll = Math.Min(Math.Min(MaxScroll, descHeight + 30 - Viewport), Scroll + 400);
                    }
                    break;
                case "Up":
                    if (Scroll > 0)
                    {
                        Scroll = Math.Max(0, Scroll - 400);
                        break;
                    }
                    return false;
                case "Return":
                    if (descTruncated)
                    {
                        descExpanded = !descExpanded;
                        FillDescription();
                        ArrangeParts();
                        Reveal(descExpanded ? +1 : -1);
                        if (!descExpanded)
                        {
                            Scroll = 0;
                        }
                    }
                    break;
                case "XF86Back":
                case "Escape":
                    return false;
            }
            Refresh();
            return true;
        }

        private static int CharsPerLine(int width, float pixelSize) => Math.Max(10, (int)(width / (pixelSize * 0.55f)));

        private static List<string> WrapLines(string text, int max)
        {
            var lines = new List<string>();
            foreach (string paragraph in text.Replace("\r", "").Split('\n'))
            {
                var line = new System.Text.StringBuilder();
                foreach (string raw in paragraph.Split(' '))
                {
                    string word = raw;
                    while (word.Length > max)
                    {
                        if (line.Length > 0)
                        {
                            lines.Add(line.ToString());
                            line.Clear();
                        }
                        lines.Add(word.Substring(0, max));
                        word = word.Substring(max);
                    }
                    if (word.Length == 0)
                    {
                        continue;
                    }
                    if (line.Length > 0 && line.Length + 1 + word.Length > max)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }
                    if (line.Length > 0)
                    {
                        line.Append(' ');
                    }
                    line.Append(word);
                }
                lines.Add(line.ToString());
            }
            while (lines.Count > 1 && lines[lines.Count - 1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }
            return lines;
        }
    }
}
