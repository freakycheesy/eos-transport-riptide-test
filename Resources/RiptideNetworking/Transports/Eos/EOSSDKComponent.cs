﻿using Epic.OnlineServices;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.Platform;

using System;
using System.Runtime.InteropServices;

using UnityEngine;

/// <summary>
/// Manages the Epic Online Services SDK
/// Do not destroy this component!
/// The Epic Online Services SDK can only be initialized once,
/// after releasing the SDK the game has to be restarted in order to initialize the SDK again.
/// In the unity editor the OnDestroy function will not run so that we dont have to restart the editor after play.
/// </summary>
namespace Riptide.Transports.Eos {
    [DefaultExecutionOrder(-32000)]
    public class EOSSDK : MonoBehaviour {

        // Unity Inspector shown variables

        [SerializeField]
        private EosApiKey apiKeys;

        [Header("User Login")]
        public bool authInterfaceLogin = false;
        public Epic.OnlineServices.Auth.LoginCredentialType authInterfaceCredentialType = Epic.OnlineServices.Auth.LoginCredentialType.AccountPortal;
        public uint devAuthToolPort = 7878;
        public string devAuthToolCredentialName = "";
        public Epic.OnlineServices.ExternalCredentialType connectInterfaceCredentialType = Epic.OnlineServices.ExternalCredentialType.DeviceidAccessToken;
        public string deviceModel = "PC Windows 64bit";
        [SerializeField] private string displayName = "User";
        public static string DisplayName {
            get {
                return Instance.displayName;
            }
            set {
                Instance.displayName = value;
            }
        }

        [Header("Misc")]
        public LogLevel epicLoggerLevel = LogLevel.Error;

        [SerializeField] private bool collectPlayerMetrics = true;
        public static bool CollectPlayerMetrics {
            get {
                return Instance.collectPlayerMetrics;
            }
        }

        public bool checkForEpicLauncherAndRestart = false;
        public bool delayedInitialization = false;
        public float platformTickIntervalInSeconds = 0.0f;
        private float platformTickTimer = 0f;
        public uint tickBudgetInMilliseconds = 0;

        // End Unity Inspector shown variables

        private ulong authExpirationHandle;


        private string authInterfaceLoginCredentialId = null;
        public static void SetAuthInterfaceLoginCredentialId(string credentialId) => Instance.authInterfaceLoginCredentialId = credentialId;
        private string authInterfaceCredentialToken = null;
        public static void SetAuthInterfaceCredentialToken(string credentialToken) => Instance.authInterfaceCredentialToken = credentialToken;
        private string connectInterfaceCredentialToken = null;
        public static void SetConnectInterfaceCredentialToken(string credentialToken) => Instance.connectInterfaceCredentialToken = credentialToken;

        private PlatformInterface EOS;

        // Interfaces
        public static Epic.OnlineServices.Achievements.AchievementsInterface GetAchievementsInterface() => Instance.EOS.GetAchievementsInterface();
        public static Epic.OnlineServices.Auth.AuthInterface GetAuthInterface() => Instance.EOS.GetAuthInterface();
        public static Epic.OnlineServices.Connect.ConnectInterface GetConnectInterface() => Instance.EOS.GetConnectInterface();
        public static Epic.OnlineServices.Ecom.EcomInterface GetEcomInterface() => Instance.EOS.GetEcomInterface();
        public static Epic.OnlineServices.Friends.FriendsInterface GetFriendsInterface() => Instance.EOS.GetFriendsInterface();
        public static Epic.OnlineServices.Leaderboards.LeaderboardsInterface GetLeaderboardsInterface() => Instance.EOS.GetLeaderboardsInterface();
        public static Epic.OnlineServices.Lobby.LobbyInterface GetLobbyInterface() => Instance.EOS.GetLobbyInterface();
        public static Epic.OnlineServices.Metrics.MetricsInterface GetMetricsInterface() => Instance.EOS.GetMetricsInterface(); // Handled by the transport automatically, only use this interface if Mirror is not used for singleplayer
        public static Epic.OnlineServices.Mods.ModsInterface GetModsInterface() => Instance.EOS.GetModsInterface();
        public static Epic.OnlineServices.P2P.P2PInterface GetP2PInterface() => Instance.EOS.GetP2PInterface();
        public static Epic.OnlineServices.PlayerDataStorage.PlayerDataStorageInterface GetPlayerDataStorageInterface() => Instance.EOS.GetPlayerDataStorageInterface();
        public static Epic.OnlineServices.Presence.PresenceInterface GetPresenceInterface() => Instance.EOS.GetPresenceInterface();
        public static Epic.OnlineServices.Sessions.SessionsInterface GetSessionsInterface() => Instance.EOS.GetSessionsInterface();
        public static Epic.OnlineServices.TitleStorage.TitleStorageInterface GetTitleStorageInterface() => Instance.EOS.GetTitleStorageInterface();
        public static Epic.OnlineServices.UI.UIInterface GetUIInterface() => Instance.EOS.GetUIInterface();
        public static Epic.OnlineServices.UserInfo.UserInfoInterface GetUserInfoInterface() => Instance.EOS.GetUserInfoInterface();


