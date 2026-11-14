using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaTube.Data;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using static OmegaTube.I18n;

namespace OmegaTube.Ui
{
    // 30% left split-screen comments panel using native Tizen.NUI components.
    // Focus stays inside the panel until closed with Back (XF86Back / Escape).
    public class CommentsPanel : View
    {
        public const int PanelWidth = 576;   // 30% of 1920
        public const int PanelHeight = 1080;
        private const int Pad = 20;
        private const int ContentWidth = PanelWidth - 2 * Pad;
        private const int HeaderH = 64;
        private const int ViewportTop = HeaderH + 8;
        private const int ViewportHeight = PanelHeight - ViewportTop;
        private const int CommentLines = 4;
        private const int MaxComments = 300;
        private const int MaxEntries = 500;
        private const int ReplyIndent = 30;
        private const int ToolH = 36;
        private const int MaxCommentLength = 10000;
        private const int AvatarSize = 36;
        private const int TextX = AvatarSize + 12;
        private const float BodyFont = Theme.SmallSize + 1;   // 20f
        private const float MetaFont = Theme.SmallSize - 2;   // 17f
        private const float LineFactor = 1.32f;

        private enum Part { Sort, Form, Comment }
        private enum Tool { Like, Dislike, Reply, Replies }

        private class ToolButton
        {
            public Tool Kind;
            public View Pill;
            public ImageView Icon;
            public TextLabel Label;
        }

        private class Entry
        {
            public Comment Comment;
            public Entry Parent;
            public readonly List<Entry> Children = new List<Entry>();
            public readonly List<ToolButton> Tools = new List<ToolButton>();
            public string MoreToken = "";
            public int Indent;
            public bool RepliesOpen, RepliesLoading, Voting;
            public View View, Highlight;
            public TextLabel Text, More;
            public View Toolbar;
            public bool Expanded, Truncated;
            public float Top, Height;
        }

        public bool EntryOpen => entry != null;
        public Action<string> Toast;

        private readonly string videoId;
        private readonly View overlayHost;
        private readonly View header, sortButton, clip, content;
        private readonly TextLabel countLabel, sortLabel, status;
        private readonly ImageView sortIcon;
        private readonly View form, formLine;
        private readonly ImageView formAvatar;
        private readonly TextLabel formLabel;
        private readonly View list;
        private readonly List<Entry> entries = new List<Entry>();

        private float scrollY;
        private Animation scrollAnim;
        private float formTop, statusTop, listTop, total;
        private int focusIndex;   // 0 sort, 1 form, 2.. comments
        private bool started, loading, newest, disabled;
        private int generation;
        private string continuation = "", sortTop = "", sortNewest = "", createParams = "";
        private string draft = "";
        private TextEntry entry;
        private Entry replyTarget;
        private int toolIndex = -1;
        private Entry painted;
        private bool posting;
        private bool closed;

        private static string myPhoto, myHandle;

        private bool HasComments => started && !disabled;
        private int LastIndex => HasComments ? 1 + entries.Count : 0;

