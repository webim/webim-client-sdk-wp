using Webim_Client.Common;
using Webim_Client.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;

using Webim_Client.Webim;
using WebimSDK;
using Webim_Client.DataModel;



// The Pivot Application template is documented at http://go.microsoft.com/fwlink/?LinkID=391641

namespace Webim_Client
{
    public sealed partial class PivotPage : Page, IFileOpenPickerContinuable
    {
        private CommandBar _realtimeCommandBar;
        private CommandBar _offlineCommandBar;

        private AppBarButton _startChatBarButton;
        private AppBarButton _closeChatBarButton;
        private AppBarButton _sendMessageBarButton;
        private AppBarButton _attachImageBarButton;

        private const string FirstGroupName = "FirstGroup";
        private const string SecondGroupName = "SecondGroup";

        private readonly RealtimeMessagesModel _realtimeMessagesModel = RealtimeMessagesModel.Model;

        private readonly NavigationHelper navigationHelper;
        private readonly ObservableDictionary defaultViewModel = new ObservableDictionary();
        private readonly ResourceLoader resourceLoader = ResourceLoader.GetForCurrentView("Resources");

        public static PivotPage Current { get; private set; }

        public PivotPage()
        {
            this.InitializeComponent();
            Current = this;

            this.NavigationCacheMode = NavigationCacheMode.Required;

            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
            this.navigationHelper.SaveState += this.NavigationHelper_SaveState;

            PrepareCommandBars();
            UpdateCommandBarButtonsStates();
            if (WebimController.Instance.Initialized)
            {
                Instance_RealtimeSessionInitialized();
            }
            else
            {
                WebimController.Instance.RealtimeSessionInitialized += Instance_RealtimeSessionInitialized;
            }
        }

        void Instance_RealtimeSessionInitialized()
        {
            PrepareRealtimeSessionChatActions();
            UpdateCommandBarButtonsStates();
        }

        private void PrepareCommandBars()
        {
            PrepareRealtimeChatBottomBar();
            PrepareOfflineChatBottomBar();
        }

        private void PrepareRealtimeChatBottomBar()
        {
            _realtimeCommandBar = new CommandBar();
            _sendMessageBarButton = new AppBarButton()
            {
                Icon = new SymbolIcon(Symbol.Send),
                Label = "Послать",
            };
            _sendMessageBarButton.Click += _sendMessageBarButton_Click;

            _attachImageBarButton = new AppBarButton()
            {
                Icon = new SymbolIcon(Symbol.Attach),
                Label = "Прикрепить",
            };
            _attachImageBarButton.Click += _attachImageBarButton_Click;

            _startChatBarButton = new AppBarButton()
            {
                Icon = new SymbolIcon(Symbol.Add),
                Label = "Начать чат",
            };
            _startChatBarButton.Click += _startChatBarButton_Click;

            _closeChatBarButton = new AppBarButton()
            {
                Icon = new SymbolIcon(Symbol.Cancel),
                Label = "Закончить",
            };
            _closeChatBarButton.Click += _closeChatBarButton_Click;

            _realtimeCommandBar.PrimaryCommands.Add(_sendMessageBarButton);
            _realtimeCommandBar.PrimaryCommands.Add(_attachImageBarButton);
            _realtimeCommandBar.PrimaryCommands.Add(_startChatBarButton);
            _realtimeCommandBar.PrimaryCommands.Add(_closeChatBarButton);
        }

        private void PrepareOfflineChatBottomBar()
        {
            _offlineCommandBar = new CommandBar();
            AppBarButton newOfflineChat = new AppBarButton()
            {
                Icon = new SymbolIcon(Symbol.Add),
                Label = "Новый чат",
            };
            _offlineCommandBar.PrimaryCommands.Add(newOfflineChat);
        }

        private void UpdateCommandBarButtonsStates()
        {
            if (!WebimController.Instance.Initialized)
            {
                _sendMessageBarButton.IsEnabled = false;
                _attachImageBarButton.IsEnabled = false;
                _startChatBarButton.IsEnabled = false;
                _closeChatBarButton.IsEnabled = false;
            }
            else
            {
                WMSession session = WebimController.Instance.RealtimeSession;
                bool chatClosed = IsChatClosed(session);
                _startChatBarButton.IsEnabled = chatClosed && session.HasOnlineOperators;
                _closeChatBarButton.IsEnabled = !chatClosed;
                _sendMessageBarButton.IsEnabled = !chatClosed;
                _attachImageBarButton.IsEnabled = !chatClosed;
            }
        }

