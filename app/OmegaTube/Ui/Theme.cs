using Tizen.NUI;

namespace OmegaTube.Ui
{
    // Omega dark theme: deep charcoal surfaces, crisp contrast, YouTube red accent.
    // 1920x1080 layout, sizes in pixels.
    public static class Theme
    {
        public static readonly Color Background = Rgb(0x0a0a0c);
        public static readonly Color Surface = Rgb(0x222226);
        public static readonly Color SurfaceHigh = Rgb(0x34343a);
        public static readonly Color SurfaceFocused = Rgb(0x3a3a42);
        public static readonly Color CardInfo = Rgb(0x1b1b1f);
        public static readonly Color CardInfoFocused = Rgb(0xe8e8ec);
        public static readonly Color CardTextFocused = Rgb(0x18181b);
        public static readonly Color Accent = Rgb(0xe52d27);
        public static readonly Color Text = Rgb(0xf2f2f5);
        public static readonly Color TextDim = Rgb(0xa8a8b2);
        public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.65f);
        public static readonly Color Panel = Rgb(0x16161a);         // settings / account cards, player panels
        public static readonly Color PanelLine = Rgb(0x2a2a30);     // separators
        public static readonly Color TextMuted = Rgb(0x70707a);
        public static readonly Color Success = Rgb(0x2ba640);
        // Grey rather than a white tint: as light as before on dark scenes, but it darkens bright ones under the icons.
        public static readonly Color GlassButton = new Color(0.24f, 0.24f, 0.24f, 0.6f);

        public const float TitleSize = 34f;
        public const float BodySize = 24f;
        public const float SmallSize = 19f;

        public const int RailWidth = 110;          // collapsed: icons only
        public const int RailExpandedWidth = 330;  // focused: icons + labels
        public const int CardWidth = 352;
        public const int CardGap = 30;
        public const int CardInfoHeight = 104;     // title (2 lines) + channel line
        public const int CardRadius = 14;          // soft corners (no sharp edges)
        public const float FocusScale = 1.1f;
        public const int FocusMargin = 30;         // room for the focused card's scale-up
        public const int RowHeight = 470;
        public const int PanelRadius = 20;

        public static int ThumbHeight(int cardWidth) => cardWidth * 9 / 16;
        public static int CardHeight(int cardWidth) => ThumbHeight(cardWidth) + CardInfoHeight;

        // Resource paths, resolved once per name (a card asks for several, hundreds of cards a page).
        private static string resources;
        private static readonly System.Collections.Generic.Dictionary<string, string> images = new System.Collections.Generic.Dictionary<string, string>();
        private static readonly System.Collections.Generic.Dictionary<string, string> icons = new System.Collections.Generic.Dictionary<string, string>();

        private static string Resolve(System.Collections.Generic.Dictionary<string, string> cache, string folder, string name)
        {
            if (!cache.TryGetValue(name, out string path))
            {
                resources = resources ?? Tizen.Applications.Application.Current.DirectoryInfo.Resource;
                path = System.IO.Path.Combine(resources, folder, name + ".png");
                cache[name] = path;
            }
            return path;
        }

        public static string Image(string name) => Resolve(images, "images", name);

        public static string Icon(string name) => Resolve(icons, "icons", name);

        private static Color Rgb(int rgb) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);
    }
}
