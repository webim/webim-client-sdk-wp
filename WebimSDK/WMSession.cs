using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Windows.Data.Json;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.System.Threading;
using Windows.Foundation;

namespace WebimSDK
{
    using WebimSDK.Extensions;

    public enum WMSessionConnectionStatus
    {
        WMSessionConnectionStatusUnknown,
        WMSessionConnectionStatusOnline,
        WMSessionConnectionStatusOffline,
    }

    public enum WMSessionState
    {
        WMSessionStateUnknown,
        WMSessionStateIdle,
        WMSessionStateIdleAfterChat,
        WMSessionStateChat,
        WMSessionStateOfflineMessage,
    }

    public class WMSession : WMBaseSession
    {
        #region Delegates and Events
        public delegate void SessionDidReceiveFullUpdateHandler(WMSession session);
        public delegate void SessionDidStartChatHandler(WMSession session, WMChat chat);
        public delegate void SessionDidChangeChatStatusHandler(WMSession session);
        public delegate void SessionDidChangeSessionStatusHandler(WMSession session);
        public delegate void SessionDidUpdateOperatorHandler(WMSession session, WMOperator chatOperator);
        public delegate void SessionDidReceiveMessageHandler(WMSession session, WMMessage message);
        public delegate void SessionDidReceiveErrorHandler(WMSession session, WMSessionError errorID);
        public delegate void SessionDidChangeOperatorTypingHandler(WMSession session, bool typing);

        public event SessionDidReceiveFullUpdateHandler SessionDidReceiveFullUpdate;
        public event SessionDidStartChatHandler SessionDidStartChat;
        public event SessionDidChangeChatStatusHandler SessionDidChangeChatStatus;
        public event SessionDidChangeSessionStatusHandler SessionDidChangeSessionStatus;
        public event SessionDidUpdateOperatorHandler SessionDidUpdateOperator;
        public event SessionDidReceiveMessageHandler SessionDidReceiveMessage;
        public event SessionDidReceiveErrorHandler SessionDidReceiveError;
        public event SessionDidChangeOperatorTypingHandler SessionDidChangeOperatorTyping;
        #endregion

        public bool HasOnlineOperators { get; set; }
        public WMSessionState State { get; set; }
        public WMChat Chat { get; set; }
        private ThreadPoolTimer ComposingTimer { get; set; }
        public bool UseDispatcher { get; set; }

        private RestClient _RestClient;
        private SharedStorage _Storage = SharedStorage.Instance;
        private bool isGettingDelta; // TODO: handle its state, if will be used
        private string _visitorFields = "{}";

        TimeSpan DeltaRestartOnNetworkErrorTimeSpan = TimeSpan.FromSeconds(10);

        public WMSession(string account, string location, WMVisitorExt visitorExt)
            : base(account, location)
        {
            _RestClient = new RestClient(Host());
            isGettingDelta = false;
            UseDispatcher = true;
            if (visitorExt != null)
            {
                _visitorFields = visitorExt.JsonEncoded();
            }
        }

        public WMSession(Uri hostUri, string location, WMVisitorExt visitorExt)
            : base(hostUri, location)
        {
            _RestClient = new RestClient(Host());
            isGettingDelta = false;
            UseDispatcher = true;
            if (visitorExt != null)
            {
                _visitorFields = visitorExt.JsonEncoded();
            }
        }

        #region Delta

        private async Task Initialize()
        {
            var accountName = AccountName ?? (HostUri != null ? HostUri.ToString() : null);
            await _Storage.InitializeForAccount(accountName);
            if (!StringExtensions.ExistsAndEquals(_Storage.Location, Location))
            {
                _Storage.Location = Location;
            }
            if (!StringExtensions.ExistsAndEquals(_Storage.VisitorFields, _visitorFields))
            {
                _Storage.VisitorFields = _visitorFields;
            }
            await _Storage.Flush();
        }