        private bool IsChatClosed(WMSession session)
        {
            return session.Chat == null ||
                session.Chat.State == WMChat.WMChatState.WMChatStateUnknown ||
                session.Chat.State == WMChat.WMChatState.WMChatStateClosed ||
                session.Chat.State == WMChat.WMChatState.WMChatStateClosedByVisitor;
        }

        async void _startChatBarButton_Click(object sender, RoutedEventArgs e)
        {
            await WebimController.Instance.RealtimeSession.StartChatAsync();
        }

        async void _closeChatBarButton_Click(object sender, RoutedEventArgs e)
        {
            await WebimController.Instance.RealtimeSession.CloseChatAsync();
        }

        async void _sendMessageBarButton_Click(object sender, RoutedEventArgs e)
        {
            string message = RealtimeMessageBox.Text;
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }
            WMSession session = WebimController.Instance.RealtimeSession;
            var result = await session.SendMessageAsync(message);
            if (result.Successful)
            {
                RealtimeMessageBox.Text = "";
            }
        }

        void _attachImageBarButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");
            openPicker.FileTypeFilter.Add(".bmp");

            openPicker.PickSingleFileAndContinue();
        }

        public async void ContinueFileOpenPicker(Windows.ApplicationModel.Activation.FileOpenPickerContinuationEventArgs args)
        {
            if (args.Files.Count == 0)
            {
                return;
            }
            
            StorageFile file = args.Files[0];
            WMSession session = WebimController.Instance.RealtimeSession;
            // TODO: continue session
            try
            {
                using (var stream = await file.OpenStreamForReadAsync())
                {
                    BinaryReader reader = new BinaryReader(stream);
                    var imageData = reader.ReadBytes((int)stream.Length);
                    var isOk = await session.SendFileAsync(imageData, file.Name, file.ContentType);
                }
            }
            catch
            {
                ;
            }
        }

        #region Realtime Session

        private void PrepareRealtimeSessionChatActions()
        {
            WMSession session = WebimController.Instance.RealtimeSession;
            session.SessionDidChangeSessionStatus += session_SessionDidChangeSessionStatus;
            session.SessionDidChangeChatStatus += session_SessionDidChangeChatStatus;
            session.SessionDidReceiveError += session_SessionDidReceiveError;
            session.SessionDidReceiveFullUpdate += session_SessionDidReceiveFullUpdate;
            session.SessionDidReceiveMessage += session_SessionDidReceiveMessage;
            session.SessionDidStartChat += session_SessionDidStartChat;
            session.SessionDidUpdateOperator += session_SessionDidUpdateOperator;
        }

        void session_SessionDidUpdateOperator(WMSession session, WMOperator chatOperator)
        {

        }

        void session_SessionDidStartChat(WMSession session, WMChat chat)
        {
            MaybeReloadMessagesForSession(session);
            UpdateCommandBarButtonsStates();
        }

        void session_SessionDidReceiveMessage(WMSession session, WMMessage message)
        {
            MaybeReloadMessagesForSession(session);
            RealtimeScrollToLastestMessage(session);
        }

        void session_SessionDidReceiveFullUpdate(WMSession session)
        {
            MaybeReloadMessagesForSession(session);
            UpdateCommandBarButtonsStates();
        }

        void session_SessionDidReceiveError(WMSession session, WMBaseSession.WMSessionError errorID)
        {
            UpdateCommandBarButtonsStates();
        }

        void session_SessionDidChangeChatStatus(WMSession session)
        {
            MaybeReloadMessagesForSession(session);
            UpdateCommandBarButtonsStates();
        }

        void session_SessionDidChangeSessionStatus(WMSession session)
        {
            MaybeReloadMessagesForSession(session);
            UpdateCommandBarButtonsStates();
        }

        void MaybeReloadMessagesForSession(WMSession session)
        {
            if (session.Chat != null && session.Chat.Messages != null)
            {
                ObservableCollection<WMMessage> collection = new ObservableCollection<WMMessage>(session.Chat.Messages);
                _realtimeMessagesModel.Messages = collection;
            }
            else
            {
                _realtimeMessagesModel.Messages.Clear();
            }
        }

        void RealtimeScrollToLastestMessage(WMSession session)
        {
            if (session.Chat != null && session.Chat.Messages != null)
            {
                var selectedItemIndex = session.Chat.Messages.Count - 1;
                if (selectedItemIndex < 0)
                {
                    return;
                }
                RealtimeChatListView.SelectedIndex = selectedItemIndex;
                RealtimeChatListView.UpdateLayout();
                RealtimeChatListView.ScrollIntoView(RealtimeChatListView.SelectedItem);
            }
        }

        #endregion

        /// <summary>
        /// Gets the <see cref="NavigationHelper"/> associated with this <see cref="Page"/>.
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }

        /// <summary>
        /// Gets the view model for this <see cref="Page"/>.
        /// This can be changed to a strongly typed view model.
        /// </summary>
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }

        /// <summary>
        /// Populates the page with content passed during navigation. Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="sender">
        /// The source of the event; typically <see cref="NavigationHelper"/>.
        /// </param>
        /// <param name="e">Event data that provides both the navigation parameter passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested and
        /// a dictionary of state preserved by this page during an earlier
        /// session. The state will be null the first time a page is visited.</param>
        private async void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            // TODO: Create an appropriate data model for your problem domain to replace the sample data
            var sampleDataGroup = await SampleDataSource.GetGroupAsync("Group-1");
            this.DefaultViewModel[FirstGroupName] = _realtimeMessagesModel;
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache. Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="sender">The source of the event; typically <see cref="NavigationHelper"/>.</param>
        /// <param name="e">Event data that provides an empty dictionary to be populated with
        /// serializable state.</param>
        private void NavigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
            // TODO: Save the unique state of the page here.
        }

        /// <summary>
        /// Adds an item to the list when the app bar button is clicked.
        /// </summary>
        private void AddAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            string groupName = this.pivot.SelectedIndex == 0 ? FirstGroupName : SecondGroupName;
            var group = this.DefaultViewModel[groupName] as SampleDataGroup;
            var nextItemId = group.Items.Count + 1;
            var newItem = new SampleDataItem(
                string.Format(CultureInfo.InvariantCulture, "Group-{0}-Item-{1}", this.pivot.SelectedIndex + 1, nextItemId),
                string.Format(CultureInfo.CurrentCulture, this.resourceLoader.GetString("NewItemTitle"), nextItemId),
                string.Empty,
                string.Empty,
                this.resourceLoader.GetString("NewItemDescription"),
                string.Empty);

            group.Items.Add(newItem);

            // Scroll the new item into view.
            var container = this.pivot.ContainerFromIndex(this.pivot.SelectedIndex) as ContentControl;
            var listView = container.ContentTemplateRoot as ListView;
            listView.ScrollIntoView(newItem, ScrollIntoViewAlignment.Leading);
        }

        /// <summary>
        /// Loads the content for the second pivot item when it is scrolled into view.
        /// </summary>
        private async void SecondPivot_Loaded(object sender, RoutedEventArgs e)
        {
            var sampleDataGroup = await SampleDataSource.GetGroupAsync("Group-2");
            this.DefaultViewModel[SecondGroupName] = sampleDataGroup;
        }

        private void pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pivot.SelectedIndex == 0)
            {
                BottomAppBar = _realtimeCommandBar;
            }
            else if (pivot.SelectedIndex == 1)
            {
                BottomAppBar = _offlineCommandBar;
            }
        }

        #region NavigationHelper registration

        /// <summary>
        /// The methods provided in this section are simply used to allow
        /// NavigationHelper to respond to the page's navigation methods.
        /// <para>
        /// Page specific logic should be placed in event handlers for the  
        /// <see cref="NavigationHelper.LoadState"/>
        /// and <see cref="NavigationHelper.SaveState"/>.
        /// The navigation parameter is available in the LoadState method 
        /// in addition to page state preserved during an earlier session.
        /// </para>
        /// </summary>
        /// <param name="e">Provides data for navigation methods and event
        /// handlers that cannot cancel the navigation request.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedFrom(e);
        }

        #endregion

        private void RealtimeChatListView_Loaded(object sender, RoutedEventArgs e)
        {
            ListView listView = sender as ListView;
            listView.DataContext = _realtimeMessagesModel;
        }

        private void RealtimeMessageBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void RealtimeMessageBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!WebimController.Instance.Initialized)
            {
                return;
            }
            WMSession session = WebimController.Instance.RealtimeSession;
            session.SetComposingMessage(false, string.Empty);
        }

        private async void RealtimeMessageBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!WebimController.Instance.Initialized)
            {
                return;
            }
            WMSession session = WebimController.Instance.RealtimeSession;
            session.SetComposingMessage(true, string.Empty);
        }

        private async void RealtimeChatListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            WMMessage message = e.ClickedItem as WMMessage;
            if (message == null || string.IsNullOrEmpty(message.SenderUID))
            {
                return;
            }
            if (!(message.Kind == WMMessage.WMMessageKind.WMMessageKindOperator || message.Kind == WMMessage.WMMessageKind.WMMessageKindFileFromOperator)) {
                return;
            }
            RatingPage ratingView = new RatingPage();
            await ratingView.ShowAsync();
            if (ratingView.Result.Canceled)
            {
                return;
            }
            await WebimController.Instance.RealtimeSession.RateOperatorWithRateAsync(message.SenderUID, ratingView.Result.rate);
        }
    }
}