        public CommentsPanel(string videoId, View overlayHost)
        {
            this.videoId = videoId;
            this.overlayHost = overlayHost;
            Size = new Size(PanelWidth, PanelHeight);
            Position = new Position(0, 0);
            BackgroundColor = new Color(0.08f, 0.08f, 0.10f, 0.96f);

            // Right border separating comments (30%) from video (70%).
            Add(new View
            {
                BackgroundColor = Theme.PanelLine,
                Size = new Size(1, PanelHeight),
                Position = new Position(PanelWidth - 1, 0),
            });

            header = new View { Size = new Size(PanelWidth, HeaderH), Position = new Position(0, 0) };
            Add(header);
            countLabel = Widgets.Bold(Widgets.Label(T("Comments"), Theme.BodySize + 2, Theme.Text, 320, HeaderH));
            countLabel.VerticalAlignment = VerticalAlignment.Center;
            countLabel.Position = new Position(Pad, 4);
            header.Add(countLabel);

            sortButton = Widgets.Rounded(new Color(1, 1, 1, 0.1f), 170, 42, 21);
            sortButton.Position = new Position(PanelWidth - Pad - 170, 14);
            header.Add(sortButton);
            sortIcon = Widgets.Icon("view_list", 24);
            sortIcon.Position = new Position(12, 9);
            sortButton.Add(sortIcon);
            sortLabel = Widgets.Label("", MetaFont, Theme.Text, 120, 42);
            sortLabel.VerticalAlignment = VerticalAlignment.Center;
            sortLabel.Position = new Position(42, 0);
            sortButton.Add(sortLabel);

            Add(new View
            {
                BackgroundColor = Theme.PanelLine,
                Size = new Size(ContentWidth, 1),
                Position = new Position(Pad, HeaderH + 4),
            });

            clip = new View
            {
                Position = new Position(0, ViewportTop),
                Size = new Size(PanelWidth, ViewportHeight),
                ClippingMode = ClippingModeType.ClipChildren,
            };
            Add(clip);

            content = new View { Size = new Size(PanelWidth, ViewportHeight) };
            clip.Add(content);

            form = new View { Size = new Size(ContentWidth, 56), Position = new Position(Pad, 12) };
            content.Add(form);
            formAvatar = Widgets.Round(new ImageView { Size = new Size(AvatarSize, AvatarSize), Position = new Position(8, 10), BackgroundColor = Theme.SurfaceHigh }, AvatarSize / 2);
            form.Add(formAvatar);
            formLabel = Widgets.Label("", BodyFont, Theme.TextDim, ContentWidth - 64, 48);
            formLabel.VerticalAlignment = VerticalAlignment.Center;
            formLabel.Position = new Position(56, 2);
            form.Add(formLabel);
            formLine = new View { Size = new Size(ContentWidth - 64, 2), Position = new Position(56, 52), BackgroundColor = Theme.TextMuted };
            form.Add(formLine);

            status = Widgets.Label("", BodyFont, Theme.TextDim, ContentWidth, 44);
            status.PositionX = Pad;
            content.Add(status);

            list = new View { Size = new Size(ContentWidth, 10), PositionX = Pad };
            content.Add(list);

            UpdateSortLabel();
            ArrangeParts();
            Hide();
        }

        public void Open()
        {
            Show();
            Begin();
            focusIndex = 0;
            toolIndex = -1;
            scrollY = 0;
            content.PositionY = 0;
            Refresh();
        }

        public void ClosePanel()
        {
            CloseEntry();
            Hide();
        }

        public void Close()
        {
            closed = true;
            generation++;
            CloseEntry();
        }

        private void Begin()
        {
            if (started)
            {
                return;
            }
            started = true;
            UpdateForm();
            ShowStatus(T("Loading comments…"));
            ArrangeParts();
            UiGuard.Fire("CommentsPanel.LoadPage", () => LoadPage(""));
            if (YouTube.SignedIn() && myPhoto == null)
            {
                UiGuard.Fire("CommentsPanel.LoadAccount", LoadAccount);
            }
        }

        private async Task LoadAccount()
        {
            AccountInfo a = await YouTube.Account();
            if (a.Error != null)
            {
                return;
            }
            myPhoto = a.Photo ?? "";
            myHandle = a.Handle ?? a.Name ?? "";
            UpdateForm();
        }

