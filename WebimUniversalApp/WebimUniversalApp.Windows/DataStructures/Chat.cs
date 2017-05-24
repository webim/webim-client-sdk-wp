using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WebimSDK;

namespace WebimUniversalApp.DataStructures
{
    public class Chat : INotifyPropertyChanged
    {
        private string _LastMessageText;
        public string LastMessageText
        {
            get
            {
                return _LastMessageText;
            }
            set
            {
                _LastMessageText = value;
                OnPropertyChanged("LastMessageText");
            }
        }

        private string _LastMessageDate;
        public string LastMessageDate
        {
            get
            {
                return _LastMessageDate;
            }
            set
            {
                _LastMessageDate = value;
                OnPropertyChanged("LastMessageDate");
            }
        }

        private string _HasUnreadMessages;
        public string HasUnreadMessages
        {
            get
            {
                return _HasUnreadMessages;
            }
            set
            {
                _HasUnreadMessages = value;
                OnPropertyChanged("HasUnreadMessages");
            }
        }

        private WMChat _WebimChat;
        public WMChat WebimChat
        {
            get
            {
                return _WebimChat;
            }
            set
            {
                this.ApplyWMChat(value);
            }
        }

        public Chat(WMChat chat)
        {
            this.ApplyWMChat(chat);
        }

        private void ApplyWMChat(WMChat chat)
        {
            _WebimChat = chat;

            string lastMessage = "Not set";
            string firstMessageDate = "Unknown date";

            if (chat.Messages != null || chat.Messages.Count > 0)
            {
                WMMessage message = chat.Messages.Last();
                lastMessage = message.Text;
                firstMessageDate = message.Timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            this.LastMessageDate = firstMessageDate;
            this.LastMessageText = lastMessage;
            this.HasUnreadMessages = chat.HasUnreadMessages ? "Has Unread Messages" : "";
        }

        #region Interface INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
    }
}
