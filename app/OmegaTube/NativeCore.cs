using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OmegaTube
{
    // Bindings to libomegacore.so (see core/include/omega/core.h).
    public static class NativeCore
    {
        private const string Lib = "omegacore";

        private const int RtldNow = 2;

        [DllImport("libdl.so.2", EntryPoint = "dlopen", SetLastError = true)]
        private static extern IntPtr DlOpen(string file, int mode);

        [DllImport("libdl.so.2", EntryPoint = "dlsym", SetLastError = true)]
        private static extern IntPtr DlSym(IntPtr handle, string name);

        [DllImport("libdl.so.2", EntryPoint = "dlerror")]
        private static extern IntPtr DlError();

        // "file path" or the dlopen failure detail (shown in bug reports).
        public static string LoadRoute { get; private set; } = "not attempted";

        // Resolves an exported symbol of the loaded core.
        private static Func<string, IntPtr> sym;
        private static readonly object loadLock = new object();
        private static bool loaded;

        // Kept for the call site in OnCreate; loads the core (see EnsureLoaded).
        public static void RegisterResolver() => EnsureLoaded();

        // Loads the core once via dlopen, then binds the function pointers.
        public static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }
            lock (loadLock)
            {
                if (loaded)
                {
                    return;
                }
                string path = CorePath();

                IntPtr handle = DlOpen(path, RtldNow);
                if (handle != IntPtr.Zero)
                {
                    sym = n => DlSym(handle, n);
                    LoadRoute = "file path";
                    Bind();
                    loaded = true;
                    return;
                }
                LoadRoute = "dlopen failed: " + DlErrorString();
                Tizen.Log.Error("OmegaTube", LoadRoute);
                throw new DllNotFoundException(LoadRoute);
            }
        }

        private static T Fn<T>(string name) where T : Delegate
        {
            IntPtr p = sym(name);
            if (p == IntPtr.Zero)
            {
                throw new EntryPointNotFoundException("omegacore: " + name);
            }
            return Marshal.GetDelegateForFunctionPointer<T>(p);
        }

        private static string DlErrorString()
        {
            IntPtr p = DlError();
            return p == IntPtr.Zero ? "unknown error" : (Marshal.PtrToStringAnsi(p) ?? "unknown error");
        }

        // app/OmegaTube/lib/libomegacore.so at runtime: a sibling of the resource folder.
        private static string CorePath()
        {
            string res = Tizen.Applications.Application.Current.DirectoryInfo.Resource;
            return Path.GetFullPath(Path.Combine(res, "..", "lib", "libomegacore.so"));
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PlayerStats
        {
            public int State;
            public int Eos;
            public long PositionMs;
            public long VideoQueuedMs;
            public long AudioQueuedMs;
            public long VideoSubmittedMs;
            public long AudioSubmittedMs;
            public long LongestStallMs;
            public uint VideoUnderruns;
            public uint AudioUnderruns;
            public uint Stalls;
            public uint Errors;
            public double VideoKbps;
            public double AudioKbps;
            public long DurationMs;
            public int Reconnecting;
            public int Reserved;
        }

        // --- Delegate types for the core's C API (cdecl; ANSI for the few char* taken as string) --------------
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Ip_v();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int I_v();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int I_b(byte[] a);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void V_v();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Ip_b(byte[] a);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Ip_bb(byte[] a, byte[] b);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Ip_bbb(byte[] a, byte[] b, byte[] c);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Ip_bi(byte[] a, int b);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void V_b(byte[] a);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void V_bb(byte[] a, byte[] b);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int I_buf(byte[] buf, int size, int maxLines);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void V_ip(IntPtr p);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Ip_ip(IntPtr p);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int I_ipl(IntPtr p, long a);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int I_ipd(IntPtr p, double a);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void V_ipStats(IntPtr p, out PlayerStats stats);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private delegate void LogInitFn(string host, int port);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private delegate void LogLineFn(string text);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private delegate IntPtr CreateFn(IntPtr win, int w, int h, string videoUrl, string audioUrl);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private delegate IntPtr CreateYtFn(IntPtr win, int w, int h, string videoId, int maxHeight, string codecs, long startMs, string audioTrack);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private delegate IntPtr CreatePreviewFn(IntPtr win, int w, int h, string videoId, int maxHeight, string codecs, long startMs);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private delegate void DiagnoseFn(IntPtr win, int w, int h, string videoUrl, string audioUrl);

        // --- Bound function pointers -------------------------------------------------------------------------
        private static Ip_v _version, _playerLastError, _configRefresh, _updateCheck, _authStatus, _authBegin, _authAccount, _authAccounts;
        private static I_v _selfTest, _reportAvailable;
        private static V_v _authSignOut;
        private static I_buf _logTail;
        private static LogInitFn _logInitUdp;
        private static LogLineFn _logLine;
        private static CreateFn _playerCreate;
        private static CreateYtFn _playerCreateYouTube;
        private static CreatePreviewFn _playerCreatePreview;
        private static I_b _previewPrefetch;
        private static DiagnoseFn _playerDiagnose;
        private static V_ip _playerTogglePause, _playerDestroy, _free;
        private static Ip_ip _playerFormats;
        private static I_ipl _playerSeek;
        private static I_ipd _playerSetRate;
        private static V_ipStats _playerGetStats;
        private static Ip_b _ytSuggest, _reportUpload, _authPoll, _qrEncode, _ytRelated, _ytDetails, _sbSegments, _authSwitch;
        private static Ip_bb _ytSearch, _ytBrowse, _ytPlaylist, _ytPlaylistPage, _ytCaptions, _ytRate, _ytChannel, _ytComments, _ytPostComment;
        private static Ip_bbb _previewSheet;
        private static Ip_bbb _ytPostReply, _ytCommentAction;
        private static Ip_bi _ytSubscribe;
        private static V_b _init;
        private static V_bb _setLocale;

        private static void Bind()
        {
            _version = Fn<Ip_v>("omega_version");
            _selfTest = Fn<I_v>("omega_selftest");
            _logInitUdp = Fn<LogInitFn>("omega_log_init_udp");
            _logLine = Fn<LogLineFn>("omega_log_line");
            _logTail = Fn<I_buf>("omega_log_tail");
            _playerCreate = Fn<CreateFn>("omega_player_create");
            _playerCreateYouTube = Fn<CreateYtFn>("omega_player_create_youtube");
            _playerCreatePreview = Fn<CreatePreviewFn>("omega_player_create_preview");
            _previewPrefetch = Fn<I_b>("omega_preview_prefetch");
            _playerLastError = Fn<Ip_v>("omega_player_last_error");
            _playerTogglePause = Fn<V_ip>("omega_player_toggle_pause");
            _playerSeek = Fn<I_ipl>("omega_player_seek");
            _playerGetStats = Fn<V_ipStats>("omega_player_get_stats");
            _playerDestroy = Fn<V_ip>("omega_player_destroy");
            _playerDiagnose = Fn<DiagnoseFn>("omega_player_diagnose");
            _ytSearch = Fn<Ip_bb>("omega_yt_search");
            _ytSuggest = Fn<Ip_b>("omega_yt_suggest");
            _ytBrowse = Fn<Ip_bb>("omega_yt_browse");
            _init = Fn<V_b>("omega_init");
            _configRefresh = Fn<Ip_v>("omega_config_refresh");
            _setLocale = Fn<V_bb>("omega_set_locale");
            _updateCheck = Fn<Ip_v>("omega_update_check");
            _reportAvailable = Fn<I_v>("omega_report_available");
            _reportUpload = Fn<Ip_b>("omega_report_upload");
            _authStatus = Fn<Ip_v>("omega_auth_status");
            _authBegin = Fn<Ip_v>("omega_auth_begin");
            _authPoll = Fn<Ip_b>("omega_auth_poll");
            _authSignOut = Fn<V_v>("omega_auth_sign_out");
            _qrEncode = Fn<Ip_b>("omega_qr_encode");
            _ytPlaylist = Fn<Ip_bb>("omega_yt_playlist");
            _ytPlaylistPage = Fn<Ip_bb>("omega_yt_playlist_page");
            _ytRelated = Fn<Ip_b>("omega_yt_related");
            _playerFormats = Fn<Ip_ip>("omega_player_formats");
            _playerSetRate = Fn<I_ipd>("omega_player_set_rate");
            _ytCaptions = Fn<Ip_bb>("omega_yt_captions");
            _ytDetails = Fn<Ip_b>("omega_yt_details");
            _ytRate = Fn<Ip_bb>("omega_yt_rate");
            _ytSubscribe = Fn<Ip_bi>("omega_yt_subscribe");
            _ytChannel = Fn<Ip_bb>("omega_yt_channel");
            _ytComments = Fn<Ip_bb>("omega_yt_comments");
            _previewSheet = Fn<Ip_bbb>("omega_preview_sheet");
            _ytPostComment = Fn<Ip_bb>("omega_yt_post_comment");
            _ytPostReply = Fn<Ip_bbb>("omega_yt_post_reply");
            _ytCommentAction = Fn<Ip_bbb>("omega_yt_comment_action");
            _authAccount = Fn<Ip_v>("omega_auth_account");
            _authAccounts = Fn<Ip_v>("omega_auth_accounts");
            _authSwitch = Fn<Ip_b>("omega_auth_switch");
            _sbSegments = Fn<Ip_b>("omega_sb_segments");
            _free = Fn<V_ip>("omega_free");
        }

        // --- Public API (unchanged signatures; call sites are untouched) --------------------------------------
        public static int SelfTest() => _selfTest();
        public static void LogInitUdp(string host, int port) => _logInitUdp(host, port);
        public static void Log(string text) => _logLine(text);
        public static IntPtr PlayerCreate(IntPtr ecoreWindow, int width, int height, string videoUrl, string audioUrl) => _playerCreate(ecoreWindow, width, height, videoUrl, audioUrl);
        public static IntPtr PlayerCreateYouTube(IntPtr ecoreWindow, int width, int height, string videoId, int maxHeight, string codecs, long startMs, string audioTrack) => _playerCreateYouTube(ecoreWindow, width, height, videoId, maxHeight, codecs, startMs, audioTrack);
        public static IntPtr PlayerLastError() => _playerLastError();
        public static void PlayerTogglePause(IntPtr player) => _playerTogglePause(player);
        public static int PlayerSeek(IntPtr player, long positionMs) => _playerSeek(player, positionMs);
        // Muted, video-only preview with no watch-history reporting (omega_player_create_preview). Blocking.
        public static IntPtr PlayerCreatePreview(IntPtr ecoreWindow, int width, int height, string videoId, int maxHeight, string codecs, long startMs) => _playerCreatePreview(ecoreWindow, width, height, videoId, maxHeight, codecs, startMs);
        // Resolves a video's streams ahead of its preview (no decoder, no history). Blocking. True when cached.
        public static bool PreviewPrefetch(string videoId) => _previewPrefetch(Utf8z(videoId)) != 0;
        public static void PlayerGetStats(IntPtr player, out PlayerStats stats) => _playerGetStats(player, out stats);
        public static void PlayerDestroy(IntPtr player) => _playerDestroy(player);
        public static void PlayerDiagnose(IntPtr ecoreWindow, int width, int height, string videoUrl, string audioUrl) => _playerDiagnose(ecoreWindow, width, height, videoUrl, audioUrl);
        public static IntPtr PlayerFormats(IntPtr player) => _playerFormats(player);
        public static int PlayerSetRate(IntPtr player, double rate) => _playerSetRate(player, rate);

        public static IntPtr YtSearch(string query, string continuation) => _ytSearch(Utf8z(query), Utf8z(continuation));
        public static IntPtr YtSuggest(string query) => _ytSuggest(Utf8z(query));
        public static IntPtr YtBrowse(string browseId, string continuation) => _ytBrowse(Utf8z(browseId), Utf8z(continuation));
        public static void Init(string dataDir) { EnsureLoaded(); _init(Utf8z(dataDir)); }
        public static IntPtr ConfigRefresh() => _configRefresh();
        public static void SetLocale(string language, string region) => _setLocale(Utf8z(language), Utf8z(region));
        public static IntPtr UpdateCheck() => _updateCheck();
        public static int ReportAvailable() => _reportAvailable();
        public static IntPtr ReportUpload(string text) => _reportUpload(Utf8z(text));
        public static IntPtr AuthStatus() => _authStatus();
        public static IntPtr AuthBegin() => _authBegin();
        public static IntPtr AuthPoll(string deviceCode) => _authPoll(Utf8z(deviceCode));
        public static void AuthSignOut() => _authSignOut();
        public static IntPtr QrEncode(string text) => _qrEncode(Utf8z(text));
        public static IntPtr YtPlaylist(string videoId, string playlistId) => _ytPlaylist(Utf8z(videoId), Utf8z(playlistId));
        public static IntPtr YtPlaylistPage(string playlistId, string continuation) => _ytPlaylistPage(Utf8z(playlistId), Utf8z(continuation));
        public static IntPtr YtRelated(string videoId) => _ytRelated(Utf8z(videoId));
        public static IntPtr YtCaptions(string url, string translateTo) => _ytCaptions(Utf8z(url), Utf8z(translateTo));
        public static IntPtr YtDetails(string videoId) => _ytDetails(Utf8z(videoId));
        public static IntPtr YtRate(string videoId, string rating) => _ytRate(Utf8z(videoId), Utf8z(rating));
        public static IntPtr YtSubscribe(string channelId, bool subscribe) => _ytSubscribe(Utf8z(channelId), subscribe ? 1 : 0);
        public static IntPtr YtComments(string videoId, string continuation) => _ytComments(Utf8z(videoId), Utf8z(continuation));
        // Animated WebP preview -> BMP sprite sheet at outPath; url "" = look it up by id (see omega_preview_sheet).
        public static IntPtr PreviewSheet(string videoId, string url, string outPath) =>
            _previewSheet(Utf8z(videoId), Utf8z(url), Utf8z(outPath));
        public static IntPtr YtPostComment(string createParams, string text) => _ytPostComment(Utf8z(createParams), Utf8z(text));
        public static IntPtr YtPostReply(string videoId, string commentId, string text) => _ytPostReply(Utf8z(videoId), Utf8z(commentId), Utf8z(text));
        public static IntPtr YtCommentAction(string videoId, string commentId, string action) => _ytCommentAction(Utf8z(videoId), Utf8z(commentId), Utf8z(action));
        public static IntPtr YtChannel(string channelId, string continuation) => _ytChannel(Utf8z(channelId), Utf8z(continuation));
        public static IntPtr AuthAccount() => _authAccount();
        public static IntPtr AuthAccounts() => _authAccounts();
        public static IntPtr AuthSwitch(string id) => _authSwitch(Utf8z(id));
        public static IntPtr SbSegments(string videoId) => _sbSegments(Utf8z(videoId));
        public static void Free(IntPtr p) => _free(p);

        // Managed string -> NUL-terminated UTF-8 (the default ANSI marshalling would mangle non-ASCII).
        private static byte[] Utf8z(string s)
        {
            byte[] raw = System.Text.Encoding.UTF8.GetBytes(s ?? "");
            Array.Resize(ref raw, raw.Length + 1);
            return raw;
        }

        // Native UTF-8 C string -> managed string.
        public static string Utf8(IntPtr p)
        {
            if (p == IntPtr.Zero)
            {
                return "";
            }
            int len = 0;
            while (Marshal.ReadByte(p, len) != 0)
            {
                len++;
            }
            var bytes = new byte[len];
            Marshal.Copy(p, bytes, 0, len);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        public static string Version => Marshal.PtrToStringAnsi(_version());

        public static string RecentLog(int maxLines)
        {
            var buf = new byte[8192];
            int n = _logTail(buf, buf.Length, maxLines);
            return System.Text.Encoding.UTF8.GetString(buf, 0, n);
        }
    }
}