        private async Task LoadPage(string token)
        {
            loading = true;
            int gen = generation;
            CommentPage page = await YouTube.Comments(videoId, token);
            if (gen != generation || closed)
            {
                return;
            }
            loading = false;
            if (page.Error != null)
            {
                Tizen.Log.Warn("omegatube-modern", "comments: " + page.Error);
                if (entries.Count == 0)
                {
                    ShowStatus(T("Comments unavailable"));
                }
                continuation = "";
                ArrangeParts();
                return;
            }
            if (page.Disabled)
            {
                disabled = true;
                form.Hide();
                sortButton.Hide();
                ShowStatus(T("Comments are turned off."));
                focusIndex = 0;
                ArrangeParts();
                Refresh();
                return;
            }
            if (page.Count.Length > 0)
            {
                countLabel.Text = T("{0} comments", page.Count);
            }
            if (page.SortTop.Length > 0) sortTop = page.SortTop;
            if (page.SortNewest.Length > 0) sortNewest = page.SortNewest;
            if (page.CreateParams.Length > 0) createParams = page.CreateParams;
            Widgets.SetVisible(sortButton, sortTop.Length > 0 || sortNewest.Length > 0);
            continuation = page.Continuation;
            foreach (Comment c in page.Items)
            {
                if (entries.Count >= MaxComments)
                {
                    continuation = "";
                    break;
                }
                AddEntry(c, entries.Count, null);
            }
            if (entries.Count == 0)
            {
                ShowStatus(T("No comments yet."));
            }
            else
            {
                status.Hide();
            }
            ArrangeParts();
            Refresh();
        }

        private void ToggleSort()
        {
            string token = newest ? sortTop : sortNewest;
            if (token.Length == 0 || loading)
            {
                return;
            }
            newest = !newest;
            UpdateSortLabel();
            generation++;
            ClearEntries();
            continuation = "";
            ShowStatus(T("Loading comments…"));
            ArrangeParts();
            Refresh();
            UiGuard.Fire("CommentsPanel.LoadPage", () => LoadPage(token));
        }

        private void UpdateSortLabel() => sortLabel.Text = newest ? T("Newest") : T("Top");

        private void MaybeLoadMore()
        {
            if (HasComments && !loading && continuation.Length > 0 && entries.Count < MaxComments && focusIndex >= entries.Count - 4)
            {
                string token = continuation;
                continuation = "";
                UiGuard.Fire("CommentsPanel.LoadPage", () => LoadPage(token));
            }
        }

        private void ClearEntries()
        {
            foreach (Entry e in entries)
            {
                list.Remove(e.View);
                e.View.Dispose();
            }
            entries.Clear();
            painted = null;
        }

        private void ShowStatus(string text)
        {
            status.Text = text;
            status.Show();
        }

        private void UpdateForm()
        {
            if (!YouTube.SignedIn())
            {
                form.Hide();
                return;
            }
            form.Show();
            if (!string.IsNullOrEmpty(myPhoto))
            {
                formAvatar.ResourceUrl = myPhoto;
            }
            formLabel.Text = draft.Length > 0 ? draft : T("Add a comment…");
            formLabel.TextColor = draft.Length > 0 ? Theme.Text : Theme.TextDim;
        }

        private Entry AddEntry(Comment c, int at, Entry parent, int indent = 0)
        {
            int x = indent;
            int w = ContentWidth - x;
            var view = new View { Size = new Size(w, 80), PositionX = x };
            var highlight = Widgets.Rounded(new Color(1, 1, 1, 0.10f), w + 12, 80, 12);
            highlight.Position = new Position(-6, -4);
            highlight.Hide();
            view.Add(highlight);

            var avatar = Widgets.Round(new ImageView
            {
                Size = new Size(AvatarSize, AvatarSize),
                Position = new Position(0, 6),
                BackgroundColor = Theme.SurfaceHigh,
                ResourceUrl = c.Avatar,
            }, AvatarSize / 2);
            view.Add(avatar);

            float y = 4;
            if (c.Pinned)
            {
                var pinned = Widgets.Label(T("Pinned"), MetaFont - 1, Theme.TextDim, w - TextX, 20);
                pinned.Position = new Position(TextX, y);
                view.Add(pinned);
                y += 22;
            }

            string who = (c.Author.Length > 0 ? c.Author : T("Unknown")) + (c.Published.Length > 0 ? "  ·  " + c.Published : "");
            TextLabel author = Widgets.Label(who, MetaFont, c.Creator ? Theme.Text : Theme.TextDim, w - TextX, 24);
            if (c.Creator)
            {
                author.BackgroundColor = Theme.SurfaceHigh;
                Widgets.Round(author, 10);
                author.SizeWidth = Math.Min(w - TextX, EstimateWidth(who, MetaFont) + 20);
                author.Text = "  " + who + "  ";
            }
            author.Position = new Position(TextX, y);
            view.Add(author);
            y += 26;

            var body = Widgets.Label("", BodyFont, Theme.Text, w - TextX, 10);
            body.MultiLine = true;
            body.Ellipsis = false;
            body.VerticalAlignment = VerticalAlignment.Top;
            body.Position = new Position(TextX, y);
            view.Add(body);

            TextLabel more = Widgets.Bold(Widgets.Label("", MetaFont, Theme.TextDim, 220, 26));
            more.PositionX = TextX;
            more.Hide();
            view.Add(more);

            var toolbar = new View { Size = new Size(w - TextX, ToolH), PositionX = TextX };
            view.Add(toolbar);

            var e = new Entry
            {
                Comment = c,
                Parent = parent,
                Indent = indent,
                View = view,
                Highlight = highlight,
                Text = body,
                More = more,
                Toolbar = toolbar,
                Top = y,
            };
            BuildTools(e);
            FillEntry(e);
            list.Add(view);
            entries.Insert(at, e);
            parent?.Children.Add(e);
            return e;
        }

