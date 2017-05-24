using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Data.Json;
using System.Runtime.Serialization;

namespace WebimSDK
{
    [DataContract]
    public class WMChat
    {
        public enum WMChatState
        {
            WMChatStateUnknown,
            WMChatStateQueue,
            WMChatStateChatting,
            WMChatStateClosed,
            WMChatStateClosedByVisitor,
            WMChatStateClosedByOperator,
            WMChatStateInvitation,
        }

        public enum WMChatStatus
        {
            Sent,
            NotSent,
            Deleted,
        }

        internal enum SyncronizationTask
        {
            None,
            MarkAsRead,
            Delete,
        }

        [DataMember]
        public WMChatState State { get; set; }

        [DataMember]
        public WMChatStatus Status { get; set; }

        [DataMember]
        internal SyncronizationTask SyncTask { get; set; }

        [DataMember]
        public bool IsOffline { get; set; }

        [DataMember]
        public bool HasUnreadMessages { get; set; }

        [DataMember]
        public DateTime UnreadByOperatorTimestamp { get; set; }

        [DataMember]
        public string Uid { get; set; }

        [DataMember]
        public string Subject { get; set; }

        [DataMember]
        public string DepartmentKey { get; set; }

        [DataMember]
        public List<WMMessage> Messages { get; set; }

        [DataMember]
        public string ClientSideId { get; set; }

        // For Realtime
        public WMOperator ChatOperator { get; set; }

        public bool OperatorTyping { get; set; }

        private WMBaseSession _Session = null;
        internal WMBaseSession Session
        {
            get
            {
                return _Session;
            }
            set
            {
                _Session = value;
                foreach (WMMessage message in Messages)
                {
                    message.Session = value;
                }
            }
        }

        public WMChat() {}

        internal WMChat(JsonValue jsonValue)
        {
            InitializeWithJsonValue(jsonValue, null);
        }

        internal WMChat(JsonValue jsonValue, WMBaseSession session)
        {
            InitializeWithJsonValue(jsonValue, session);
        }

        private void InitializeWithJsonValue(JsonValue jsonValue, WMBaseSession session)
        {
            _Session = session;

            JsonObject jsonObject = jsonValue.GetObject();
            IJsonValue value;
            if (jsonObject.TryGetValue("id", out value))
            {
                if (value.ValueType == JsonValueType.String)
                {
                    Uid = value.GetString();
                }
                else if (value.ValueType == JsonValueType.Number)
                {
                    Uid = value.GetNumber().ToString();
                }
            }
            if (jsonObject.TryGetValue("offline", out value) && value.ValueType == JsonValueType.Boolean)
            {
                IsOffline = value.GetBoolean();
            }
            if (jsonObject.TryGetValue("state", out value) && value.ValueType == JsonValueType.String)
            {
                State = StateFromString(value.GetString());
            }
            else
            {
                State = WMChatState.WMChatStateUnknown;
            }
            if (jsonObject.TryGetValue("unreadByOperatorSinceTs", out value) && value.ValueType == JsonValueType.Number)
            {
                double unreadByOperatorSince = value.GetNumber();
                UnreadByOperatorTimestamp = DateTimeHelper.DateTimeFromTimestamp(unreadByOperatorSince);
            }
            else
            {
                UnreadByOperatorTimestamp = default(DateTime);
            }
            if (jsonObject.TryGetValue("messages", out value) && value.ValueType == JsonValueType.Array)
            {
                Messages = MessagesFromJsonArray(value.GetArray(), session);
            }
            if (jsonObject.TryGetValue("subject", out value) && value.ValueType == JsonValueType.String)
            {
                Subject = value.GetString();
            }

            if (jsonObject.TryGetValue("unreadByVisitorSinceTs", out value))
            {
                // We assume that this field is present and null for a case when its read by visitor
                if (value.ValueType == JsonValueType.Null)
                {
                    HasUnreadMessages = false;
                }
                else
                {
                    HasUnreadMessages = true;
                }
            }
            if (jsonObject.TryGetValue("operator", out value) && value.ValueType == JsonValueType.Object)
            {
                if (ChatOperator == null)
                {
                    ChatOperator = new WMOperator((JsonValue)value);
                }
                else
                {
                    ChatOperator.Initialize((JsonValue)value);
                }
            }
            if (jsonObject.TryGetValue("operatorTyping", out value) && value.ValueType == JsonValueType.Boolean)
            {
                OperatorTyping = value.GetBoolean();
            }
            else
            {
                OperatorTyping = false;
            }
            if (jsonObject.TryGetValue("clientSideId", out value) && value.ValueType == JsonValueType.String)
            {
                ClientSideId = value.GetString();
            }
        }

        private List<WMMessage> MessagesFromJsonArray(JsonArray jsonArray, WMBaseSession session)
        {
            List<WMMessage> messagesList = new List<WMMessage>();
            foreach (var item in jsonArray)
            {
                WMMessage message = new WMMessage((JsonValue)item, session);
                messagesList.Add(message);
            }
            return messagesList;
        }

        internal List<WMMessage> Merge(WMChat chat)
        {
            State = chat.State;
            IsOffline = chat.IsOffline;
            HasUnreadMessages = chat.HasUnreadMessages;
            UnreadByOperatorTimestamp = chat.UnreadByOperatorTimestamp;
            ClientSideId = chat.ClientSideId;
         
            // Merge Messages
            List<WMMessage> newMessages = new List<WMMessage>();
            foreach (WMMessage item in chat.Messages)
            {
                WMMessage message = FindMessageByID(item.Uid);
                if (message == null)
                {
                    newMessages.Add(item);
                }
            }
            if (newMessages.Count > 0)
            {
                if (Messages == null)
                {
                    Messages = new List<WMMessage>();
                }
                Messages.AddRange(newMessages);
                ResortMessages();
            }
            return newMessages;
        }

        internal void ResortMessages()
        {
            Messages = Messages.OrderBy(s => s.Timestamp).ToList();
        }

        internal void MergeOffline(WMChat chat)
        {
            Uid = chat.Uid;
            State = chat.State;
            Status = WMChatStatus.Sent;
            IsOffline = chat.IsOffline;
            ClientSideId = chat.ClientSideId;
            if (SyncTask == SyncronizationTask.None)
            {
                HasUnreadMessages = chat.HasUnreadMessages;
            }
            UnreadByOperatorTimestamp = chat.UnreadByOperatorTimestamp;

            Messages[0].MergeOffline(chat.Messages[0]);
        }

        public WMMessage FindMessageByID(string uid)
        {
            foreach (var item in Messages)
            {
                if (item.Uid.Equals(uid))
                {
                    return item;
                }
            }
            return null;
        }

        internal WMChatState StateFromString(string value)
        {
            if ("queue".Equals(value))
            {
                return WMChatState.WMChatStateQueue;
            }
            else if ("chatting".Equals(value))
            {
                return WMChatState.WMChatStateChatting;
            }
            else if ("closed".Equals(value))
            {
                return WMChatState.WMChatStateClosed;
            }
            else if ("closed_by_visitor".Equals(value))
            {
                return WMChatState.WMChatStateClosedByVisitor;
            }
            else if ("closed_by_operator".Equals(value))
            {
                return WMChatState.WMChatStateClosedByOperator;
            }
            else if ("invitation".Equals(value))
            {
                return WMChatState.WMChatStateInvitation;
            }
            return WMChatState.WMChatStateUnknown;
        }
    }
}