        protected EpicAccountId localUserAccountId;
        public static EpicAccountId LocalUserAccountId {
            get {
                return Instance.localUserAccountId;
            }
        }

        protected string localUserAccountIdString;
        public static string LocalUserAccountIdString {
            get {
                return Instance.localUserAccountIdString;
            }
        }

        protected ProductUserId localUserProductId;
        public static ProductUserId LocalUserProductId {
            get {
                return Instance.localUserProductId;
            }
        }

        protected string localUserProductIdString;
        public static string LocalUserProductIdString {
            get {
                return Instance.localUserProductIdString;
            }
        }

        protected bool initialized;
        public static bool Initialized {
            get {
                return Instance.initialized;
            }
        }

        protected bool isConnecting;
        public static bool IsConnecting {
            get {
                return Instance.isConnecting;
            }
        }

        public static void ResetConnection() {
            Instance.isConnecting = false;
            Instance.initialized = false;
        }

        protected static EOSSDK instance;
        protected static EOSSDK Instance {
            get {
                if (instance == null) {
                    return new GameObject("EOSSDKComponent").AddComponent<EOSSDK>();
                }
                else {
                    return instance;
                }
            }
        }

        public static void Tick() {
            instance.platformTickTimer -= Time.deltaTime;
            instance.EOS.Tick();
        }