        private void BuildTools(Entry e)
        {
            foreach (ToolButton tb in e.Tools)
            {
                e.Toolbar.Remove(tb.Pill);
                tb.Pill.Dispose();
            }
            e.Tools.Clear();
            int x = 0;
            x = AddTool(e, Tool.Like, "thumb_up", e.Comment.Likes, x);
            x = AddTool(e, Tool.Dislike, "thumb_down", "", x);
            if (YouTube.SignedIn())
            {
                x = AddTool(e, Tool.Reply, null, T("Reply"), x);
            }
            if (e.Parent == null && e.Comment.ReplyToken.Length > 0)
            {
                string label = e.RepliesOpen ? T("Hide replies")
                             : e.Comment.Replies.Length > 0 ? T("{0} replies", e.Comment.Replies)
                             : T("Replies");
                AddTool(e, Tool.Replies, null, label, x);
            }
            PaintTools(e, painted == e ? toolIndex : -1);
        }

        private int AddTool(Entry e, Tool kind, string icon, string text, int x)
        {
            var t = new ToolButton { Kind = kind, Pill = Widgets.Rounded(Color.Transparent, 36, ToolH, ToolH / 2) };
            t.Pill.Position = new Position(x, 0);
            int innerX = 8;
            if (icon != null)
            {
                t.Icon = Widgets.Icon(icon, 18);
                t.Icon.Position = new Position(innerX, (ToolH - 18) / 2);
                t.Pill.Add(t.Icon);
                innerX += 24;
            }
            if (!string.IsNullOrEmpty(text))
            {
                float wText = EstimateWidth(text, MetaFont) + 10;
                t.Label = Widgets.Label(text, MetaFont, Theme.Text, (int)Math.Ceiling(wText), ToolH);
                t.Label.VerticalAlignment = VerticalAlignment.Center;
                t.Label.Position = new Position(innerX, 0);
                t.Pill.Add(t.Label);
                innerX += (int)Math.Ceiling(wText);
            }
            int w = Math.Max(ToolH, innerX + 6);
            t.Pill.SizeWidth = w;
            e.Toolbar.Add(t.Pill);
            e.Tools.Add(t);
            return x + w + 4;
        }

        private static void PaintTools(Entry e, int selected)
        {
            for (int i = 0; i < e.Tools.Count; i++)
            {
                ToolButton t = e.Tools[i];
                bool on = i == selected;
                bool active = t.Kind == Tool.Like ? e.Comment.Liked : t.Kind == Tool.Dislike && e.Comment.Disliked;
                t.Pill.BackgroundColor = on ? Theme.CardInfoFocused : Color.Transparent;
                if (t.Icon != null)
                {
                    t.Icon.Color = on ? new Color(0.16f, 0.16f, 0.16f, 1f) : active ? Theme.Accent : Color.White;
                }
                if (t.Label != null)
                {
                    t.Label.TextColor = on ? Theme.CardTextFocused : Theme.Text;
                }
            }
        }

