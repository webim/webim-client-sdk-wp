using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.Networking.PushNotifications;
using Windows.Storage;

using WebimSDK;

namespace Webim_Client.Webim
{
    public class WebimController
    {
        #region Singleton

        private static volatile WebimController instance;
        private static object syncRoot = new Object();

        private WebimController() { }

        public static WebimController Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                            instance = new WebimController();
                    }
                }

                return instance;
            }
        }

        #endregion

        private const string WebimAccount = "webimru069";
        private const string WebimLocation = "winphone-reference";

        private WMSession _realtimeSession;
        public WMSession RealtimeSession
        {
            get
            {
                if (!Initialized)
                {
                    throw new Exception("Not initialized");
                }
                return _realtimeSession;
            }
        }

        private WMOfflineSession _offlineSession;
        public WMOfflineSession OfflineSession
        {
            get
            {
                if (!Initialized)
                {
                    throw new Exception("Not initialized");
                }
                return _offlineSession;
            }
        }

        public delegate void RealtimeSessionInitializedHandler();
        public event RealtimeSessionInitializedHandler RealtimeSessionInitialized;

        public delegate void OfflineSessionInitializedHandler();
        public event OfflineSessionInitializedHandler OfflineSessionInitialized;

        private PushNotificationChannel _pushNotificationChannel;

        public bool Initialized { get; private set; }

        public async Task InitializeAsync()
        {
            const string SettingsPushTokenKey = "PushToken";
            string pushToken = (string)ApplicationData.Current.LocalSettings.Values[SettingsPushTokenKey];

            _realtimeSession = new WMSession(WebimAccount, WebimLocation, null);
            await _realtimeSession.StartSessionAsync();

            _offlineSession = new WMOfflineSession(WebimAccount, WebimLocation, pushToken, null);
            await _offlineSession.Initialize();

            Initialized = true;
            if (RealtimeSessionInitialized != null)
            {
                RealtimeSessionInitialized();
            }
            if (OfflineSessionInitialized != null)
            {
                OfflineSessionInitialized();
            }

            try
            {
                _pushNotificationChannel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
                if (_pushNotificationChannel != null && !String.IsNullOrEmpty(_pushNotificationChannel.Uri))
                {
                    if (string.IsNullOrEmpty(pushToken) || !pushToken.Equals(_pushNotificationChannel.Uri))
                    {
                        await _realtimeSession.SetDeviceTokenAsync(_pushNotificationChannel.Uri);
                        ApplicationData.Current.LocalSettings.Values[SettingsPushTokenKey] = _pushNotificationChannel.Uri;
                    }
                }
            } catch (Exception) {}
        }
    }
}
