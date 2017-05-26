using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Windows.Data.Json;
using System.Diagnostics;

namespace WebimSDK
{
    public class WMOfflineSession : WMBaseSession
    {
        private RestClient _RestClient { get; set; }
        private SharedStorage _SharedStorage = SharedStorage.Instance;
        private bool isSyncInProgress;

        public string Token { get; set; }
        public string Platfom { get; set; }

        public WMOfflineSession(string account, string location, string token, string platform) : base(account, location)
        {
            Token = token;
            Platfom = platform;

            _RestClient = new RestClient(Host());
        }

        public WMOfflineSession(Uri hostUri, string location, string token, string platform) : base(hostUri, location)
        {
            Token = token;
            Platfom = platform;

            _RestClient = new RestClient(Host());
        }

        public async Task Initialize()
        {
            var accountName = AccountName ?? (HostUri != null ? HostUri.ToString() : null);
            await _SharedStorage.InitializeForAccount(accountName);

            foreach (var chat in _SharedStorage.ChatsList)
            {
                chat.Session = this;
            }

            _SharedStorage.Token = Token;
            _SharedStorage.Platform = Platfom;
            _SharedStorage.Location = Location;

            await _SharedStorage.Flush();
        }

        public List<WMChat> ChatsList()
        {
            if (_SharedStorage.isInitialized)
            {
                var chatsToDisplay = from chat in _SharedStorage.ChatsList
                                     where chat.Status != WMChat.WMChatStatus.Deleted
                                     select chat;
                return chatsToDisplay.ToList();
            }
            return new List<WMChat>();
        }

        public async Task<WMAPIResponse<WMHistoryChanges>> GetHistoryAsync()
        {
            return await GetHistoryAsync(false);
        }

        public async Task<WMAPIResponse<WMHistoryChanges>> GetHistoryAsync(bool forced)
        {
            if (_SharedStorage.VisitorID == null)
            {
                try
                {
                    await this.StartSessionAsync();
                }
                catch (Exception)
                {
                    return new WMAPIResponse<WMHistoryChanges>(false, null, WMSessionError.WMSessionErrorNetworkError, null);
                }
            }
            if (_SharedStorage.VisitorID == null)
            {
                return new WMAPIResponse<WMHistoryChanges>(false, null, WMSessionError.WMSessionErrorNotConfigured, new WMHistoryChanges());
            }

            try
            {
                using (HttpResponseMessage response = await _RestClient.GetHistoryAsync(forced, _SharedStorage))
                {
                    response.EnsureSuccessStatusCode();
                    return await ResponseProcessor.ProcessGetHistoryResponseAsync(response, _SharedStorage, this);
                }
            }
            catch (Exception)
            {
                return new WMAPIResponse<WMHistoryChanges>(false, null, WMSessionError.WMSessionErrorNetworkError, null);
            }
        }

        public async Task<WMAPIResponse<WMMessage>> SendMessageAsync(string text, WMChat chat, string subject, string departmentKey)
        {
            return await SendMessageAsync(text, chat, subject, departmentKey, null);
        }

        public async Task<WMAPIResponse<WMMessage>> SendMessageAsync(string text, WMChat chat, string departmentKey)
        {
            return await SendMessageAsync(text, chat, null, departmentKey, null);
        }

