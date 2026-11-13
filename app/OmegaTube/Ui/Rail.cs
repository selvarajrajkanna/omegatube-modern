using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace OmegaTube.Ui
{
    // Navigation rail: a panel that shows icons only while the content has focus, icons + labels while the
    // rail has focus.
    public class Rail : View
    {
        public struct Entry
        {
            public string Icon, Label;
        }

        private const int ItemHeight = 84;
        private const int Top = 150;
        private const int VisibleItems = 11;       // beyond this the list scrolls with the focus
        private const int IconSize = 40;

        private readonly List<View> items = new List<View>();
        private readonly List<TextLabel> labels = new List<TextLabel>();
        private readonly View panel;
        private readonly View list;
        private ImageView avatar;    // active account photo, top-left; focusable to switch profile
        private View avatarRing;     // focus highlight around the avatar
        private Animation scroll;
        private int index;
        private bool focused;
        private Animation widen;
        private int drawnIndex = -1;
        private bool drawnFocused;

        public int Index => index;
        public int Count => items.Count;

        public Rail(IList<Entry> entries)
        {
            Size = new Size(Theme.RailExpandedWidth, 1080);

            panel = new View { Size = new Size(Theme.RailWidth, 1080), BackgroundColor = Theme.Background };
            Add(panel);
            // The top-left profile avatar (falls back to a person icon when signed out / no photo).
            // Focusable: the home screen moves focus here from the top rail entry, and OK cycles accounts.
            avatarRing = new ImageView { ResourceUrl = Theme.Image("circle_mask"), Color = Theme.Accent,
                                         Size = new Size(76, 76), Position = new Position(17, 38) };
            avatarRing.Hide();
            Add(avatarRing);
            avatar = new ImageView { Size = new Size(64, 64), Position = new Position(23, 44) };
            Add(avatar);
            Widgets.MaskedImage(avatar, Theme.Icon("person"), Theme.Image("circle_mask"));

            var clip = new View { Position = new Position(0, Top), Size = new Size(Theme.RailExpandedWidth, 1080 - Top),
                                  ClippingMode = ClippingModeType.ClipChildren };
            Add(clip);
            list = new View { Size = new Size(Theme.RailExpandedWidth, 2000) };
            clip.Add(list);

            for (int i = 0; i < entries.Count; i++)
            {
                var item = new View
                {
                    Position = new Position(14, i * ItemHeight),
                    Size = new Size(Theme.RailWidth - 28, ItemHeight - 12),
                };
                Widgets.Round(item, (ItemHeight - 12) / 2);
                var icon = new ImageView { ResourceUrl = Theme.Icon(entries[i].Icon), Size = new Size(IconSize, IconSize),
                                           Position = new Position(21, (ItemHeight - 12 - IconSize) / 2) };
                item.Add(icon);
                var label = Widgets.Label(entries[i].Label, Theme.BodySize + 2, Theme.Text, 220, ItemHeight - 12);
                label.VerticalAlignment = VerticalAlignment.Center;
                label.Position = new Position(80, 0);
                label.Hide();
                item.Add(label);
                list.Add(item);
                items.Add(item);
                labels.Add(label);
            }
            Refresh();
        }

        // The active account's avatar (empty -> default person icon).
        public void SetAvatar(string photo)
        {
            if (avatar != null)
            {
                string url = string.IsNullOrEmpty(photo) ? Theme.Icon("person") : photo;
                Widgets.MaskedImage(avatar, url, Theme.Image("circle_mask"));
            }
        }

        public void SetAvatarFocused(bool value)
        {
            if (avatarRing != null)
            {
                Widgets.SetVisible(avatarRing, value);
            }
        }

        public void SetFocused(bool value)
        {
            if (focused == value)
            {
                return;   // asked on every key press: only a change needs animating
            }
            focused = value;
            widen?.Stop();
            widen = new Animation(160);
            float w = value ? Theme.RailExpandedWidth : Theme.RailWidth;
            widen.AnimateTo(panel, "SizeWidth", w, Widgets.EaseOut);
            widen.Play();
            foreach (View item in items)
            {
                item.SizeWidth = (value ? Theme.RailExpandedWidth : Theme.RailWidth) - 28;
            }
            foreach (TextLabel l in labels)
            {
                Widgets.SetVisible(l, value);
            }
            panel.BackgroundColor = value ? Theme.Surface : Theme.Background;
            Refresh();
        }

        public void Move(int delta)
        {
            index = System.Math.Max(0, System.Math.Min(items.Count - 1, index + delta));
            Refresh();
        }

        public void Select(int i)
        {
            index = i;
            Refresh();
        }

        private void Refresh()
        {
            if (index == drawnIndex && focused == drawnFocused)
            {
                return;   // nothing it draws has changed
            }
            drawnIndex = index;
            drawnFocused = focused;
            int first = System.Math.Max(0, index - VisibleItems + 2);
            scroll = Widgets.Slide(list, 0, -first * ItemHeight, scroll);
            for (int i = 0; i < items.Count; i++)
            {
                bool current = i == index;
                items[i].BackgroundColor = focused && current ? Theme.Accent
                                         : current ? Theme.SurfaceHigh : Color.Transparent;
            }
        }
    }
}
