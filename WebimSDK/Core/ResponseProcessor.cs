using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using System.Net.Http;

namespace WebimSDK
{
    class ResponseProcessor
    {
        #region History Response

        public static async Task<WMAPIResponse<WMHistoryChanges>> ProcessGetHistoryResponseAsync(HttpResponseMessage response, SharedStorage storage, WMBaseSession session)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new WMAPIResponse<WMHistoryChanges>(false, response, 0, new WMHistoryChanges());
            }

            string responseString = await response.Content.ReadAsStringAsync();
            JsonValue jsonValue = JsonValue.Parse(responseString);
            JsonObject jsonObject = jsonValue.GetObject();

            IJsonValue value;
            if (jsonObject.TryGetValue("error", out value) && value.ValueType == JsonValueType.String)
            {
                string errorString = value.GetString();
                WMBaseSession.WMSessionError errorCode = WMBaseSession.ErrorFromString(errorString);
                return new WMAPIResponse<WMHistoryChanges>(false, response, errorCode, new WMHistoryChanges());
            }

            WMHistoryChanges changes = new WMHistoryChanges();

            if (jsonObject.TryGetValue("chats", out value) && value.ValueType == JsonValueType.Array)
            {
                var responseChats = ChatsFromJsonArray(value.GetArray(), session);
                var newChatsChanges = ApplyNewHistoryChatsList(responseChats, storage);
                changes = newChatsChanges;
            }

            if (jsonObject.TryGetValue("messages", out value) && value.ValueType == JsonValueType.Array)
            {
                var responseMessages = MessagesFromJsonArray(value.GetArray(), session);
                var newMessagesChanges = ApplyNewMessagesList(responseMessages, storage);

                changes.ModifiedChats.AddRange(newMessagesChanges.ModifiedChats);
                changes.ModifiedChats = changes.ModifiedChats.Distinct().ToList();
                changes.Messages.AddRange(newMessagesChanges.Messages);
            }

            foreach (var chat in changes.ModifiedChats)
            {
                chat.ResortMessages();
            }

            bool revChanged = false;
            if (jsonObject.TryGetValue("lastChangeTs", out value) && value.ValueType == JsonValueType.Number)
            {
                var lastChangeTs = value.GetNumber();
                if (storage.LastChangeTs != lastChangeTs)
                {
                    storage.LastChangeTs = lastChangeTs;
                    revChanged = true;
                }
            }