        private void FillEntry(Entry e)
        {
            float textTop = e.Top;
            int perLine = CharsPerLine(ContentWidth - TextX - e.Indent - 12, BodyFont);
            List<string> lines = WrapLines(e.Comment.Text, perLine);
            e.Truncated = lines.Count > CommentLines;
            int shown = e.Expanded || !e.Truncated ? lines.Count : CommentLines;
            e.Text.Text = string.Join("\n", lines.GetRange(0, shown));
            float textH = Math.Max(1, shown) * BodyFont * LineFactor;
            e.Text.SizeHeight = textH + 6;
            float y = textTop + textH + 4;
            if (e.Truncated)
            {
                e.More.Text = e.Expanded ? T("Show less") : T("Read more");
                e.More.PositionY = y;
                e.More.Show();
                y += 28;
            }
            else
            {
                e.More.Hide();
            }
            e.Toolbar.PositionY = y + 2;
            e.Height = y + 2 + ToolH + 12;
            e.View.SizeHeight = e.Height;
            e.Highlight.SizeHeight = e.Height;
        }

        private void ArrangeParts()
        {
            float y = 12;
            formTop = y;
            form.PositionY = y;
            if (form.Visibility)
            {
                y += 66;
            }
            statusTop = y;
            status.PositionY = y;
            if (status.Visibility)
            {
                y += 48;
            }
            listTop = y;
            list.PositionY = y;
            float cy = 0;
            foreach (Entry e in entries)
            {
                e.View.PositionY = cy;
                cy += e.Height;
            }
            list.SizeHeight = cy;
            total = y + cy + 24;
            content.SizeHeight = Math.Max(ViewportHeight, total);
        }

        private float MaxScroll => Math.Max(0, total - ViewportHeight);

        private Part PartOf(int index)
        {
            if (index == 0) return Part.Sort;
            if (index == 1) return Part.Form;
            return Part.Comment;
        }

        private Entry FocusedEntry => HasComments && focusIndex >= 2 && focusIndex - 2 < entries.Count ? entries[focusIndex - 2] : null;

        private (float top, float bottom) Bounds(int index)
        {
            switch (PartOf(index))
            {
                case Part.Sort:
                    return (0, 0);
                case Part.Form:
                    return (formTop, formTop + 56);
                default:
                    Entry e = entries[index - 2];
                    return (listTop + e.View.PositionY, listTop + e.View.PositionY + e.Height);
            }
        }

        private void Refresh()
        {
            bool sort = focusIndex == 0;
            sortButton.BackgroundColor = sort ? Theme.CardInfoFocused : new Color(1, 1, 1, 0.10f);
            sortLabel.TextColor = sort ? Theme.CardTextFocused : Theme.Text;
            sortIcon.Color = sort ? new Color(0.16f, 0.16f, 0.16f, 1f) : Color.White;

            bool field = focusIndex == 1;
            formLine.BackgroundColor = field ? Theme.Text : Theme.TextMuted;
            formLine.SizeHeight = field ? 3 : 2;
            form.BackgroundColor = field ? new Color(1, 1, 1, 0.08f) : Color.Transparent;
            Widgets.Round(form, 12);

            for (int i = 0; i < entries.Count; i++)
            {
                Widgets.SetVisible(entries[i].Highlight, focusIndex == i + 2);
            }
            Entry current = FocusedEntry;
            if (painted != null && painted != current)
            {
                PaintTools(painted, -1);
            }
            if (current != null)
            {
                PaintTools(current, toolIndex);
            }
            painted = current;
        }

        private void Reveal(int direction)
        {
            (float top, float bottom) = Bounds(focusIndex);
            float margin = 20;
            if (bottom - top > ViewportHeight - 2 * margin)
            {
                scrollY = direction > 0 ? top - margin : bottom - ViewportHeight + margin;
            }
            else if (top - margin < scrollY)
            {
                scrollY = top - margin;
            }
            else if (bottom + margin > scrollY + ViewportHeight)
            {
                scrollY = bottom + margin - ViewportHeight;
            }
            scrollY = Math.Max(0, Math.Min(MaxScroll, scrollY));
            scrollAnim = Widgets.Slide(content, 0, -scrollY, scrollAnim, 160);
        }

