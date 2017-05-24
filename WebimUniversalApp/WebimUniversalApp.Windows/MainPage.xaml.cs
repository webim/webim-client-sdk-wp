using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using System.Diagnostics;

using WebimSDK;
using WebimUniversalApp.DataStructures;
using Windows.System.Profile;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Networking.Connectivity;
using Windows.UI.Core;
using Windows.System.Threading;
using Windows.ApplicationModel.Core;

namespace WebimUniversalApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public WMSession RealtimeSession { get; set; }
        public WMOfflineSession OfflineSession { get; set; }

        public ObservableCollection<Chat> ChatsCollection = new ObservableCollection<Chat>();
        public ObservableCollection<Message> MessagesCollection = new ObservableCollection<Message>();
        private WMChat _activeChat;
        private DispatcherTimer _updateTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(10) };
        private NetworkStatusChangedEventHandler networkStatusCallback;
        private bool registeredNetworkStatusNotif;
        private CoreDispatcher _cd = Window.Current.CoreWindow.Dispatcher;

        public MainPage()
        {
            this.InitializeComponent();
            ChatsListView.DataContext = ChatsCollection;
            MessagesListView.DataContext = MessagesCollection;
        }

        private async Task UI_LoadMessagesForActiveChatAsync()
        {
            MessagesCollection.Clear();

            if (_activeChat == null)
                return;

            foreach (var item in _activeChat.Messages)
            {
                MessagesCollection.Add(new Message(item, OfflineSession));
            }
            if (_activeChat.HasUnreadMessages)
            {
                await OfflineSession.MarkChatAsReadAsync(_activeChat);
                try
                {
                    Chat chat = ChatsCollection.First(s => s.WebimChat == _activeChat);
                    chat.WebimChat = _activeChat; // Force loading fields
                }
                catch
                {

                }
            }
        }

        private async Task WebimCheckForUpdatesAsync()
        {
            WMAPIResponse<WMHistoryChanges> historyResponse = await OfflineSession.GetHistoryAsync();
            if (!historyResponse.Successful)
            {
                if (historyResponse.WebimError == WMBaseSession.WMSessionError.WMSessionErrorNetworkError)
                {
                    Debug.WriteLine("Getting History failed: network error");
                }
                else if (historyResponse.WebimError == WMBaseSession.WMSessionError.WMSessionErrorNotConfigured)
                {
                    Debug.WriteLine("Getting history failed: no history before first chat");
                }
                return;
            }

            WMHistoryChanges changes = historyResponse.ResponseData;
            foreach (var item in changes.NewChats)
            {
                ChatsCollection.Insert(0, new Chat(item));
            }

            if (_activeChat != null && changes.ModifiedChats.Contains(_activeChat))
            {
                await UI_LoadMessagesForActiveChatAsync();
            }
            foreach (WMChat webimChat in changes.ModifiedChats)
            {
                try
                {
                    Chat chat = ChatsCollection.First(s => s.WebimChat == webimChat);
                    chat.WebimChat = webimChat;
                }
                catch { }
            }
        }

        private async Task SendOfflineMessagesAsync()
        {
            var unsentResponse = await OfflineSession.SendUnsentRequestsAsync();
            if (unsentResponse.Successful)
            {
                WMSyncChanges changes = unsentResponse.ResponseData;
                Debug.WriteLine("Uploaded " + changes.SentChats.Count + " chats");
                Debug.WriteLine("Uploaded " + changes.SentMessages.Count + " messages");
            }
            await UI_LoadMessagesForActiveChatAsync();
        }

        private async Task<string> GetHardwareId()
        {
            string filename = "installationId";
            StorageFolder folder = ApplicationData.Current.LocalFolder;
            bool shouldCreateFile = false;

            try
            {
                StorageFile file = await folder.GetFileAsync(filename);
            }
            catch (FileNotFoundException)
            {
                shouldCreateFile = true;
            }

            if (shouldCreateFile)
            {
                StorageFile file = await folder.CreateFileAsync(filename);
                string installationId = Guid.NewGuid().ToString();
                using (Stream fileStream = await file.OpenStreamForWriteAsync())
                {
                    using (var streamWriter = new StreamWriter(fileStream))
                    {
                        streamWriter.Write(installationId.ToString());
                    }
                }
                return installationId;
            }
            else
            {
                StorageFile file = await folder.GetFileAsync(filename);
                using (var fileStream = await file.OpenStreamForReadAsync())
                {
                    using (var streamReader = new StreamReader(fileStream))
                    {
                        return await streamReader.ReadToEndAsync();
                    }
                }
            }
        }

        private void SubscribeToNetworkStatusChange()
        {
            try
            {
                networkStatusCallback = new NetworkStatusChangedEventHandler(OnNetworkStatusChangeAsync);
                if (!registeredNetworkStatusNotif)
                {
                    NetworkInformation.NetworkStatusChanged += networkStatusCallback;
                    registeredNetworkStatusNotif = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unexpected exception occured: " + ex.ToString());
            }
        }

        private async void OnNetworkStatusChangeAsync(object sender)
        {
            try
            {
                // get the ConnectionProfile that is currently used to connect to the Internet                
                ConnectionProfile InternetConnectionProfile = NetworkInformation.GetInternetConnectionProfile();
                await _cd.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    if (InternetConnectionProfile == null)
                    {
                        Debug.WriteLine("Lost Internet connection");
                    }
                    else
                    {
                        Debug.WriteLine("Connection established");
                        await SendOfflineMessagesAsync();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unexpected exception occured: " + ex.ToString());
            }
        }


        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            string hardwareID = await GetHardwareId();

            OfflineSession = new WMOfflineSession("webimru069", "winphone", hardwareID, "winphone");
#if (false)
            await OfflineSession.ClearCachedUserDataAsync();
#endif
            await OfflineSession.Initialize();

            foreach (var item in OfflineSession.ChatsList())
            {
                ChatsCollection.Insert(0, new Chat(item));
            }

            SubscribeToNetworkStatusChange();

            await SendOfflineMessagesAsync();
            await WebimCheckForUpdatesAsync();

            _updateTimer.Tick += _updateTimer_Tick;
            _updateTimer.Start();

            WMVisitorExt visitorFields = new WMVisitorExt()
            {
                Name = "Eugeny",
                Email = "support@webim.ru",
                Phone = "+7 812 3855337",
                CRC = "cdc2c8b0542897dd311fe85754479860",
            };

            RealtimeSession = new WMSession("webimru069", "winphone", visitorFields);
            RealtimeSession.SessionDidReceiveError += RealtimeSession_SessionDidReceiveError;
            RealtimeSession.SessionDidChangeChatStatus += RealtimeSession_SessionDidChangeChatStatus;
            RealtimeSession.SessionDidChangeSessionStatus += RealtimeSession_SessionDidChangeSessionStatus;
            RealtimeSession.SessionDidReceiveMessage += RealtimeSession_SessionDidReceiveMessage;
            RealtimeSession.SessionDidStartChat += RealtimeSession_SessionDidStartChat;
            RealtimeSession.SessionDidUpdateOperator += RealtimeSession_SessionDidUpdateOperator;
            RealtimeSession.SessionDidReceiveFullUpdate += RealtimeSession_SessionDidReceiveFullUpdate;

            await RealtimeSession.StartSessionAsync();
        }

        void RealtimeSession_SessionDidReceiveFullUpdate(WMSession session)
        {
            Debug.WriteLine("RealtimeSession_SessionDidReceiveFullUpdate");
        }

        void RealtimeSession_SessionDidUpdateOperator(WMSession session, WMOperator chatOperator)
        {
            Debug.WriteLine("RealtimeSession_SessionDidUpdateOperator");
        }

        void RealtimeSession_SessionDidStartChat(WMSession session, WMChat chat)
        {
            Debug.WriteLine("RealtimeSession_SessionDidStartChat");
        }

        void RealtimeSession_SessionDidReceiveMessage(WMSession session, WMMessage message)
        {
            Debug.WriteLine("RealtimeSession_SessionDidReceiveMessage");
        }

        void RealtimeSession_SessionDidChangeSessionStatus(WMSession session)
        {
            Debug.WriteLine("RealtimeSession_SessionDidChangeSessionStatus");
        }

        void RealtimeSession_SessionDidChangeChatStatus(WMSession session)
        {
            Debug.WriteLine("RealtimeSession_SessionDidChangeChatStatus");
        }

        void RealtimeSession_SessionDidReceiveError(WMSession session, WMBaseSession.WMSessionError errorID)
        {
            Debug.WriteLine("RealtimeSession_SessionDidReceiveError");
            Debug.WriteLine(errorID);
        }

        private async void _updateTimer_Tick(object sender, object e)
        {
            await WebimCheckForUpdatesAsync();
        }

        private void StartNewChatButton_Click(object sender, RoutedEventArgs e)
        {
            ChatsListView.SelectedItem = null;
            MessageTextBox.Focus(FocusState.Keyboard);
        }

        private async void ChatsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MessagesCollection.Clear();

            Chat activeChat = e.AddedItems != null && e.AddedItems.Count > 0 ? (Chat)e.AddedItems.First() : null;
            _activeChat = activeChat == null ? null : activeChat.WebimChat;
            await UI_LoadMessagesForActiveChatAsync();
        }

        private void MessagesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private async Task SendMessageOnRealtimeChat()
        {
            var response = await RealtimeSession.SendMessageAsync(MessageTextBox.Text);
        }

        private async void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageTextBox.Text.Length == 0)
            {
                return;
            }
            if (RealtimeSwitch.IsOn)
            {
                await SendMessageOnRealtimeChat();
                return;
            }
            // _ActiveChat is a trigger for create chat & message and create message inside of existed chat
            WMAPIResponse<WMMessage> response = await OfflineSession.SendMessageAsync(MessageTextBox.Text, _activeChat, null, null);

            WMMessage message = response.ResponseData;

            if (_activeChat == null)
            {
                WMChat newChat = OfflineSession.ChatForMessage(message);
                if (newChat.Status == WMChat.WMChatStatus.NotSent)
                {
                    Debug.WriteLine("Created offline chat, don't forget to upload it when possible");
                }
                Chat uiChat = new Chat(newChat);
                ChatsCollection.Insert(0, uiChat);

                ChatsListView.SelectedItem = uiChat;
            }
            else
            {
                if (message.Status == WMMessage.WMMessageStatus.NotSent)
                {
                    Debug.WriteLine("Created offline message");
                }
                var uiMessage = new Message(message, OfflineSession);
                MessagesCollection.Add(uiMessage);
                MessagesListView.ScrollIntoView(uiMessage);
            }
            // Reset content of message box on any seccessful response
            MessageTextBox.Text = string.Empty;
        }

        private void MessageTextBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                SendMessageButton_Click(sender, e);
            }
        }

        private async void ChatCellButton_Click(object sender, RoutedEventArgs e)
        {
            Chat chat = (Chat)((Button)sender).DataContext;
            if (chat != null)
            {
                WMChat webimChat = chat.WebimChat;
                var retVal = await OfflineSession.DeleteChatAsync(webimChat);
                if (retVal.Successful)
                {
                    ChatsCollection.Remove(chat);
                }
            }
        }

        private async void AttachImageButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");

            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    using (var stream = await file.OpenStreamForReadAsync())
                    {
                        BinaryReader reader = new BinaryReader(stream);
                        var imageData = reader.ReadBytes((int)stream.Length);
                        var response = await OfflineSession.SendImageAsync(imageData, file.Name, WMBaseSession.WMChatAttachmentImageType.WMChatAttachmenWMChatAttachmentImageJPEG, _activeChat, null, null);
                        if (response.Successful)
                        {
                            WMMessage message = response.ResponseData;

                            if (_activeChat == null)
                            {
                                WMChat newChat = OfflineSession.ChatForMessage(message);
                                Chat uiChat = new Chat(newChat);
                                ChatsCollection.Insert(0, uiChat);

                                ChatsListView.SelectedItem = uiChat;
                            }
                            else
                            {
                                var uiMessage = new Message(message, OfflineSession);
                                MessagesCollection.Add(uiMessage);
                                MessagesListView.ScrollIntoView(uiMessage);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    ;
                }
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await WebimCheckForUpdatesAsync();
        }

        private async void Upload_Click(object sender, RoutedEventArgs e)
        {
            await SendOfflineMessagesAsync();
        }

        private void RealtimeSwitch_Toggled(object sender, RoutedEventArgs e)
        {

        }

        private async void StartChat_Click(object sender, RoutedEventArgs e)
        {
            await RealtimeSession.StartChatAsync();
        }

        private async void EndChat_Click(object sender, RoutedEventArgs e)
        {
            await RealtimeSession.CloseChatAsync();
        }
    }
}