        public async Task<bool> StartSessionAsync()
        {
            // there are two possible states: client may already has page-id or not
            // If it already has page-id, first run delta without init, else
            // init session call and deltas after it

            await Initialize();


            if (string.IsNullOrEmpty(_Storage.PageID))
            {
                // run init delta
                await GetInitialDeltaAsync();
            }
            else
            {
                // Run deltas cycle
                StartGettingDeltas();
            }
            return true;
        }

        private void StartGettingDeltas()
        {
            // Cancel any previously sheduled requests
            _RestClient.CancelDeltaRequest();

            // Prepare new task for delta
            Task task = StartGettingDeltasAsync();
        }

        private async Task GetInitialDeltaAsync()
        {
            try
            {
                _Storage.Revision = 0;
                using (var initResponse = await _RestClient.StartSessionAsync(_Storage))
                {
                    if (ProcessDeltaRequestResponseStatus(initResponse))
                    {
                        await ProcessRealtimeDeltaResponseAsync(initResponse);
                        StartGettingDeltas();
                    }
                }
            }
            catch (HttpRequestException e)
            {
                HandleDeltaRequestHttpError(e);
            }
            catch (Exception)
            {
                HandleCommonResponseException();
            }
        }

        private async Task StartGettingDeltasAsync()
        {
            try
            {
                using (var deltaResponse = await _RestClient.GetDeltaAsync(true, _Storage))
                {
                    if (ProcessDeltaRequestResponseStatus(deltaResponse))
                    {
                        await ProcessRealtimeDeltaResponseAsync(deltaResponse);
                        StartGettingDeltas();
                    }
                }
            }
            catch (HttpRequestException e)
            {
                HandleDeltaRequestHttpError(e);
            }
            catch (Exception)
            {
                HandleCommonResponseException();
            }
        }

        // throws HttpRequestException
        private void HandleDeltaResponseCommon(HttpResponseMessage response)
        {
            if (response == null)
            {
                RunOnUIThread(() =>
                {
                    if (SessionDidReceiveError != null)
                    {
                        SessionDidReceiveError(this, WMSessionError.WMSessionErrorNotConfigured);
                    }
                });
            }

            ProcessDeltaRequestResponseStatus(response);
        }

        private void HandleCommonResponseException()
        {
            CancelDeltaAndRestartAfterInterval(DeltaRestartOnNetworkErrorTimeSpan);
            RunOnUIThread(() =>
            {
                if (SessionDidReceiveError != null)
                {
                    SessionDidReceiveError(this, WMSessionError.WMSessionErrorUnknown);
                }
            });
        }

        private void HandleDeltaRequestHttpError(HttpRequestException e)
        {
            CancelDeltaAndRestartAfterInterval(DeltaRestartOnNetworkErrorTimeSpan);

            RunOnUIThread(() =>
            {
                if (SessionDidReceiveError != null)
                {
                    SessionDidReceiveError(this, WMSessionError.WMSessionErrorNetworkError);
                }
            });
        }

        private void CancelDeltaAndRestartAfterInterval(TimeSpan timeSpan)
        {
            _RestClient.CancelDeltaRequest();
            isGettingDelta = false;

            ThreadPoolTimer timer = ThreadPoolTimer.CreateTimer((handler) =>
            {
                if (!isGettingDelta)
                {
                    isGettingDelta = true;
                    StartGettingDeltas();
                }
            }, timeSpan);
        }

        #endregion

        #region Delta Full Update

        #region Delta Processors