        // Traps all focus inside the comments panel until Back / Escape is pressed.
        public bool Key(string key)
        {
            if (entry != null)
            {
                entry.Key(key);
                return true;
            }
            if (key == "XF86Back" || key == "Escape")
            {
                return false;
            }
            (float top, float bottom) = Bounds(focusIndex);
            switch (key)
            {
                case "Down":
                    if (focusIndex >= 1 && bottom + 20 > scrollY + ViewportHeight && scrollY < MaxScroll)
                    {
                        scrollY = Math.Min(Math.Min(MaxScroll, bottom + 20 - ViewportHeight), scrollY + 320);
                        scrollAnim = Widgets.Slide(content, 0, -scrollY, scrollAnim, 160);
                        break;
                    }
                    if (focusIndex < LastIndex)
                    {
                        focusIndex++;
                        if (focusIndex == 1 && !form.Visibility && LastIndex >= 2)
                        {
                            focusIndex = 2;
                        }
                        toolIndex = -1;
                        Reveal(+1);
                        MaybeLoadMore();
                    }
                    break;
                case "Up":
                    if (focusIndex >= 1 && top - 20 < scrollY && scrollY > 0)
                    {
                        scrollY = Math.Max(Math.Max(0, top - 20), scrollY - 320);
                        scrollAnim = Widgets.Slide(content, 0, -scrollY, scrollAnim, 160);
                        break;
                    }
                    if (focusIndex > 0)
                    {
                        focusIndex--;
                        if (focusIndex == 1 && !form.Visibility)
                        {
                            focusIndex = 0;
                        }
                        toolIndex = -1;
                        Reveal(-1);
                    }
                    break;
                case "Left":
                case "Right":
                    if (FocusedEntry != null)
                    {
                        toolIndex = Math.Max(-1, Math.Min(FocusedEntry.Tools.Count - 1, toolIndex + (key == "Right" ? 1 : -1)));
                    }
                    break;
                case "Return":
                    Activate();
                    break;
            }
            Refresh();
            return true;
        }

        private void Activate()
        {
            switch (PartOf(focusIndex))
            {
                case Part.Sort:
                    ToggleSort();
                    break;
                case Part.Form:
                    OpenEntry();
                    break;
                case Part.Comment:
                    Entry e = entries[focusIndex - 2];
                    if (e.Comment == null)
                    {
                        LoadMoreReplies(e);
                    }
                    else if (toolIndex >= 0)
                    {
                        UseTool(e, e.Tools[toolIndex].Kind);
                    }
                    else if (e.Truncated)
                    {
                        e.Expanded = !e.Expanded;
                        FillEntry(e);
                        ArrangeParts();
                        Reveal(e.Expanded ? +1 : -1);
                    }
                    else if (e.Comment.ReplyToken.Length > 0)
                    {
                        ToggleReplies(e);
                    }
                    break;
            }
        }

        private void UseTool(Entry e, Tool kind)
        {
            switch (kind)
            {
                case Tool.Like:
                    ToggleVote(e, true);
                    break;
                case Tool.Dislike:
                    ToggleVote(e, false);
                    break;
                case Tool.Reply:
                    OpenEntry(e);
                    break;
                case Tool.Replies:
                    ToggleReplies(e);
                    break;
            }
        }

        private void ToggleVote(Entry e, bool like)
        {
            if (!YouTube.SignedIn())
            {
                Toast?.Invoke(T("Sign in to vote"));
                return;
            }
            if (e.Voting)
            {
                return;
            }
            bool wasLiked = e.Comment.Liked, wasDisliked = e.Comment.Disliked;
            string wasLikes = e.Comment.Likes;
            string action = like ? (wasLiked ? "unlike" : "like") : (wasDisliked ? "undislike" : "dislike");
            e.Comment.Liked = like && !wasLiked;
            e.Comment.Disliked = !like && !wasDisliked;
            e.Comment.Likes = AdjustLikes(wasLikes, (e.Comment.Liked ? 1 : 0) - (wasLiked ? 1 : 0));
            BuildTools(e);
            e.Voting = true;
            UiGuard.Fire("CommentsPanel.Vote", () => SendVote(e, action, wasLiked, wasDisliked, wasLikes));
        }

