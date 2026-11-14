using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OmegaTube
{
    // Bindings to libomegacore.so (see core/include/omega/core.h).
    public static class NativeCore
    {
        // "file path" or the NativeLibrary failure detail (shown in bug reports).
        public static string LoadRoute { get; private set; } = "not attempted";

        // Resolves an exported symbol of the loaded core.
        private static IntPtr libHandle;
        private static readonly object loadLock = new object();
        private static bool loaded;

        // Loads the core once via .NET 8 NativeLibrary, then binds the function pointers.
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
                try
                {
                    libHandle = NativeLibrary.Load(path);
                    LoadRoute = "file path";
                    Bind();
                    loaded = true;
                }
                catch (Exception e)
                {
                    LoadRoute = "NativeLibrary.Load failed: " + e.Message;
                    Tizen.Log.Error("omegatube-modern", LoadRoute);
                    throw;
                }
            }
        }

        private static T Fn<T>(string name) where T : Delegate =>
            Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(libHandle, name));

        // app/OmegaTube/lib/libomegacore.so at runtime: a sibling of the resource folder.
        internal static string CorePath()
        {
            string res = Tizen.Applications.Application.Current.DirectoryInfo.Resource;
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(res, "..", "lib", "libomegacore.so"));
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

        // --- Delegate types for the core's C API (cdecl; UTF-8 strings marshalled via LPUTF8Str) --------------
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Ip_v();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int I_v();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void V_v();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Ip_s([MarshalAs(UnmanagedType.LPUTF8Str)] string a);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Ip_ss([MarshalAs(UnmanagedType.LPUTF8Str)] string a, [MarshalAs(UnmanagedType.LPUTF8Str)] string b);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Ip_sss([MarshalAs(UnmanagedType.LPUTF8Str)] string a, [MarshalAs(UnmanagedType.LPUTF8Str)] string b, [MarshalAs(UnmanagedType.LPUTF8Str)] string c);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Ip_si([MarshalAs(UnmanagedType.LPUTF8Str)] string a, int b);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void V_s([MarshalAs(UnmanagedType.LPUTF8Str)] string a);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void V_ss([MarshalAs(UnmanagedType.LPUTF8Str)] string a, [MarshalAs(UnmanagedType.LPUTF8Str)] string b);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int I_buf(byte[] buf, int size, int maxLines);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void V_ip(IntPtr p);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Ip_ip(IntPtr p);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int I_ipl(IntPtr p, long a);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int I_ipd(IntPtr p, double a);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int I_ipiiii(IntPtr p, int a, int b, int c, int d);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void V_ipStats(IntPtr p, out PlayerStats stats);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void LogInitFn([MarshalAs(UnmanagedType.LPUTF8Str)] string host, int port);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr CreateYtFn(IntPtr win, int w, int h, [MarshalAs(UnmanagedType.LPUTF8Str)] string videoId, int maxHeight, [MarshalAs(UnmanagedType.LPUTF8Str)] string codecs, long startMs, [MarshalAs(UnmanagedType.LPUTF8Str)] string audioTrack);

        // --- Bound function pointers -------------------------------------------------------------------------
        private static Ip_v _version, _playerLastError, _configRefresh, _updateCheck, _authStatus, _authBegin, _authAccount, _authAccounts;
        private static I_v _reportAvailable;
        private static V_v _authSignOut;
        private static I_buf _logTail;
        private static LogInitFn _logInitUdp;
        private static V_s _logLine;
        private static CreateYtFn _playerCreateYouTube;
        private static V_ip _playerTogglePause, _playerDestroy, _free;
        private static Ip_ip _playerFormats;
        private static I_ipl _playerSeek;
        private static I_ipd _playerSetRate;
        private static I_ipiiii _playerSetDisplayRect;
        private static V_ipStats _playerGetStats;
        private static Ip_s _ytSuggest, _reportUpload, _authPoll, _qrEncode, _ytDetails, _sbSegments, _authSwitch;
        private static Ip_ss _ytSearch, _ytBrowse, _ytPlaylist, _ytPlaylistPage, _ytRelated, _ytCaptions, _ytRate, _ytChannel, _ytComments, _ytPostComment;
        private static Ip_sss _ytPostReply, _ytCommentAction;
        private static Ip_si _ytSubscribe;
        private static V_s _init;
        private static V_ss _setLocale;

        private static void Bind()
        {
            _version = Fn<Ip_v>("omega_version");
            _logInitUdp = Fn<LogInitFn>("omega_log_init_udp");
            _logLine = Fn<V_s>("omega_log_line");
            _logTail = Fn<I_buf>("omega_log_tail");
            _playerCreateYouTube = Fn<CreateYtFn>("omega_player_create_youtube");
            _playerLastError = Fn<Ip_v>("omega_player_last_error");
            _playerTogglePause = Fn<V_ip>("omega_player_toggle_pause");
            _playerSeek = Fn<I_ipl>("omega_player_seek");
            _playerGetStats = Fn<V_ipStats>("omega_player_get_stats");
            _playerDestroy = Fn<V_ip>("omega_player_destroy");
            _ytSearch = Fn<Ip_ss>("omega_yt_search");
            _ytSuggest = Fn<Ip_s>("omega_yt_suggest");
            _ytBrowse = Fn<Ip_ss>("omega_yt_browse");
            _init = Fn<V_s>("omega_init");
            _configRefresh = Fn<Ip_v>("omega_config_refresh");
            _setLocale = Fn<V_ss>("omega_set_locale");
            _updateCheck = Fn<Ip_v>("omega_update_check");
            _reportAvailable = Fn<I_v>("omega_report_available");
            _reportUpload = Fn<Ip_s>("omega_report_upload");
            _authStatus = Fn<Ip_v>("omega_auth_status");
            _authBegin = Fn<Ip_v>("omega_auth_begin");
            _authPoll = Fn<Ip_s>("omega_auth_poll");
            _authSignOut = Fn<V_v>("omega_auth_sign_out");
            _qrEncode = Fn<Ip_s>("omega_qr_encode");
            _ytPlaylist = Fn<Ip_ss>("omega_yt_playlist");
            _ytPlaylistPage = Fn<Ip_ss>("omega_yt_playlist_page");
            _ytRelated = Fn<Ip_ss>("omega_yt_related");
            _playerFormats = Fn<Ip_ip>("omega_player_formats");
            _playerSetRate = Fn<I_ipd>("omega_player_set_rate");
            _playerSetDisplayRect = Fn<I_ipiiii>("omega_player_set_display_rect");
            _ytCaptions = Fn<Ip_ss>("omega_yt_captions");
            _ytDetails = Fn<Ip_s>("omega_yt_details");
            _ytRate = Fn<Ip_ss>("omega_yt_rate");
            _ytSubscribe = Fn<Ip_si>("omega_yt_subscribe");
            _ytChannel = Fn<Ip_ss>("omega_yt_channel");
            _ytComments = Fn<Ip_ss>("omega_yt_comments");
            _ytPostComment = Fn<Ip_ss>("omega_yt_post_comment");
            _ytPostReply = Fn<Ip_sss>("omega_yt_post_reply");
            _ytCommentAction = Fn<Ip_sss>("omega_yt_comment_action");
            _authAccount = Fn<Ip_v>("omega_auth_account");
            _authAccounts = Fn<Ip_v>("omega_auth_accounts");
            _authSwitch = Fn<Ip_s>("omega_auth_switch");
            _sbSegments = Fn<Ip_s>("omega_sb_segments");
            _free = Fn<V_ip>("omega_free");
        }

        // --- Public API ---------------------------------------------------------------------------------------
        public static void LogInitUdp(string host, int port) => _logInitUdp(host ?? "", port);
        public static void Log(string text) => _logLine(text ?? "");
        public static IntPtr PlayerCreateYouTube(IntPtr ecoreWindow, int width, int height, string videoId, int maxHeight, string codecs, long startMs, string audioTrack) =>
            _playerCreateYouTube(ecoreWindow, width, height, videoId ?? "", maxHeight, codecs ?? "", startMs, audioTrack ?? "");
        public static IntPtr PlayerLastError() => _playerLastError();
        public static void PlayerTogglePause(IntPtr player) => _playerTogglePause(player);
        public static int PlayerSeek(IntPtr player, long positionMs) => _playerSeek(player, positionMs);
        public static void PlayerGetStats(IntPtr player, out PlayerStats stats) => _playerGetStats(player, out stats);
        public static void PlayerDestroy(IntPtr player) => _playerDestroy(player);
        public static IntPtr PlayerFormats(IntPtr player) => _playerFormats(player);
        public static int PlayerSetRate(IntPtr player, double rate) => _playerSetRate(player, rate);
        public static int PlayerSetDisplayRect(IntPtr player, int x, int y, int w, int h) => _playerSetDisplayRect(player, x, y, w, h);

        public static IntPtr YtSearch(string query, string continuation) => _ytSearch(query ?? "", continuation ?? "");
        public static IntPtr YtSuggest(string query) => _ytSuggest(query ?? "");
        public static IntPtr YtBrowse(string browseId, string continuation) => _ytBrowse(browseId ?? "", continuation ?? "");
        public static void Init(string dataDir) { EnsureLoaded(); _init(dataDir ?? ""); }
        public static IntPtr ConfigRefresh() => _configRefresh();
        public static void SetLocale(string language, string region) => _setLocale(language ?? "", region ?? "");
        public static IntPtr UpdateCheck() => _updateCheck();
        public static int ReportAvailable() => _reportAvailable();
        public static IntPtr ReportUpload(string text) => _reportUpload(text ?? "");
        public static IntPtr AuthStatus() => _authStatus();
        public static IntPtr AuthBegin() => _authBegin();
        public static IntPtr AuthPoll(string deviceCode) => _authPoll(deviceCode ?? "");
        public static void AuthSignOut() => _authSignOut();
        public static IntPtr QrEncode(string text) => _qrEncode(text ?? "");
        public static IntPtr YtPlaylist(string videoId, string playlistId) => _ytPlaylist(videoId ?? "", playlistId ?? "");
        public static IntPtr YtPlaylistPage(string playlistId, string continuation) => _ytPlaylistPage(playlistId ?? "", continuation ?? "");
        public static IntPtr YtRelated(string videoId, string continuation = "") => _ytRelated(videoId ?? "", continuation ?? "");
        public static IntPtr YtCaptions(string url, string translateTo) => _ytCaptions(url ?? "", translateTo ?? "");
        public static IntPtr YtDetails(string videoId) => _ytDetails(videoId ?? "");
        public static IntPtr YtRate(string videoId, string rating) => _ytRate(videoId ?? "", rating ?? "");
        public static IntPtr YtSubscribe(string channelId, bool subscribe) => _ytSubscribe(channelId ?? "", subscribe ? 1 : 0);
        public static IntPtr YtComments(string videoId, string continuation) => _ytComments(videoId ?? "", continuation ?? "");
        public static IntPtr YtPostComment(string createParams, string text) => _ytPostComment(createParams ?? "", text ?? "");
        public static IntPtr YtPostReply(string videoId, string commentId, string text) => _ytPostReply(videoId ?? "", commentId ?? "", text ?? "");
        public static IntPtr YtCommentAction(string videoId, string commentId, string action) => _ytCommentAction(videoId ?? "", commentId ?? "", action ?? "");
        public static IntPtr YtChannel(string channelId, string continuation) => _ytChannel(channelId ?? "", continuation ?? "");
        public static IntPtr AuthAccount() => _authAccount();
        public static IntPtr AuthAccounts() => _authAccounts();
        public static IntPtr AuthSwitch(string id) => _authSwitch(id ?? "");
        public static IntPtr SbSegments(string videoId) => _sbSegments(videoId ?? "");
        public static void Free(IntPtr p) => _free(p);

        // Native UTF-8 C string -> managed string.
        public static string Utf8(IntPtr p) => p == IntPtr.Zero ? "" : (Marshal.PtrToStringUTF8(p) ?? "");

        public static string Version => Marshal.PtrToStringUTF8(_version()) ?? "";

        public static string RecentLog(int maxLines)
        {
            var buf = new byte[8192];
            int n = _logTail(buf, buf.Length, maxLines);
            return Encoding.UTF8.GetString(buf, 0, n);
        }
    }
}