        // If we're in editor, we should dynamically load and unload the SDK between play sessions.
        // This allows us to initialize the SDK each time the game is run in editor.
#if UNITY_EDITOR_WIN
        [DllImport("Kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpLibFileName);

        [DllImport("Kernel32.dll")]
        private static extern int FreeLibrary(IntPtr hLibModule);

        [DllImport("Kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        private IntPtr libraryPointer;
#endif

#if UNITY_EDITOR_OSX
        [DllImport("libdl.dylib")]
        private static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl.dylib")]
        private static extern int dlclose(IntPtr handle);

        [DllImport("libdl.dylib")]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        private const int RTLD_NOW = 2; // This flag means to resolve all symbols now.
        private IntPtr libraryPointer;

        public static IntPtr LoadLibrary(string path) {
            return dlopen(path, RTLD_NOW);
        }

        public static int FreeLibrary(IntPtr handle) {
            return dlclose(handle);
        }

        public static IntPtr GetProcAddress(IntPtr handle, string procName) {
            return dlsym(handle, procName);
        }
#endif

#if UNITY_EDITOR_LINUX
        [DllImport("libdl.so", EntryPoint = "dlopen")]
        private static extern IntPtr LoadLibrary(String lpFileName, int flags = 2);   

        [DllImport("libdl.so", EntryPoint = "dlclose")]
        private static extern int FreeLibrary(IntPtr hLibModule);
    
        [DllImport("libdl.so")]
        private static extern IntPtr dlsym(IntPtr handle, String symbol);

        [DllImport("libdl.so")]
        private static extern IntPtr dlerror();

        private static IntPtr GetProcAddress(IntPtr hModule, string lpProcName) {
            // clear previous errors if any
            dlerror();
            var res = dlsym(hModule, lpProcName);
            var errPtr = dlerror();
            if (errPtr != IntPtr.Zero) {
                throw new Exception("dlsym: " + Marshal.PtrToStringAnsi(errPtr));
            }
            return res;
        }    
        private IntPtr libraryPointer;
#endif

        private void Awake() {
            // Initialize Java version of the SDK with a reference to the VM with JNI
            // See https://eoshelp.epicgames.com/s/question/0D54z00006ufJBNCA2/cant-get-createdeviceid-to-work-in-unity-android-c-sdk?language=en_US
            if (Application.platform == RuntimePlatform.Android) {
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                // My custom "LibraryLoader.java" file in /Plugins/Android/lib to load the EOSSDK.so
                // which v1.17.0 removed for some reason...
                AndroidJavaClass libraryLoader = new AndroidJavaClass("com.epicgames.mobile.eossdk.LibraryLoader");
                libraryLoader.CallStatic("load");

                AndroidJavaClass EOS_SDK_JAVA = new AndroidJavaClass("com.epicgames.mobile.eossdk.EOSSDK");
                EOS_SDK_JAVA.CallStatic("init", activity);
            }

            // Prevent multiple instances
            if (instance != null) {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(instance);

#if UNITY_EDITOR
            var libraryPath = "Assets/Resources/EOSSDK/EOSSDK/" + Config.LibraryName;

            libraryPointer = LoadLibrary(libraryPath);
            if (libraryPointer == IntPtr.Zero) {
                throw new Exception("Failed to load library: " + libraryPath);
            }

            Bindings.Hook(libraryPointer, GetProcAddress);
#endif

            if (!delayedInitialization) {
                Initialize();
            }
        }

        protected void InitializeImplementation() {
            isConnecting = true;

            var initializeOptions = new InitializeOptions() {
                ProductName = apiKeys.epicProductName,
                ProductVersion = apiKeys.epicProductVersion
            };

            var initializeResult = PlatformInterface.Initialize(ref initializeOptions);

            // This code is called each time the game is run in the editor, so we catch the case where the SDK has already been initialized in the editor.
            var isAlreadyConfiguredInEditor = Application.isEditor && initializeResult == Result.AlreadyConfigured;
            if (initializeResult != Result.Success && !isAlreadyConfiguredInEditor) {
                throw new System.Exception("Failed to initialize platform: " + initializeResult);
            }

            // The SDK outputs lots of information that is useful for debugging.
            // Make sure to set up the logging interface as early as possible: after initializing.
            LoggingInterface.SetLogLevel(LogCategory.AllCategories, epicLoggerLevel);
            LoggingInterface.SetCallback((ref LogMessage message) => {
                if (message.Message == "DeviceId access credentials already exist for the current user profile on the local device.") {
                    return;
                }
                Debug.LogError(message);
            });

            var options = new Options() {
                ProductId = apiKeys.epicProductId,
                SandboxId = apiKeys.epicSandboxId,
                DeploymentId = apiKeys.epicDeploymentId,
                ClientCredentials = new ClientCredentials() {
                    ClientId = apiKeys.epicClientId,
                    ClientSecret = apiKeys.epicClientSecret
                },
                TickBudgetInMilliseconds = tickBudgetInMilliseconds
            };

            EOS = PlatformInterface.Create(ref options);
            if (EOS == null) {
                throw new System.Exception("Failed to create platform");
            }

            if (checkForEpicLauncherAndRestart) {
                Result result = EOS.CheckForLauncherAndRestart();

                // If not started through epic launcher the app will be restarted and we can quit 
                if (result != Result.NoChange) {

                    // Log error if launcher check failed, but still quit to prevent hacking
                    if (result == Result.UnexpectedError) {
                        Debug.LogError("Unexpected Error while checking if app was started through epic launcher");
                    }

                    Application.Quit();
                }
            }

            // If we use the Auth interface then only login into the Connect interface after finishing the auth interface login
            // If we don't use the Auth interface we can directly login to the Connect interface
            if (authInterfaceLogin) {
                if (authInterfaceCredentialType == Epic.OnlineServices.Auth.LoginCredentialType.Developer) {
                    authInterfaceLoginCredentialId = "localhost:" + devAuthToolPort;
                    authInterfaceCredentialToken = devAuthToolCredentialName;
                }

                // Login to Auth Interface
                Epic.OnlineServices.Auth.LoginOptions loginOptions = new Epic.OnlineServices.Auth.LoginOptions() {
                    Credentials = new Epic.OnlineServices.Auth.Credentials() {
                        Type = authInterfaceCredentialType,
                        Id = authInterfaceLoginCredentialId,
                        Token = authInterfaceCredentialToken
                    },
                    ScopeFlags = Epic.OnlineServices.Auth.AuthScopeFlags.BasicProfile | Epic.OnlineServices.Auth.AuthScopeFlags.FriendsList | Epic.OnlineServices.Auth.AuthScopeFlags.Presence
                };

                EOS.GetAuthInterface().Login(ref loginOptions, null, OnAuthInterfaceLogin);
            }
            else {
                // Login to Connect Interface
                if (connectInterfaceCredentialType == Epic.OnlineServices.ExternalCredentialType.DeviceidAccessToken) {
                    Epic.OnlineServices.Connect.CreateDeviceIdOptions createDeviceIdOptions = new Epic.OnlineServices.Connect.CreateDeviceIdOptions();
                    createDeviceIdOptions.DeviceModel = deviceModel;
                    EOS.GetConnectInterface().CreateDeviceId(ref createDeviceIdOptions, null, OnCreateDeviceId);
                }
                else {
                    ConnectInterfaceLogin();
                }
            }

        }
        public static void Initialize() {
            if (Instance.initialized || Instance.isConnecting) {
                return;
            }

            Instance.InitializeImplementation();
        }

        private void OnAuthInterfaceLogin(ref Epic.OnlineServices.Auth.LoginCallbackInfo loginCallbackInfo) {
            if (loginCallbackInfo.ResultCode == Result.Success) {
                Debug.Log("Auth Interface Login succeeded");

                Utf8String accountIdString;
                Result result = loginCallbackInfo.LocalUserId.ToString(out accountIdString);
                if (Result.Success == result) {
                    Debug.Log("EOS User ID:" + accountIdString);

                    localUserAccountIdString = accountIdString;
                    localUserAccountId = loginCallbackInfo.LocalUserId;
                }

                ConnectInterfaceLogin();
            }
            else if (Epic.OnlineServices.Common.IsOperationComplete(loginCallbackInfo.ResultCode)) {
                Debug.Log("Login returned " + loginCallbackInfo.ResultCode);
            }
        }

        private void OnCreateDeviceId(ref Epic.OnlineServices.Connect.CreateDeviceIdCallbackInfo createDeviceIdCallbackInfo) {
            if (createDeviceIdCallbackInfo.ResultCode == Result.Success || createDeviceIdCallbackInfo.ResultCode == Result.DuplicateNotAllowed) {
                ConnectInterfaceLogin();
            }
            else if (Epic.OnlineServices.Common.IsOperationComplete(createDeviceIdCallbackInfo.ResultCode)) {
                Debug.Log("Device ID creation returned " + createDeviceIdCallbackInfo.ResultCode);
            }
        }

        private void ConnectInterfaceLogin() {
            var loginOptions = new Epic.OnlineServices.Connect.LoginOptions();

            if (connectInterfaceCredentialType == Epic.OnlineServices.ExternalCredentialType.Epic) {
                Epic.OnlineServices.Auth.Token? token;
                var options = new Epic.OnlineServices.Auth.CopyUserAuthTokenOptions();

                Result result = EOS.GetAuthInterface().CopyUserAuthToken(ref options, localUserAccountId, out token);

                if (result == Result.Success) {
                    connectInterfaceCredentialToken = token?.AccessToken;
                }
                else {
                    Debug.LogError("Failed to retrieve User Auth Token");
                }
            }
            else if (connectInterfaceCredentialType == Epic.OnlineServices.ExternalCredentialType.DeviceidAccessToken) {
                loginOptions.UserLoginInfo = new Epic.OnlineServices.Connect.UserLoginInfo() {
                    DisplayName = displayName,
                };
            }

            loginOptions.Credentials = new Epic.OnlineServices.Connect.Credentials() {
                Type = connectInterfaceCredentialType,
                Token = connectInterfaceCredentialToken,
            };


            EOS.GetConnectInterface().Login(ref loginOptions, null, OnConnectInterfaceLogin);
        }

        private void OnConnectInterfaceLogin(ref Epic.OnlineServices.Connect.LoginCallbackInfo loginCallbackInfo) {
            if (loginCallbackInfo.ResultCode == Result.Success) {
                Debug.Log("Connect Interface Login succeeded");

                Utf8String productIdString;
                Result result = loginCallbackInfo.LocalUserId.ToString(out productIdString);
                if (Result.Success == result) {
                    Debug.Log("EOS User Product ID:" + productIdString);

                    localUserProductIdString = productIdString;
                    localUserProductId = loginCallbackInfo.LocalUserId;
                }

                initialized = true;
                isConnecting = false;

                var authExpirationOptions = new Epic.OnlineServices.Connect.AddNotifyAuthExpirationOptions();
                authExpirationHandle = EOS.GetConnectInterface().AddNotifyAuthExpiration(ref authExpirationOptions, null, OnAuthExpiration);
            }
            else if (Epic.OnlineServices.Common.IsOperationComplete(loginCallbackInfo.ResultCode)) {
                Debug.Log("Login returned " + loginCallbackInfo.ResultCode + "\nRetrying...");
                var options = new Epic.OnlineServices.Connect.CreateUserOptions() { ContinuanceToken = loginCallbackInfo.ContinuanceToken };

                EOS.GetConnectInterface().CreateUser(ref options, null, (ref Epic.OnlineServices.Connect.CreateUserCallbackInfo cb) => {
                    if (cb.ResultCode != Result.Success) {
                        Debug.Log(cb.ResultCode);
                        return;
                    }
                    localUserProductId = cb.LocalUserId;
                    ConnectInterfaceLogin();
                });
            }
        }

        private void OnAuthExpiration(ref Epic.OnlineServices.Connect.AuthExpirationCallbackInfo authExpirationCallbackInfo) {
            Debug.Log("AuthExpiration callback");
            EOS.GetConnectInterface().RemoveNotifyAuthExpiration(authExpirationHandle);
            ConnectInterfaceLogin();
        }

        // Calling tick on a regular interval is required for callbacks to work.
        private void LateUpdate() {
            if (EOS != null) {
                platformTickTimer += Time.deltaTime;

                if (platformTickTimer >= platformTickIntervalInSeconds) {
                    platformTickTimer = 0;
                    EOS.Tick();
                }
            }
        }

        private void OnApplicationQuit() {
            if (EOS != null) {
                EOS.Release();
                EOS = null;
                PlatformInterface.Shutdown();
            }

            // Unhook the library in the editor, this makes it possible to load the library again after stopping to play
#if UNITY_EDITOR
            if (libraryPointer != IntPtr.Zero) {
                Bindings.Unhook();

                // Free until the module ref count is 0
                while (FreeLibrary(libraryPointer) != 0) {
                }

                libraryPointer = IntPtr.Zero;
            }
#endif
        }
    }
}

[System.Serializable]
public class EosApiKey {
    public string epicProductName = "MyApplication";
    public string epicProductVersion = "1.0";
    public string epicProductId = "";
    public string epicSandboxId = "";
    public string epicDeploymentId = "";
    public string epicClientId = "";
    public string epicClientSecret = "";
}