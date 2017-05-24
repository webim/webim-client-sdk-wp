using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Globalization.DateTimeFormatting;
using System.Runtime.Serialization;

namespace WebimSDK
{
    [DataContract]
    public class WMMessage
    {
        public enum WMMessageKind
        {
            WMMessageKindUnknown,
            WMMessageKindForOperator,
            WMMessageKindInfo,
            WMMessageKindVisitor,
            WMMessageKindOperator,
            WMMessageKindOperatorBusy,
            WMMessageKindContactsRequest,
            WMMessageKindContacts,
            WMMessageKindFileFromOperator,
            WMMessageKindFileFromVisitor,
        }

        public enum WMMessageStatus
        {
            Sent,
            Sending,
            NotSent,
        }

        [DataMember]
        public WMMessageKind Kind { get; set; }

        [DataMember]
        public WMMessageStatus Status { get; set; }

        [DataMember]
        public string Text { get; set; }

        [DataMember]
        public DateTime Timestamp { get; set; }

        [DataMember]
        public double Ts { get; set; }

        [DataMember]
        public string Uid { get; set; }

        [DataMember]
        public string ChatId { get; set; }

        [DataMember]
        public string AttachmentPath { get; set; }

        [DataMember]
        public string MimeType { get; set; }

        [DataMember]
        public string FileName { get; set; }

        [DataMember]
        public string ClientRID { get; set; }

        [DataMember]
        public string ClientSideId { get; set; }

        public string SenderUID
        {
            get
            {
                return AuthorID;
            }
            private set { }
        }

        public string SenderName
        {
            get
            {
                return Name;
            }
            private set { }
        }

        public Uri SenderAvatarURL
        {
            get
            {
                if (string.IsNullOrEmpty(Avatar) || Session == null)
                {
                    return null;
                }
                return new Uri(Session.Host() + "/" + Avatar);
            }
            private set { }
        }

        [DataMember]
        internal string AuthorID { get; set; }

        [DataMember]
        internal string Avatar { get; set; }

        [DataMember]
        internal string Name { get; set; }

        internal WMBaseSession Session { get; set; }

        public WMMessage()
        {

        }

        internal WMMessage(JsonValue jsonValue)
        {
            InitializeWithJsonValue(jsonValue, null);
            Status = WMMessageStatus.Sent;
        }

        internal WMMessage(JsonValue jsonValue, WMBaseSession session)
        {
            InitializeWithJsonValue(jsonValue, session);
            Status = WMMessageStatus.Sent;
        }

        private void InitializeWithJsonValue(JsonValue jsonValue, WMBaseSession session)
        {
            Session = session;

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
            if (jsonObject.TryGetValue("kind", out value) && value.ValueType == JsonValueType.String)
            {
                Kind = MessageKindFromString(value.GetString());
            }
            if (jsonObject.TryGetValue("chatId", out value))
            {
                if (value.ValueType == JsonValueType.String)
                {
                    ChatId = value.GetString();
                }
                else if (value.ValueType == JsonValueType.Number)
                {
                    ChatId = value.GetNumber().ToString();
                }
            }
            if (jsonObject.TryGetValue("text", out value))
            {
                Text = value.GetString();
                if (Kind == WMMessageKind.WMMessageKindFileFromOperator || Kind == WMMessageKind.WMMessageKindFileFromVisitor)
                {
                    JsonValue descrData = JsonValue.Parse(Text);
                    JsonObject descrDataObj = descrData.GetObject();

                    string guid = descrDataObj.ContainsKey("guid") ? descrDataObj.GetNamedString("guid") : null;
                    string file = descrDataObj.ContainsKey("filename") ? descrDataObj.GetNamedString("filename") : null;
                    AttachmentPath = string.Format("l/v/download/{0}/{1}", guid, file);
                }
            }
            if (jsonObject.TryGetValue("ts", out value) && value.ValueType == JsonValueType.Number)
            {
                Ts = value.GetNumber();
                Timestamp = DateTimeHelper.DateTimeFromTimestamp(Ts);
            }
            if (jsonObject.TryGetValue("authorId", out value) && value.ValueType != JsonValueType.Null)
            {
                if (value.ValueType == JsonValueType.String)
                {
                    AuthorID = value.GetString();
                }
                else if (value.ValueType == JsonValueType.Number)
                {
                    AuthorID = value.GetNumber().ToString();
                }
            }
            if (jsonObject.TryGetValue("avatar", out value) && value.ValueType == JsonValueType.String)
            {
                Avatar = value.GetString();
            }
            if (jsonObject.TryGetValue("name", out value) && value.ValueType == JsonValueType.String)
            {
                Name = value.GetString();
            }
            if (jsonObject.TryGetValue("clientSideId", out value) && value.ValueType == JsonValueType.String) 
            {
                ClientSideId = value.GetString();
            }
        }

        // Apply changes from the object created at network reply for offline message sending
        internal void MergeOffline(WMMessage message)
        {
            Status = WMMessageStatus.Sent;
            Kind = message.Kind;
            Text = message.Text;
            Ts = message.Ts;
            Timestamp = message.Timestamp;
            Uid = message.Uid;
            AttachmentPath = message.AttachmentPath;
            ChatId = message.ChatId;
            Avatar = message.Avatar;
            AuthorID = message.AuthorID;
            Name = message.AuthorID;
            ClientSideId = message.ClientSideId;
        }

        public bool isTextMessage()
        {
            return Kind == WMMessageKind.WMMessageKindVisitor || Kind == WMMessageKind.WMMessageKindOperator;
        }

        public bool isFileMessage()
        {
            return Kind == WMMessageKind.WMMessageKindFileFromOperator || Kind == WMMessageKind.WMMessageKindFileFromVisitor;
        }

        public void SetSenderDetails(string uid, string name, string avatarPath)
        {
            Name = name;
            AuthorID = uid;
            Avatar = avatarPath;
        }

        internal static WMMessageKind MessageKindFromString(string value)
        {
            if ("for_operator".Equals(value))
            {
                return WMMessageKind.WMMessageKindForOperator;
            }
            else if ("visitor".Equals(value))
            {
                return WMMessageKind.WMMessageKindVisitor;
            }
            else if ("operator".Equals(value))
            {
                return WMMessageKind.WMMessageKindOperator;
            }
            else if ("info".Equals(value))
            {
                return WMMessageKind.WMMessageKindInfo;
            }
            else if ("operator_busy".Equals(value))
            {
                return WMMessageKind.WMMessageKindOperatorBusy;
            }
            else if ("cont_req".Equals(value))
            {
                return WMMessageKind.WMMessageKindContactsRequest;
            }
            else if ("contacts".Equals(value))
            {
                return WMMessageKind.WMMessageKindContacts;
            }
            else if ("file_operator".Equals(value))
            {
                return WMMessageKind.WMMessageKindFileFromOperator;
            }
            else if ("file_visitor".Equals(value))
            {
                return WMMessageKind.WMMessageKindFileFromVisitor;
            }
            return WMMessageKind.WMMessageKindUnknown;
        }
    }
}