        internal void ProcessRealtimeFullUpdate(JsonValue deltaValue)
        {
            JsonObject deltaObject = deltaValue.GetObject();

            IJsonValue value;
            if (deltaObject.TryGetValue("pageId", out value) && value.ValueType != JsonValueType.Null)
            {
                _Storage.PageID = value.GetString();
            }
            if (deltaObject.TryGetValue("visitSessionId", out value) && value.ValueType != JsonValueType.Null)
            {
                _Storage.VisitSessionID = value.GetString();
            }
            if (deltaObject.TryGetValue("visitor", out value) && value.ValueType != JsonValueType.Null)
            {
                JsonObject visitorJsonObject = value.GetObject();
                IJsonValue visitorIDValue;
                if (visitorJsonObject.TryGetValue("id", out visitorIDValue))
                {
                    string newVisitorID = visitorIDValue.GetString();

                    _Storage.VisitorID = newVisitorID;
                    _Storage.Visitor = value.Stringify();

                    // maybe clear storage file and chat list as for offline
                }
            }
            if (deltaObject.TryGetValue("onlineOperators", out value) && value.ValueType != JsonValueType.Null)
            {
                if (value.ValueType == JsonValueType.Boolean)
                {
                    HasOnlineOperators = value.GetBoolean();
                }
            }
            if (deltaObject.TryGetValue("state", out value))
            {
                RealtimeUpdateSessionStateWithObject((JsonValue)value);
                if (SessionDidChangeSessionStatus != null)
                {
                    SessionDidChangeSessionStatus(this);
                }
            }
            if (deltaObject.TryGetValue("chat", out value))
            {
                RealtimeUpdateChatWithObject((JsonValue)value);
                if (SessionDidStartChat != null)
                {
                    SessionDidStartChat(this, Chat);
                }
            }

            if (SessionDidReceiveFullUpdate != null)
            {
                SessionDidReceiveFullUpdate(this);
            }
        }

        internal void RealtimeUpdateSessionStateWithObject(JsonValue stateObject)
        {
            if (stateObject.ValueType == JsonValueType.String)
            {
                State = SessionStateFromString(stateObject.GetString());
            }
            else
            {
                State = WMSessionState.WMSessionStateUnknown;
            }
        }

        internal WMSessionState SessionStateFromString(string stateString)
        {
            Dictionary<string, WMSessionState> map = new Dictionary<string, WMSessionState>()
            {
                {"idle", WMSessionState.WMSessionStateIdle},
                {"idle-after-chat", WMSessionState.WMSessionStateIdleAfterChat },
                {"chat", WMSessionState.WMSessionStateChat},
                {"offline-message", WMSessionState.WMSessionStateOfflineMessage},
            };
            WMSessionState state = WMSessionState.WMSessionStateUnknown;
            if (map.TryGetValue(stateString, out state))
            {
                return state;
            }
            return WMSessionState.WMSessionStateUnknown;
        }

        internal void RealtimeUpdateChatWithObject(JsonValue chatObject)
        {
            if (chatObject.ValueType == JsonValueType.Null)
            {
                Chat = null;
            }
            else
            {
                Chat = new WMChat(chatObject, this);
            }
        }

        internal void ProcessRealtimeDeltaList(JsonValue deltaValue)
        {
            if (deltaValue.ValueType != JsonValueType.Array)
            {
                return;
            }
            foreach (var item in deltaValue.GetArray())
            {
                JsonObject itemObject = item.GetObject();
                string deltaItemType = null;
                string operationAction = null;
                IJsonValue data = null;

                IJsonValue value;
                if (itemObject.TryGetValue("objectType", out value))
                {
                    deltaItemType = value.GetString();
                }
                if (itemObject.TryGetValue("event", out value))
                {
                    operationAction = value.GetString();
                }
                if (itemObject.TryGetValue("data", out value))
                {
                    data = value;
                }

                ProcessDeltaItem(deltaItemType, operationAction, (JsonValue)data);
            }
        }

        #endregion


        #region Delta Item Processor

        const string AddAction = "add";
        const string UpdateAction = "upd";
        const string DeleteAction = "del";

        internal void RunOnUIThread(DispatchedHandler action)
        {
            if (UseDispatcher)
            {
                var asyncAction = CoreApplication.GetCurrentView().CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, new DispatchedHandler(action));
            }
            else
            {
                action();
            }
        }