        private async Task<WMAPIResponse<WMMessage>> SendMessageAsync(string text, WMChat chat, string subject, string departmentKey, string fileDescriptor)
        {
            var newMessageResponse = await OfflineSendMessageAsync(text, chat, subject, departmentKey, fileDescriptor);

            // Before each action we are refreshing a session to avoid problems with visitor object changes
            var responseObject = await StartSessionAsync();
            if (!responseObject.Successful)
            {
                var failResponse = new WMAPIResponse<WMMessage>(responseObject);
                failResponse.ResponseData = newMessageResponse.ResponseData;
                return failResponse;
            }

            if (chat != null && chat.Status == WMChat.WMChatStatus.Deleted)
            {
                throw new Exception("An attempt to send message to for the deleted chat");
            }
            else if ((chat != null && chat.Status == WMChat.WMChatStatus.NotSent && responseObject.WebimError == WMSessionError.WMSessionErrorUnknown) ||
                responseObject.WebimError == WMSessionError.WMSessionErrorNetworkError)
            {
#if (false)
                // If user tries to send a message to the chat, created offline, first, send all the offline actions
                await SendUnsentRequestsAsync();
#else
                // On that moment don't send a message, just add it to the offline queue
                return newMessageResponse;
#endif
            }

            try
            {
                string chatID = chat == null ? null : chat.Uid;
                string clientID = newMessageResponse.ResponseData.ClientRID;
                using (HttpResponseMessage sendMessageResponse = await _RestClient.SendMessageAsync(text, subject, departmentKey, chatID, fileDescriptor, clientID, _SharedStorage))
                {
                    sendMessageResponse.EnsureSuccessStatusCode();
                    return await ResponseProcessor.ProcessSendMessageResponseAsync(sendMessageResponse, _SharedStorage, chat, this);
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (Exception)
            {
            }

            return newMessageResponse;
        }

        private async Task<WMAPIResponse<WMMessage>> OfflineSendMessageAsync(string text, WMChat chat, string subject, string departmentKey, string fileDescriptor)
        {
            Object messageOrChat = OfflineWorkHelper.SendMessage(text, chat, subject, departmentKey);
            WMMessage message = null;

            if (chat == null)
            {
                chat = (WMChat)messageOrChat;
                message = chat.Messages[0];
                _SharedStorage.OfflineAddChat(chat);
            }
            else
            {
                message = (WMMessage)messageOrChat;
            }

            await _SharedStorage.SaveToFile();
            return new WMAPIResponse<WMMessage>(true, null, WMSessionError.WMSessionErrorNetworkError, message);
        }

        public async Task<WMAPIResponse<bool>> DeleteChatAsync(WMChat chat)
        {
            if (chat.Status == WMChat.WMChatStatus.NotSent)
            {
                bool isOk = await ResponseProcessor.DoDeleteChat(_SharedStorage, chat);
                return new WMAPIResponse<bool>(true, null, 0, true);
            }

            var responseObject = await StartSessionAsync();
            if (!responseObject.Successful)
            {
                return new WMAPIResponse<bool>(responseObject);
            }
            if (chat == null || chat.Uid == null)
            {
                throw new Exception("Invalid argument: expected valid id of chat");
            }
            if (responseObject.WebimError == WMSessionError.WMSessionErrorNetworkError)
            {
                return await OfflineDeleteChatAsync(chat);
            }

            try
            {
                return await DoDeleteChatAsync(chat);
            }
            catch (Exception)
            {
            }
            return await OfflineDeleteChatAsync(chat);
        }

        private async Task<WMAPIResponse<bool>> OfflineDeleteChatAsync(WMChat chat)
        {
            chat.Status = WMChat.WMChatStatus.Deleted;
            chat.SyncTask = WMChat.SyncronizationTask.Delete;
            await _SharedStorage.SaveToFile();
            return new WMAPIResponse<bool>(true, null, WMSessionError.WMSessionErrorNetworkError, true);
        }

        public async Task<WMAPIResponse<bool>> MarkChatAsReadAsync(WMChat chat)
        {
            var responseObject = await StartSessionAsync();
            if (!responseObject.Successful)
            {
                return new WMAPIResponse<bool>(responseObject);
            }
            if (chat == null || chat.Uid == null)
            {
                throw new Exception("Invalid argument: expected valid id of chat");
            }
            if (chat.Status == WMChat.WMChatStatus.NotSent || responseObject.WebimError == WMSessionError.WMSessionErrorNetworkError)
            {
#if (false)
                await SendUnsentRequestsAsync();
#else
                return await OfflineMarkChatAsReadAsync(chat);
#endif
            }
            else if (chat.Status == WMChat.WMChatStatus.Deleted)
            {
                throw new Exception("An attempt to mark as read deleted chat");
            }

            try
            {
                return await DoMarkChatAsReadAsync(chat);
            }
            catch (Exception)
            {
            }
            return await OfflineMarkChatAsReadAsync(chat);
        }

        private async Task<WMAPIResponse<bool>> OfflineMarkChatAsReadAsync(WMChat chat)
        {
            if (chat.HasUnreadMessages)
            {
                chat.HasUnreadMessages = false;
                chat.SyncTask = WMChat.SyncronizationTask.MarkAsRead;
                await _SharedStorage.SaveToFile();
            }
            return new WMAPIResponse<bool>(true, null, WMSessionError.WMSessionErrorNetworkError, true);
        }

        public async Task<WMAPIResponse<WMMessage>> SendImageAsync(byte[] imageData, WMChatAttachmentImageType type, WMChat chat, string subject, string departmentKey)
        {
            string mimeType = type == WMChatAttachmentImageType.WMChatAttachmentImagePNG ? "image/png" : "image/jpeg";
            return await SendFileAsync(imageData, mimeType, chat, subject, departmentKey);
        }

        public async Task<WMAPIResponse<WMMessage>> SendImageAsync(byte[] imageData, string fileName, WMChatAttachmentImageType type, WMChat chat, string departmentKey)
        {
            string mimeType = type == WMChatAttachmentImageType.WMChatAttachmentImagePNG ? "image/png" : "image/jpeg";
            return await SendFileAsync(imageData, fileName, mimeType, chat, null, departmentKey);
        }

        public async Task<WMAPIResponse<WMMessage>> SendImageAsync(byte[] imageData, string fileName, WMChatAttachmentImageType type, WMChat chat, string subject, string departmentKey)
        {
            string mimeType = type == WMChatAttachmentImageType.WMChatAttachmentImagePNG ? "image/png" : "image/jpeg";
            return await SendFileAsync(imageData, fileName, mimeType, chat, subject, departmentKey);
        }

        public async Task<WMAPIResponse<WMMessage>> SendFileAsync(byte[] fileData, string fileName, string mimeType, WMChat chat, string subject, string departmentKey)
        {
            var responseObject = await StartSessionAsync();
            if (!responseObject.Successful)
            {
                return new WMAPIResponse<WMMessage>(responseObject);
            }

            if ((chat != null && chat.Status == WMChat.WMChatStatus.NotSent) || responseObject.WebimError == WMSessionError.WMSessionErrorNetworkError)
            {
                return await OfflineSendFileAsync(fileData, fileName, mimeType, chat, subject, departmentKey);
            }

            try
            {
                string chatID = chat == null ? null : chat.Uid;
                using (HttpResponseMessage sendFileResponse = await _RestClient.SendFileAsync(fileData, fileName, mimeType, _SharedStorage))
                {
                    sendFileResponse.EnsureSuccessStatusCode();
                    WMAPIResponse<string> processorResponse = await ResponseProcessor.ProcessSendFile(sendFileResponse);
                    if (!processorResponse.Successful)
                    {
                        return new WMAPIResponse<WMMessage>(processorResponse);
                    }
                    string fileDescriptor = processorResponse.ResponseData;
                    var sendMessageResponse = await SendMessageAsync(null, chat, subject, departmentKey, fileDescriptor);
                    return sendMessageResponse;
                }
            }
            catch (Exception)
            {
            }

            return await OfflineSendFileAsync(fileData, fileName, mimeType, chat, subject, departmentKey);
        }

        public async Task<WMAPIResponse<WMMessage>> SendFileAsync(byte[] fileData, string fileName, string mimeType, WMChat chat, string departmentKey)
        {
            return await SendFileAsync(fileData, fileName, mimeType, chat, null, departmentKey);
        }

        public async Task<WMAPIResponse<WMMessage>> SendFileAsync(byte[] fileData, string mimeType, WMChat chat, string subject, string departmentKey)
        {
            return await SendFileAsync(fileData, null, mimeType, chat, subject, departmentKey);
        }

        public async Task<WMAPIResponse<WMMessage>> SendFileAsync(byte[] fileData, string mimeType, WMChat chat, string departmentKey)
        {
            return await SendFileAsync(fileData, null, mimeType, chat, null, departmentKey);
        }

        private async Task<WMAPIResponse<WMMessage>> OfflineSendFileAsync(byte[] fileData, string fileName, string mimeType, WMChat chat, string subject, string departmentKey)
        {
            Object chatOrMessage = await OfflineWorkHelper.SendFileAsync(fileData, fileName, mimeType, chat, subject, departmentKey);
            WMMessage message = null;
            if (chat == null)
            {
                chat = (WMChat)chatOrMessage;
                message = chat.Messages[0];
                _SharedStorage.OfflineAddChat(chat);
            }
            else
            {
                message = (WMMessage)chatOrMessage;
            }
            await _SharedStorage.SaveToFile();
            return new WMAPIResponse<WMMessage>(true, null, WMSessionError.WMSessionErrorNetworkError, message);
        }

        public async Task<WMAPIResponse<WMSyncChanges>> SendUnsentRequestsAsync()
        {
            if (!isSyncInProgress)
            {
                isSyncInProgress = true;
                WMSyncChanges changes = new WMSyncChanges();
                bool shouldSave = false;

                try
                {
                    shouldSave = await DoSendUnsentAsync(changes);
                }
                catch (HttpRequestException)
                {
                    shouldSave = true;
                }
                catch (Exception)
                {
#if DEBUG
                    throw;
#endif
                }

                if (shouldSave)
                {
                    await _SharedStorage.SaveToFile();
                }

                isSyncInProgress = false;
                Debug.WriteLine("ended sync");
                return new WMAPIResponse<WMSyncChanges>(true, null, 0, changes);
            }
            return new WMAPIResponse<WMSyncChanges>(false, null, 0, null);
        }

        private async Task<bool> DoSendUnsentAsync(WMSyncChanges changes)
        {
            bool hasChanges = false;
            // Refresh session only once, then though the loop:
            // 1. Delete chats, scheduled for removal
            // 2. Create offline chats, for each chat, submit all its offline messages
            // 3. Mark chats as read if they were marked so

            var responseObject = await StartSessionAsync();
            if (responseObject.WebimError == WMSessionError.WMSessionErrorNetworkError)
            {
                return false;
            }

            // 1
            var chatsToDelete = from chat in _SharedStorage.ChatsList
                                where chat.Status == WMChat.WMChatStatus.Deleted || chat.SyncTask == WMChat.SyncronizationTask.Delete
                                select chat;
            foreach (WMChat chat in chatsToDelete)
            {
                await DoDeleteChatAsync(chat);
                hasChanges = true;
            }

            // 2
            List<WMChat> rejectedChats = new List<WMChat>();
            foreach (WMChat chat in _SharedStorage.ChatsList)
            {
                foreach (WMMessage message in chat.Messages)
                {
                    if (message.Status == WMMessage.WMMessageStatus.NotSent)
                    {
                        var sendData = await SendOfflineMessageAsync(message, chat);
                        if (sendData.Successful)
                        {
                            changes.Add(sendData.ResponseData);
                            hasChanges = true;
                        }
                        else
                        {
                            if (IsCriticalWebimError(responseObject.WebimError))
                            {
                                changes.RejectedMessages.Add(message.Uid);
                                chat.Messages.Remove(message);
                                if (chat.Messages.Count == 0)
                                {
                                    rejectedChats.Add(chat);
                                }
                                hasChanges = true;
                            }
                        }
                    }
                }
            }
            foreach (var chat in rejectedChats)
            {
                changes.RejectedChats.Add(chat.Uid);
                _SharedStorage.OfflineRemoveChat(chat);
            }

            // 3
            var chatsToMark = from chat in _SharedStorage.ChatsList
                              where chat.SyncTask == WMChat.SyncronizationTask.MarkAsRead
                              select chat;
            foreach (WMChat chat in chatsToMark)
            {
                await DoMarkChatAsReadAsync(chat);
                hasChanges = true;
            }
            return hasChanges;
        }

        private bool IsCriticalWebimError(WMBaseSession.WMSessionError error)
        {
            switch (error)
            {
                case WMSessionError.WMSessionErrorAttachmentSizeExceeded:
                case WMSessionError.WMSessionErrorAttachmentTypeNotAllowed:
                case WMSessionError.WMSessionErrorMessageSizeExceeded:
                case WMSessionError.WMSessionErrorChatNotFound:
                    return true;
            }
            return false;
        }

        #region Do Networking Methods

        private async Task<WMAPIResponse<bool>> DoStartSessionAsync()
        {
            WMAPIResponse<bool> response = null;
            int TotalTurns = 3;
            for (int i = 0; i < TotalTurns; i++)
            {
                int turn = i + 1;
                using (HttpResponseMessage httpResponse = await _RestClient.StartSessionAsync(_SharedStorage))
                {
                    httpResponse.EnsureSuccessStatusCode();
                    response = await ResponseProcessor.ProcessStartSessionResponse(httpResponse, _SharedStorage);
                    if (response.Successful || turn == TotalTurns)
                    {
                        return response;
                    }
                    await Task.Delay(turn * 1000); // in seconds
                }
            }
            return response;
        }

        private async Task<WMAPIResponse<bool>> StartSessionAsync()
        {
            try
            {
                return await DoStartSessionAsync();
            }
            catch (Exception)
            {
                return new WMAPIResponse<bool>(true, null, WMSessionError.WMSessionErrorNetworkError, false);
            }
        }

        private async Task<WMAPIResponse<bool>> DoDeleteChatAsync(WMChat chat)
        {
            using (HttpResponseMessage deleteMessageResponse = await _RestClient.DeleteMessageAsync(chat.Uid, _SharedStorage))
            {
                deleteMessageResponse.EnsureSuccessStatusCode();
                return await ResponseProcessor.ProcessDeleteChat(deleteMessageResponse, _SharedStorage, chat);
            }
        }

        private async Task<WMAPIResponse<bool>> DoMarkChatAsReadAsync(WMChat chat)
        {
            using (HttpResponseMessage markChatResponse = await _RestClient.MarkChatAsReadAsync(chat.Uid, _SharedStorage))
            {
                markChatResponse.EnsureSuccessStatusCode();
                return await ResponseProcessor.ProcessMarkChatAsRead(markChatResponse, _SharedStorage, chat);
            }
        }

        private async Task<WMAPIResponse<WMSyncChanges>> SendOfflineMessageAsync(WMMessage message, WMChat chat)
        {
            bool needsToCreateChat = chat.Status == WMChat.WMChatStatus.NotSent;
            string chatID = needsToCreateChat ? null : chat.Uid;
            string fileDescriptor = null;

            // If this offline message is a file-message, send it first and then send as ordinary message with file descriptor
            if (message.Kind == WMMessage.WMMessageKind.WMMessageKindFileFromVisitor)
            {
                byte[] fileData = await OfflineWorkHelper.LoadFileForMessageAsync(message);
                using (var sendFileResponse = await _RestClient.SendFileAsync(fileData, message.FileName, message.MimeType, _SharedStorage))
                {
                    sendFileResponse.EnsureSuccessStatusCode();
                    WMAPIResponse<string> processorResponse = await ResponseProcessor.ProcessSendFile(sendFileResponse);
                    if (!processorResponse.Successful)
                    {
                        return new WMAPIResponse<WMSyncChanges>(false, sendFileResponse, processorResponse.WebimError, new WMSyncChanges());
                    }
                    await OfflineWorkHelper.DeleteFileForMessage(message);

                    fileDescriptor = processorResponse.ResponseData;
                }
            }

            using (var response = await _RestClient.SendMessageAsync(message.Text, chat.Subject, chat.DepartmentKey, chatID, fileDescriptor, message.ClientRID, _SharedStorage))
            {
                response.EnsureSuccessStatusCode();
                var processResponse = await ResponseProcessor.RawProcessSendMessageResponseAsync(response, needsToCreateChat, this);
                WMSyncChanges changes = null;
                string oldChatID = chat.Uid;
                string oldMsgID = message.Uid;

                if (!processResponse.Successful)
                {
                    return new WMAPIResponse<WMSyncChanges>(false, null, processResponse.WebimError, null);
                }

                if (chat.Status == WMChat.WMChatStatus.NotSent)
                {
                    chat.MergeOffline((WMChat)processResponse.ResponseData);
                    changes = new WMSyncChanges(new WMSyncBlob(oldChatID, chat.Uid), new WMSyncBlob(oldMsgID, message.Uid));
                }
                else
                {
                    message.MergeOffline((WMMessage)processResponse.ResponseData);
                    changes = new WMSyncChanges(null, new WMSyncBlob(oldMsgID, message.Uid));
                }
                return new WMAPIResponse<WMSyncChanges>(true, response, 0, changes);
            }
        }

        #endregion

        internal async Task<WMAPIResponse<bool>> StartSessionStubMethod() // Don't expose this method
        {
            return await StartSessionAsync();
        }

        public async Task ClearCachedUserDataAsync()
        {
            await _SharedStorage.ClearStorageRecoverableData();
        }

        #region Accessing internal data

        public WMChat ChatForMessage(WMMessage message)
        {
            return _SharedStorage.FindChatByID(message.ChatId);
        }

        #endregion
    }
}