        private async Task SendVote(Entry e, string action, bool wasLiked, bool wasDisliked, string wasLikes)
        {
            int gen = generation;
            string error = await YouTube.CommentAction(videoId, e.Comment.Id, action);
            if (gen != generation || closed)
            {
                return;
            }
            e.Voting = false;
            if (error != null)
            {
                Tizen.Log.Warn("omegatube-modern", "comment vote: " + error);
                e.Comment.Liked = wasLiked;
                e.Comment.Disliked = wasDisliked;
                e.Comment.Likes = wasLikes;
                BuildTools(e);
                Toast?.Invoke(T("Action failed"));
            }
        }

        private static string AdjustLikes(string text, int delta)
        {
            if (delta == 0 || !int.TryParse(text, out int n))
            {
                return text;
            }
            n = Math.Max(0, n + delta);
            return n == 0 ? "" : n.ToString();
        }

        private void ToggleReplies(Entry e)
        {
            if (e.RepliesLoading)
            {
                return;
            }
            if (e.RepliesOpen)
            {
                e.RepliesOpen = false;
                RemoveChildren(e);
                BuildTools(e);
                ArrangeParts();
                return;
            }
            if (e.Children.Count > 0)
            {
                e.RepliesOpen = true;
                int at = entries.IndexOf(e) + 1;
                foreach (Entry r in e.Children)
                {
                    list.Add(r.View);
                    entries.Insert(at++, r);
                }
                BuildTools(e);
                ArrangeParts();
                return;
            }
            string token = e.Comment.ReplyToken;
            if (token.Length == 0)
            {
                return;
            }
            e.RepliesLoading = true;
            UiGuard.Fire("CommentsPanel.LoadReplies", () => LoadReplies(e, token, null));
        }

        private void LoadMoreReplies(Entry row)
        {
            if (row.Parent.RepliesLoading || row.MoreToken.Length == 0)
            {
                return;
            }
            string token = row.MoreToken;
            row.Parent.RepliesLoading = true;
            ((TextLabel)row.View.GetChildAt(1)).Text = T("Loading…");
            UiGuard.Fire("CommentsPanel.LoadReplies", () => LoadReplies(row.Parent, token, row));
        }

        private async Task LoadReplies(Entry parent, string token, Entry moreRow)
        {
            int gen = generation;
            CommentPage page = await YouTube.Comments(videoId, token);
            if (gen != generation || closed)
            {
                return;
            }
            parent.RepliesLoading = false;
            if (page.Error != null)
            {
                if (moreRow != null)
                {
                    ((TextLabel)moreRow.View.GetChildAt(1)).Text = T("Show more replies");
                }
                Toast?.Invoke(T("Replies unavailable"));
                return;
            }
            if (moreRow != null)
            {
                entries.Remove(moreRow);
                parent.Children.Remove(moreRow);
                list.Remove(moreRow.View);
                moreRow.View.Dispose();
            }
            parent.RepliesOpen = true;
            int at = entries.IndexOf(parent) + 1 + parent.Children.Count;
            foreach (Comment c in page.Items)
            {
                if (entries.Count >= MaxEntries)
                {
                    page.Continuation = "";
                    break;
                }
                AddEntry(c, at++, parent, ReplyIndent);
            }
            if (page.Continuation.Length > 0)
            {
                AddMoreRow(parent, at, page.Continuation);
            }
            BuildTools(parent);
            ArrangeParts();
            Refresh();
        }