            if (changes.HasChanges() || revChanged)
            {
                await storage.SaveToFile();
            }
            return new WMAPIResponse<WMHistoryChanges>(true, response, 0, changes);
        }

        private static List<WMChat> ChatsFromJsonArray(JsonArray jsonArray, WMBaseSession session)
        {
            List<WMChat> newChatsList = new List<WMChat>();
            foreach (var item in jsonArray)
            {
                WMChat chat = new WMChat((JsonValue)item, session);
                newChatsList.Add(chat);
            }
            return newChatsList;
        }

        private static List<WMMessage> MessagesFromJsonArray(JsonArray jsonArray, WMBaseSession session)
        {
            List<WMMessage> newMesssagesList = new List<WMMessage>();
            foreach (var item in jsonArray)
            {
                WMMessage message = new WMMessage((JsonValue)item, session);
                newMesssagesList.Add(message);
            }
            return newMesssagesList;
        }

        private static WMHistoryChanges ApplyNewHistoryChatsList(List<WMChat> chatsArray, SharedStorage storage)
        {
            /*
             * Foreach new instance:
             * 1. Try to find existed chat and update, if updated, add to the changes chats list
             * 2. If not found, append to the existed list and new chats list
             */

            List<WMChat> reallyNewChatsList = new List<WMChat>();
            List<WMChat> updateChatsList = new List<WMChat>();
            List<WMMessage> updatedMessagesList = new List<WMMessage>();

            foreach (var item in chatsArray)
            {
                WMChat chat = storage.FindChatByID(item.Uid);
                if (chat == null)
                {
                    reallyNewChatsList.Add(item);
                    storage.OfflineAddChat(item);
                    if (item.Messages != null)
                    {
                        updatedMessagesList.AddRange(item.Messages);
                    }
                }
                else
                {
                    List<WMMessage> newMessages = chat.Merge(item);
                    if (newMessages != null && newMessages.Count > 0)
                    {
                        updatedMessagesList.AddRange(newMessages);
                    }
                    updateChatsList.Add(chat);
                }
            }

            WMHistoryChanges historyChanges = new WMHistoryChanges();
            historyChanges.Messages = updatedMessagesList;
            historyChanges.NewChats = reallyNewChatsList;
            historyChanges.ModifiedChats = updateChatsList;
            return historyChanges;
        }

        private static WMHistoryChanges ApplyNewMessagesList(List<WMMessage> messagesList, SharedStorage storage)
        {
            List<WMChat> updatedChats = new List<WMChat>();

            foreach (var item in messagesList)
            {
                WMChat modifiedChat = storage.FindChatByID(item.ChatId);
                if (modifiedChat == null)
                {
                    throw new Exception("Chat for the message is not found"); // Low the priority
                }

                if (modifiedChat.Messages.Find(s => s.Uid.Equals(item.Uid)) == null)
                {
                    modifiedChat.Messages.Add(item);

                    if (!updatedChats.Contains(modifiedChat))
                    {
                        updatedChats.Add(modifiedChat);
                        modifiedChat.HasUnreadMessages = true;
                    }
                }
            }

            WMHistoryChanges historyChanges = new WMHistoryChanges();
            historyChanges.ModifiedChats = updatedChats;
            historyChanges.Messages = messagesList;
            return historyChanges;
        }

        #endregion

        #region Start Session

        public static async Task<WMAPIResponse<bool>> ProcessStartSessionResponse(HttpResponseMessage response, SharedStorage storage)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new WMAPIResponse<bool>(false, response, WMBaseSession.WMSessionError.WMSessionErrorUnknown, false);
            }
            string responseString = await response.Content.ReadAsStringAsync();
            if (responseString == null || responseString.Length == 0)
            {
                return new WMAPIResponse<bool>(false, response, WMBaseSession.WMSessionError.WMSessionErrorUnknown, false);
            }

            JsonValue jsonValue = JsonValue.Parse(responseString);
            JsonObject jsonObject = jsonValue.GetObject();
            if (jsonObject.ContainsKey("error"))
            {
                var error = WMBaseSession.ErrorFromString(jsonObject.GetNamedString("error"));
                return new WMAPIResponse<bool>(false, response, error, true);
            }
            else
            {
                var revision = jsonObject.GetNamedNumber("revision"); // Ignoring this value for offline chats, using lastChangeTs (see doc)
                var fullUpdateValue = jsonObject.GetNamedValue("fullUpdate");

                var fullUpdateObject = fullUpdateValue.GetObject();
                var visitSessionID = fullUpdateObject.GetNamedString("visitSessionId");
                var pageID = fullUpdateObject.GetNamedString("pageId");

                bool shouldSaveCached = false;

                if (fullUpdateObject.ContainsKey("visitor"))
                {
                    JsonValue visitorJsonValue = fullUpdateObject.GetNamedValue("visitor");
                    if (visitorJsonValue.GetObject().ContainsKey("id"))
                    {
                        var newVisitorID = visitorJsonValue.GetObject().GetNamedString("id");
                        if (storage.VisitorID == null || !storage.VisitorID.Equals(newVisitorID))
                        {
                            string visitorString = visitorJsonValue.Stringify();
                            if (storage.VisitorID != null)
                            {
                                await storage.ClearStorageRecoverableData();
                                storage.ChatsList = new List<WMChat>();
                            }
                            storage.Visitor = visitorString;
                            storage.VisitorID = newVisitorID;
                            shouldSaveCached = true;
                        }
                    }
                }
                if (string.IsNullOrEmpty(storage.VisitSessionID) || !storage.VisitSessionID.Equals(visitSessionID))
                {
                    storage.VisitSessionID = visitSessionID;
                    shouldSaveCached = true;
                }
                if (string.IsNullOrEmpty(storage.PageID) || !storage.PageID.Equals(pageID))
                {
                    storage.PageID = pageID;
                    shouldSaveCached = true;
                }

                if (shouldSaveCached)
                {
                    await storage.SaveToFile();
                }

                return new WMAPIResponse<bool>(true, response, WMBaseSession.WMSessionError.WMSessionErrorUnknown, true);
            }
        }

        #endregion

        #region Send Message

        public static async Task<WMAPIResponse<WMMessage>> ProcessSendMessageResponseAsync(HttpResponseMessage response, SharedStorage storage, WMChat chat, WMBaseSession session)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new WMAPIResponse<WMMessage>(false, response, 0, null);
            }

            string responseString = await response.Content.ReadAsStringAsync();
            JsonValue responseValue = JsonValue.Parse(responseString);
            JsonObject responseObject = responseValue.GetObject();

            if (responseObject.ContainsKey("error"))
            {
                var error = WMBaseSession.ErrorFromString(responseObject.GetNamedString("error"));
                return new WMAPIResponse<WMMessage>(false, response, error, null);
            }

            WMMessage message = null;
            JsonValue jsonValue = responseObject.GetNamedValue("data");

            if (chat == null)
            {
                // Create chat and put a message into it
                WMChat newChat = new WMChat(jsonValue, session);
                message = newChat.Messages.First();
                storage.OfflineAddChat(newChat);
            }
            else
            {
                // Append
                message = new WMMessage(jsonValue, session);
                chat.Messages.Add(message);
            }
            await storage.SaveToFile();
            return new WMAPIResponse<WMMessage>(true, response, 0, message);
        }

        public static async Task<WMAPIResponse<Object>> RawProcessSendMessageResponseAsync(HttpResponseMessage response, bool isNewChat, WMBaseSession session)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new WMAPIResponse<Object>(false, response, 0, null);
            }

            string responseString = await response.Content.ReadAsStringAsync();
            JsonValue responseValue = JsonValue.Parse(responseString);
            JsonObject responseObject = responseValue.GetObject();

            if (responseObject.ContainsKey("error"))
            {
                var error = WMBaseSession.ErrorFromString(responseObject.GetNamedString("error"));
                return new WMAPIResponse<Object>(false, response, error, null);
            }

            WMMessage message = null;
            JsonValue jsonValue = responseObject.GetNamedValue("data");

            if (isNewChat)
            {
                WMChat newChat = new WMChat(jsonValue, session);
                return new WMAPIResponse<Object>(true, response, 0, newChat);
            }
            else
            {
                message = new WMMessage(jsonValue, session);
                return new WMAPIResponse<Object>(true, response, 0, message);
            }
        }

        #endregion

        #region Delete Message

        public static async Task<WMAPIResponse<bool>> ProcessDeleteChat(HttpResponseMessage response, SharedStorage storage, WMChat chat)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new WMAPIResponse<bool>(false, response, 0, false);
            }

            string responseString = await response.Content.ReadAsStringAsync();
            JsonValue responseValue = JsonValue.Parse(responseString);
            JsonObject responseObject = responseValue.GetObject();

            if (responseObject.ContainsKey("error"))
            {
                var error = WMBaseSession.ErrorFromString(responseObject.GetNamedString("error"));
                if (error == WMBaseSession.WMSessionError.WMSessionErrorChatNotFound)
                {
                    bool ok = await DoDeleteChat(storage, chat);
                    return new WMAPIResponse<bool>(true, response, error, ok);
                }
                return new WMAPIResponse<bool>(false, response, error, false);
            }
            bool isOk = await DoDeleteChat(storage, chat);
            return new WMAPIResponse<bool>(true, response, 0, isOk);
        }

        internal static async Task<bool> DoDeleteChat(SharedStorage storage, WMChat chat)
        {
            bool ret = storage.OfflineRemoveChat(chat);
            await storage.SaveToFile();
            return ret;
        }

        #endregion

        #region Mark Message

        public static async Task<WMAPIResponse<bool>> ProcessMarkChatAsRead(HttpResponseMessage response, SharedStorage storage, WMChat chat)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new WMAPIResponse<bool>(false, response, 0, false);
            }

            string responseString = await response.Content.ReadAsStringAsync();
            JsonValue responseValue = JsonValue.Parse(responseString);
            JsonObject responseObject = responseValue.GetObject();

            if (responseObject.ContainsKey("error"))
            {
                var error = WMBaseSession.ErrorFromString(responseObject.GetNamedString("error"));
                return new WMAPIResponse<bool>(false, response, error, false);
            }
            chat.SyncTask = WMChat.SyncronizationTask.None;
            chat.HasUnreadMessages = false;
            await storage.SaveToFile();
            return new WMAPIResponse<bool>(true, response, 0, true);
        }
        #endregion

        #region Send File

        public static async Task<WMAPIResponse<string>> ProcessSendFile(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new WMAPIResponse<string>(false, response, 0, null);
            }

            string responseString = await response.Content.ReadAsStringAsync();
            JsonValue responseValue = JsonValue.Parse(responseString);
            JsonObject responseObject = responseValue.GetObject();

            if (responseObject.ContainsKey("error"))
            {
                var error = WMBaseSession.ErrorFromString(responseObject.GetNamedString("error"));
                return new WMAPIResponse<string>(false, response, error, null);
            }
            string value = responseObject.GetNamedObject("data").Stringify();
            return new WMAPIResponse<string>(true, response, 0, value);
        }
        #endregion
    }
}