        internal void DeltaProcessVisitSessionState(string action, JsonValue dataValue)
        {
            if (AddAction == action)
            {
                RealtimeUpdateSessionStateWithObject(dataValue);

                if (SessionDidChangeSessionStatus != null)
                {
                    RunOnUIThread(() => SessionDidChangeSessionStatus(this));
                }
            }
        }

        internal void DeltaProcessChat(string action, JsonValue dataValue)
        {
            if (UpdateAction == action)
            {
                RealtimeUpdateChatWithObject(dataValue);
                if (SessionDidStartChat != null)
                {
                    RunOnUIThread(() => SessionDidStartChat(this, Chat));
                }
            }
        }

        internal void DeltaProcessChatMessage(string action, JsonValue dataValue)
        {
            if (AddAction == action)
            {
                WMMessage message = new WMMessage(dataValue, this);
                Chat.Messages.Add(message);
                if (SessionDidReceiveMessage != null)
                {
                    RunOnUIThread(() => SessionDidReceiveMessage(this, message));
                }
            }
        }

        internal void DeltaProcessChatState(string action, JsonValue dataValue)
        {
            if (UpdateAction == action)
            {
                Chat.State = Chat.StateFromString(dataValue.GetString());
                if (SessionDidChangeChatStatus != null)
                {
                    RunOnUIThread(() => SessionDidChangeChatStatus(this));
                }
            }
        }

        internal void DeltaProcessChatOperator(string action, JsonValue dataValue)
        {
            if (UpdateAction == action)
            {
                WMOperator chatOperator = Chat.ChatOperator;
                if (dataValue.ValueType == JsonValueType.Null)
                {
                    Chat.ChatOperator = null;
                    chatOperator = null;
                }
                else
                {
                    if (Chat.ChatOperator == null)
                    {
                        Chat.ChatOperator = new WMOperator(dataValue);
                    }
                    else
                    {
                        Chat.ChatOperator.Initialize(dataValue);
                    }
                    chatOperator = Chat.ChatOperator;
                }
                if (SessionDidUpdateOperator != null)
                {
                    RunOnUIThread(() => SessionDidUpdateOperator(this, Chat.ChatOperator));
                }
            }
        }

        internal void DeltaProcessChatReadByVisitor(string action, JsonValue dataValue)
        {
            if (UpdateAction == action)
            {
                Chat.HasUnreadMessages = !dataValue.GetBoolean();
                if (SessionDidChangeChatStatus != null)
                {
#if (false)
                    RunOnUIThread(() => SessionDidChangeChatStatus(this));
#endif
                }
            }
        }

        internal void DeltaProcessChatOperatorTyping(string action, JsonValue dataValue)
        {
            if (UpdateAction == action)
            {
                Chat.OperatorTyping = dataValue.GetBoolean();
                if (SessionDidChangeOperatorTyping != null)
                {
                    RunOnUIThread(() => SessionDidChangeOperatorTyping(this, Chat.OperatorTyping));
                }
            }
        }

        #endregion

        internal void ProcessDeltaItem(string objectTypeString, string eventString, JsonValue dataValue)
        {
            Dictionary<string, Action> map = new Dictionary<string, Action>
            {
                {"VISIT_SESSION_STATE", () => DeltaProcessVisitSessionState(eventString, dataValue) },
                {"CHAT", () => DeltaProcessChat(eventString, dataValue) },
                {"CHAT_MESSAGE", () => DeltaProcessChatMessage(eventString, dataValue) },
                {"CHAT_STATE", () => DeltaProcessChatState(eventString, dataValue) },
                {"CHAT_OPERATOR", () => DeltaProcessChatOperator(eventString, dataValue) },
                {"CHAT_READ_BY_VISITOR", () => DeltaProcessChatReadByVisitor(eventString, dataValue) },
                {"CHAT_OPERATOR_TYPING", () => DeltaProcessChatOperatorTyping(eventString, dataValue) },
            };
            Action processor;
            if (map.TryGetValue(objectTypeString, out processor))
            {
                processor();
            }
            else
            {
                // Warning: unrecognized delta item type
            }
        }

