using System;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace OmegaTube.Ui
{
    // Player option list (subtitles, audio, speed, quality): a title, one line per choice with a check mark on
    // the current one, the focused line lit. Long lists scroll with the focus.
    public class ListPanel : View
    {
        public class Item
        {
            public string Text, Detail, SubText, Badge;
            public bool Checked;
        }

        private class RowViews
        {
            public View Root, Badge;
            public ImageView Check;
            public TextLabel Text, Detail, SubText, BadgeText;
        }

        private const int LineH = 52, Top = 64, MaxVisible = 8, Pad = 14;

        private readonly TextLabel title;
        private readonly View clip, lines;
        private readonly List<RowViews> rows = new List<RowViews>();
        private List<Item> items = new List<Item>();
        private Animation scroll;

        public int Index { get; private set; }

        public ListPanel()
        {
            BackgroundColor = new Color(0.11f, 0.11f, 0.13f, 0.94f);
            Widgets.Round(this, 18);
            title = Widgets.Label("", Theme.BodySize, Theme.Text, 600, 44);
            title.VerticalAlignment = VerticalAlignment.Center;
            title.Position = new Position(24, 12);
            Add(title);
            clip = new View { Position = new Position(0, Top), ClippingMode = ClippingModeType.ClipChildren };
            Add(clip);
            lines = new View();
            clip.Add(lines);
            Hide();
        }

        // Shows the list with its bottom-right corner at (right, bottom).
        public void Open(string heading, List<Item> list, int right, int bottom, int width = 460)
        {
            foreach (RowViews r in rows)
            {
                lines.Remove(r.Root);
                r.Root.Dispose();
            }
            rows.Clear();
            items = list;
            title.Text = heading;
            title.SizeWidth = width - 48;
            Index = Math.Max(0, list.FindIndex(i => i.Checked));
            int rowW = width - 2 * Pad;
            int rowH = LineH - 4;
            for (int i = 0; i < list.Count; i++)
            {
                Item item = list[i];
                var row = Widgets.Rounded(Color.Transparent, rowW, rowH, 12);
                row.Position = new Position(Pad, i * LineH);
                var check = Widgets.Icon("check", 26);
                check.Position = new Position(14, (rowH - 26) / 2);
                Widgets.SetVisible(check, item.Checked);
                row.Add(check);

                bool detail = !string.IsNullOrEmpty(item.Detail);
                bool hasBadge = !string.IsNullOrEmpty(item.Badge);
                bool hasSub = !string.IsNullOrEmpty(item.SubText);
                int badgeW = hasBadge ? 72 : 0;
                int rightUsed = hasBadge ? badgeW + 20 : 12;
                int subW = hasSub ? 175 : 0;

                var text = Widgets.Label(item.Text, Theme.BodySize - 2, Theme.Text, rowW - 56 - rightUsed - (hasSub ? subW + 8 : 0), detail ? 28 : rowH);
                text.VerticalAlignment = detail ? VerticalAlignment.Bottom : VerticalAlignment.Center;
                text.Position = new Position(50, detail ? 2 : 0);
                row.Add(text);

                TextLabel detailLabel = null;
                if (detail)
                {
                    detailLabel = Widgets.Label(item.Detail, Theme.SmallSize - 2, Theme.TextDim, rowW - 56 - rightUsed, 20);
                    detailLabel.Position = new Position(50, 28);
                    row.Add(detailLabel);
                }

                TextLabel subLabel = null;
                if (hasSub)
                {
                    subLabel = Widgets.Label(item.SubText, Theme.SmallSize - 3, Theme.TextDim, subW, rowH);
                    subLabel.HorizontalAlignment = HorizontalAlignment.End;
                    subLabel.VerticalAlignment = VerticalAlignment.Center;
                    subLabel.Position = new Position(rowW - rightUsed - subW, 0);
                    row.Add(subLabel);
                }

                View badgeView = null;
                TextLabel badgeLabel = null;
                if (hasBadge)
                {
                    badgeView = Widgets.Rounded(new Color(1f, 1f, 1f, 0.14f), badgeW, 24, 12);
                    badgeView.Position = new Position(rowW - badgeW - 12, (rowH - 24) / 2);
                    badgeLabel = Widgets.Label(item.Badge, Theme.SmallSize - 4, Theme.Text, badgeW, 24);
                    badgeLabel.HorizontalAlignment = HorizontalAlignment.Center;
                    badgeLabel.VerticalAlignment = VerticalAlignment.Center;
                    badgeView.Add(badgeLabel);
                    row.Add(badgeView);
                }

                lines.Add(row);
                rows.Add(new RowViews
                {
                    Root = row, Check = check, Text = text, Detail = detailLabel,
                    SubText = subLabel, Badge = badgeView, BadgeText = badgeLabel,
                });
            }
            int visible = Math.Min(MaxVisible, list.Count);
            Size = new Size(width, Top + visible * LineH + 12);
            clip.Size = new Size(width, visible * LineH);
            Position = new Position(right - width, bottom - SizeHeight);
            lines.PositionY = 0;
            Show();
            Refresh(false);
        }

        public void Move(int delta)
        {
            Index = Math.Max(0, Math.Min(items.Count - 1, Index + delta));
            Refresh(true);
        }

        private void Refresh(bool animate)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                bool f = i == Index;
                RowViews r = rows[i];
                r.Root.BackgroundColor = f ? Theme.CardInfoFocused : Color.Transparent;
                r.Check.Color = f ? new Color(0.16f, 0.16f, 0.16f, 1f) : Color.White;
                r.Text.TextColor = f ? Theme.CardTextFocused : Theme.Text;
                if (r.Detail != null)
                {
                    r.Detail.TextColor = f ? new Color(0.33f, 0.33f, 0.33f, 1f) : Theme.TextDim;
                }
                if (r.SubText != null)
                {
                    r.SubText.TextColor = f ? new Color(0.33f, 0.33f, 0.33f, 1f) : Theme.TextDim;
                }
                if (r.Badge != null)
                {
                    r.Badge.BackgroundColor = f ? new Color(0f, 0f, 0f, 0.14f) : new Color(1f, 1f, 1f, 0.14f);
                    r.BadgeText.TextColor = f ? Theme.CardTextFocused : Theme.Text;
                }
            }
            // Keep the focused line inside the window, one line of context when possible.
            int first = Math.Max(0, Math.Min(Index - MaxVisible + 2, items.Count - MaxVisible));
            float y = -first * LineH;
            if (animate)
            {
                scroll = Widgets.Slide(lines, 0, y, scroll, 140);
            }
            else
            {
                lines.PositionY = y;
            }
        }
    }
}