        private void AddMoreRow(Entry parent, int at, string token)
        {
            int w = ContentWidth - ReplyIndent;
            var view = new View { Size = new Size(w, 42), PositionX = ReplyIndent };
            var highlight = Widgets.Rounded(new Color(1, 1, 1, 0.10f), w + 12, 42, 12);
            highlight.Position = new Position(-6, 0);
            highlight.Hide();
            view.Add(highlight);
            TextLabel label = Widgets.Bold(Widgets.Label(T("Show more replies"), MetaFont, Theme.Text, w - 16, 42));
            label.VerticalAlignment = VerticalAlignment.Center;
            label.PositionX = 8;
            view.Add(label);
            var row = new Entry { Parent = parent, MoreToken = token, View = view, Highlight = highlight, Height = 42 };
            list.Add(view);
            entries.Insert(at, row);
            parent.Children.Add(row);
        }

        private void RemoveChildren(Entry parent)
        {
            foreach (Entry r in parent.Children)
            {
                entries.Remove(r);
                list.Remove(r.View);
            }
            if (painted != null && painted.Parent == parent)
            {
                painted = null;
            }
        }

        private void OpenEntry(Entry target = null)
        {
            if (entry != null)
            {
                return;
            }
            if (!YouTube.SignedIn())
            {
                Toast?.Invoke(T("Sign in to comment"));
                return;
            }
            if (target == null && createParams.Length == 0)
            {
                Toast?.Invoke(loading ? T("Loading comments…") : T("Comments can't be posted on this video"));
                return;
            }
            replyTarget = target;
            if (target == null)
            {
                entry = new TextEntry(T("Add a comment"), T("Add a comment…"), draft, MaxCommentLength);
            }
            else
            {
                string author = target.Comment.Author;
                string initial = target.Parent != null && author.Length > 0 ? author + " " : "";
                entry = new TextEntry(T("Reply to {0}", author.Length > 0 ? author : T("Unknown")), T("Add a reply…"), initial, MaxCommentLength);
            }
            entry.Done = text => UiGuard.Run("CommentsPanel.EntryDone", () => EntryDone(text));
            overlayHost.Add(entry);
        }

        private void EntryDone(string text)
        {
            if (text == null)
            {
                if (replyTarget == null)
                {
                    draft = entry.Text;
                }
                CloseEntry();
                return;
            }
            if (posting)
            {
                return;
            }
            UiGuard.Fire("CommentsPanel.Post", () => Post(text));
        }

        private void CloseEntry()
        {
            if (entry == null)
            {
                return;
            }
            overlayHost.Remove(entry);
            entry.Dispose();
            entry = null;
            replyTarget = null;
            UpdateForm();
            Refresh();
        }

        private async Task Post(string text)
        {
            Entry target = replyTarget;
            Entry thread = target?.Parent ?? target;
            posting = true;
            entry?.SetBusy(true);
            (Comment created, string error) = target == null
                ? await YouTube.PostComment(createParams, text)
                : await YouTube.PostReply(videoId, thread.Comment.Id, text);
            posting = false;
            if (closed)
            {
                return;
            }
            entry?.SetBusy(false);
            if (error != null)
            {
                Tizen.Log.Warn("omegatube-modern", "post comment: " + error);
                Toast?.Invoke(T("Comment not posted: {0}", error));
                return;
            }
            if (target == null)
            {
                draft = "";
            }
            CloseEntry();
            Comment shown = created ?? new Comment { Author = myHandle ?? "", Avatar = myPhoto ?? "", Text = text };
            if (shown.Published.Length == 0)
            {
                shown.Published = T("just now");
            }
            if (string.IsNullOrEmpty(shown.Avatar) && !string.IsNullOrEmpty(myPhoto))
            {
                shown.Avatar = myPhoto;
            }
            status.Hide();
            int at = target == null ? 0 : entries.IndexOf(target) + 1;
            AddEntry(shown, at, thread);
            ArrangeParts();
            focusIndex = 2 + at;
            toolIndex = -1;
            Reveal(+1);
            Refresh();
            Toast?.Invoke(target == null ? T("Comment posted") : T("Reply posted"));
        }

        private static int CharsPerLine(int width, float pixelSize) => Math.Max(10, (int)(width / (pixelSize * 0.55f)));

        private static float EstimateWidth(string text, float pixelSize) => text.Length * pixelSize * 0.55f;

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