        internal async Task ProcessRealtimeDeltaResponseAsync(HttpResponseMessage response)
        {
            string responseString = await response.Content.ReadAsStringAsync();
            JsonValue responseValue;
            if (!JsonValue.TryParse(responseString, out responseValue))
            {
                throw new Exception("Unable to parse full update response: " + responseString);
            }
            if (responseValue.ValueType == JsonValueType.Null)
            {
                return;
            }

            JsonObject responseObject = responseValue.GetObject();
            if (responseObject.Keys.Count == 0)
            {
                return;
            }

            // Note: error processing must be done outside

            // to be sure we process and apply stuff on the "save" manner - in the ui thread,
            // by this it makes it proper to call events and safier write to file
            DispatchedHandler handler = delegate()
            {
                IJsonValue value;
                if (responseObject.TryGetValue("revision", out value) && value.ValueType != JsonValueType.Null)
                {
                    _Storage.Revision = value.GetNumber();
                }

                if (responseObject.TryGetValue("fullUpdate", out value) && value.ValueType == JsonValueType.Object)
                {
                    ProcessRealtimeFullUpdate((JsonValue)value);
                }
                else if (responseObject.TryGetValue("deltaList", out value) && value.ValueType == JsonValueType.Array)
                {
                    ProcessRealtimeDeltaList((JsonValue)value);
                }
                else
                {
                    throw new Exception("Neither fullUpdate or deltaList present in delta response");
                }
            };

            if (UseDispatcher)
            {
                await CoreApplication.GetCurrentView().CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, new DispatchedHandler(handler));
            }
            else
            {
                handler();
            }
        }

        internal bool RealtimeIsSuccessResponse(HttpResponseMessage response)
        {
            string responseString = response.Content.ReadAsStringAsync().Result;
            if (string.IsNullOrEmpty(responseString))
            {
                return true;
            }

            JsonValue jsonValue;
            if (JsonValue.TryParse(responseString, out jsonValue))
            {
                JsonObject jsonObject = jsonValue.GetObject();
                IJsonValue value;
                if (jsonObject.TryGetValue("result", out value) && value.ValueType == JsonValueType.String)
                {
                    if (value.GetString() == "ok")
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal WMBaseSession.WMSessionError RealtimeErrorInResponse(HttpResponseMessage response)
        {
            WMBaseSession.WMSessionError error = WMBaseSession.WMSessionError.WMSessionErrorUnknown;

            string responseString = response.Content.ReadAsStringAsync().Result;
            if (string.IsNullOrEmpty(responseString))
            {
                return error;
            }

            JsonValue jsonValue;
            if (JsonValue.TryParse(responseString, out jsonValue))
            {
                JsonObject jsonObject = jsonValue.GetObject();
                IJsonValue value;
                if (jsonObject.TryGetValue("error", out value))
                {
                    if (value.ValueType == JsonValueType.String)
                    {
                        return WMBaseSession.ErrorFromString(value.GetString());
                    }
                    else
                    {
                        throw new Exception("Unexpected error type for error");
                    }
                }
            }
            return error;
        }

        #endregion

        #region APIs

        public async Task<bool> RefreshSessionAsync()
        {
            return true;
        }

        public async Task<WMAPIResponse<string>> StartChatAsync(string clientSideId = null)
        {
            if (string.IsNullOrEmpty(clientSideId))
            {
                clientSideId = UIDGenerator.NextPositive();
            }
            return await ClientSideIdResponseHandlerAsync(clientSideId, async () =>
            {
                return await _RestClient.RealtimeStartChatAsync(clientSideId, _Storage);
            });
        }

        public async Task<bool> CloseChatAsync()
        {
            return await SimpleResponseExectionHandler(async () =>
            {
                return await _RestClient.RealtimeCloseChatAsync(_Storage);
            });
        }

        public async Task<bool> MarkChatAsReadAsync()
        {
            return await SimpleResponseExectionHandler(async () =>
            {
                return await _RestClient.RealtimeMarkChatAsReadAsync(_Storage);
            });
        }

        public async Task<WMAPIResponse<string>> SendMessageAsync(string text, string clientSideId = null)
        {
            if (string.IsNullOrEmpty(clientSideId))
            {
                clientSideId = UIDGenerator.NextPositive();
            }
            return await ClientSideIdResponseHandlerAsync(clientSideId, async () =>
            {
                return await _RestClient.RealtimeChatSendMessageAsync(text, clientSideId, _Storage);
            });
        }

        public async Task<WMAPIResponse<string>> SendImageAsync(byte[] imageData, WMChatAttachmentImageType type, string clientSideId = null)
        {
            if (string.IsNullOrEmpty(clientSideId))
            {
                clientSideId = UIDGenerator.NextPositive();
            }
            return await ClientSideIdResponseHandlerAsync(clientSideId, async () =>
            {
                return await _RestClient.RealtimeChatSendImageAsync(imageData, type, clientSideId, _Storage);
            });
        }

        public async Task<WMAPIResponse<string>> SendFileAsync(byte[] fileData, string fileName, string mimeType, string clientSideId = null)
        {
            if (string.IsNullOrEmpty(clientSideId))
            {
                clientSideId = UIDGenerator.NextPositive();
            }
            return await ClientSideIdResponseHandlerAsync(clientSideId, async () =>
            {
                return await _RestClient.RealtimeChatSendFileAsync(fileData, fileName, mimeType, clientSideId, _Storage);
            });
        }

        private string LastComposedSentDraft { get; set; }      // last sent message
        private string LastComposedCachedDraft { get; set; }    // latest message to be send
        private DateTime LastComposedSentDate { get; set; }     // last sent message time
        private bool LastComposedSentIsTyping { get; set; }     // last sent isTyping flag
        private bool LastComposedCachedIsTyping { get; set; }   // latest isTyping flag to be send

        private bool SendingComposingHasChanges_ = false;       // there were changes while sedning current state

        private bool DraftChanged(string draft)
        {
            bool hasDraft = !string.IsNullOrEmpty(draft);
            bool hasLastComposed = !string.IsNullOrEmpty(LastComposedSentDraft);
            
            if (!hasDraft && !hasLastComposed)
            {
                return false;
            }
            else if ((!hasLastComposed && hasDraft) || (hasLastComposed && !hasDraft))
            {
                return true;
            }
            else
            {
                return draft != LastComposedSentDraft;
            }
        }

        private async Task SendComposingMessageAsync()
        {
            await SimpleResponseExectionHandler(async () =>
            {
                var typing = LastComposedCachedIsTyping;
                var draft = LastComposedCachedDraft;
                bool changed = DraftChanged(LastComposedCachedDraft);
                SendingComposingHasChanges_ = false;

                var result = await _RestClient.RealtimeChatSetComposingMessageAsync(typing, changed, draft, _Storage);
                LastComposedSentDate = DateTime.Now;
                LastComposedSentDraft = draft;
                LastComposedSentIsTyping = typing;

                return result;
            });
        }

        private void TrySetComposingWithTimerAsync(int repeatTimes)
        {
            if (ComposingTimer != null)
            {
                // make sure we'll send this changes, if sending current ones is in progress
                SendingComposingHasChanges_ = true;
                return;
            }

            var time = DateTime.Now - LastComposedSentDate;

            ComposingTimer = ThreadPoolTimer.CreateTimer(async (handler) =>
            {
                await SendComposingMessageAsync();
                ComposingTimer = null;
                if (repeatTimes > 10) // avoid endless loops. (are they possible?)
                {
                    return;
                }
                if (SendingComposingHasChanges_)
                {
                    TrySetComposingWithTimerAsync(repeatTimes + 1);
                }
            }, TimeSpan.FromSeconds(time.TotalSeconds > 2 ? 0 : 2));
        }

        public void SetComposingMessage(bool isComposing, string draft)
        {
            // Put lates changes to variables and try to send them
            LastComposedCachedIsTyping = isComposing;
            LastComposedCachedDraft = draft;

            TrySetComposingWithTimerAsync(0);
        }

        public async Task<bool> SetDeviceTokenAsync(string token)
        {
            return await SimpleResponseExectionHandler(async () =>
            {
                return await _RestClient.RealtimeSetupPushTokenAsync(token, _Storage);
            });
        }

        public async Task<bool> RateOperatorWithRateAsync(string authorID, WMOperatorRate rate)
        {
            return await SimpleResponseExectionHandler(async () =>
            {
                return await _RestClient.RealtimeRateOeratorWithRate(authorID, rate, _Storage);
            });
        }

        private async Task<bool> SimpleResponseExectionHandler(Func<Task<HttpResponseMessage>> block)
        {
            try
            {
                using (var response = await block())
                {
                    return await ProcessSimpleRequestResponse(response);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<WMAPIResponse<string>> ClientSideIdResponseHandlerAsync(string clientSideId, Func<Task<HttpResponseMessage>> block)
        {
            try
            {
                using (var response = await block())
                {
                    bool success = await ProcessSimpleRequestResponse(response);
                    return new WMAPIResponse<string>(success, response, WMSessionError.WMSessionErrorUnknown, clientSideId);
                }
            }
            catch (Exception)
            {
                return new WMAPIResponse<string>(false, null, WMSessionError.WMSessionErrorNetworkError, clientSideId);
            }
        }
        #endregion

        #region RequestResponse Processor

        List<WMBaseSession.WMSessionError> DeltaHalterCodesList = new List<WMSessionError>()
            {
                WMBaseSession.WMSessionError.WMSessionErrorAccountBlocked,
                WMBaseSession.WMSessionError.WMSessionErrorNetworkError,
                WMBaseSession.WMSessionError.WMSessionErrorServerNotReady,
                WMBaseSession.WMSessionError.WMSessionErrorVisitorBanned,
            };

        private async Task<bool> ProcessSimpleRequestResponse(HttpResponseMessage response)
        {
            bool isOkCode = RealtimeIsSuccessResponse(response);
            if (isOkCode)
            {
                return isOkCode;
            }

            WMSessionError error = RealtimeErrorInResponse(response);
            if (error == WMSessionError.WMSessionErrorReinitRequired)
            {
                _Storage.Revision = 0;
                await GetInitialDeltaAsync();
            }
            else
            {
                if (DeltaHalterCodesList.Contains(error))
                {
                    _RestClient.CancelDeltaRequest();
                }

                if (SessionDidReceiveError != null)
                {
                    SessionDidReceiveError(this, error);
                }
            }
            return false;
        }

        private bool ProcessDeltaRequestResponseStatus(HttpResponseMessage response)
        {
            WMSessionError error = RealtimeErrorInResponse(response);
            if (error == WMSessionError.WMSessionErrorUnknown)
            {
                return true;
            }

            if (error == WMSessionError.WMSessionErrorReinitRequired)
            {
                _Storage.Revision = 0;
                Task task = GetInitialDeltaAsync();
            }
            else
            {
                if (DeltaHalterCodesList.Contains(error))
                {
                    CancelDeltaAndRestartAfterInterval(DeltaRestartOnNetworkErrorTimeSpan);
                }
                if (SessionDidReceiveError != null)
                {
                    SessionDidReceiveError(this, error);
                }
            }
            return false;
        }

        #endregion
    }
}
